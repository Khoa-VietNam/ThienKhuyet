using System.Collections.Generic;
using ThienKhuyet.Audio;
using ThienKhuyet.Core;
using ThienKhuyet.Gfx;
using ThienKhuyet.Narrative;
using ThienKhuyet.Player;
using ThienKhuyet.UI;
using ThienKhuyet.World;
using UnityEngine;
using UnityEngine.Playables;

namespace ThienKhuyet.Cinematics
{
    /// <summary>Plays script or TimelineAsset cinematics with camera hand-off, staging, audio, subtitles, choices and clean restoration.</summary>
    public sealed class CutscenePlayer : MonoBehaviour
    {
        CutsceneDefinition active;
        int beatIndex;
        float elapsed;
        float startTimeScale = 1f;
        GameMode previousMode;
        bool activeCutscene;
        bool waitingChoice;
        string pendingChoiceFlag;
        string[] pendingChoiceValues;
        bool restoreEnvironmentFrozen;
        float restoreEnvironmentTime;
        bool restoreStage;
        Vector3 restorePlayerPosition;
        float restorePlayerYaw;
        CutsceneStage stage;
        PlayableDirector director;
        Vector3 shotFromPosition, shotToPosition;
        Vector3 shotFromLook, shotToLook;
        float shotAt, shotDuration = 1f, shotFov = 55f;
        bool shotActive;
        float subtitleEnd = -1f;
        float timeScaleEnd = -1f;
        bool hasPendingEnd;
        string endId;

        public bool IsPlaying => activeCutscene;
        public string CurrentId => active != null ? active.id : string.Empty;

        public static CutscenePlayer Create(Transform parent)
        {
            var go = new GameObject("CutscenePlayer");
            if (parent != null) go.transform.SetParent(parent, false);
            return go.AddComponent<CutscenePlayer>();
        }

        public bool Play(string id, bool force = false)
        {
            CutsceneDefinition definition = CutsceneLibrary.Get(id);
            if (definition == null)
            {
                Debug.LogWarning("[CutscenePlayer] Cutscene not found: " + id);
                Game.UI?.Toast("toast.cutscene_missing");
                return false;
            }
            return Play(definition, force);
        }

        public bool Play(CutsceneDefinition definition, bool force = false)
        {
            if (activeCutscene || definition == null) return false;
            if (!force && !definition.replayable && Game.Session != null && Game.Session.seenCutscenes.Contains(definition.id)) return false;
            active = definition;
            activeCutscene = true;
            waitingChoice = false;
            hasPendingEnd = false;
            beatIndex = 0;
            elapsed = 0f;
            previousMode = Game.Mode;
            startTimeScale = TimeController.Instance != null ? TimeController.Instance.CinematicScale : Time.timeScale;
            pendingChoiceFlag = string.Empty;
            pendingChoiceValues = null;
            subtitleEnd = -1f;
            timeScaleEnd = -1f;
            endId = string.Empty;
            restoreStage = !string.IsNullOrEmpty(active.stage) && active.stage != "none";
            restorePlayerPosition = Game.PlayerObject != null ? Game.PlayerObject.transform.position : Vector3.zero;
            restorePlayerYaw = Game.PlayerObject != null ? Game.PlayerObject.transform.eulerAngles.y : 0f;
            restoreEnvironmentFrozen = Game.World != null && Game.World.Env != null && Game.World.Env.frozen;
            restoreEnvironmentTime = Game.World != null && Game.World.Env != null ? Game.World.Env.TimeOfDay : 7.5f;

            Game.Manager?.OnCutsceneBegin(active.id);
            if (Game.Input != null) Game.Input.PushLock("cutscene");
            Game.Mode = GameMode.Cutscene;
            if (Game.Player != null) Game.Player.SetControlled(true);
            if (Game.World != null && Game.World.Env != null) Game.World.Env.frozen = true;
            if (Game.Camera != null) Game.Camera.BeginCinematic();
            if (Game.UI != null) Game.UI.SetCinematicBars(true);
            if (Game.Audio != null)
            {
                Game.Audio.DuckMusic(0.48f);
                if (!string.IsNullOrEmpty(active.music)) Game.Audio.Music(active.music, 1.5f);
            }
            if (Game.PostFx != null) Game.PostFx.SetCinematic(true, 12f, 5.6f, 55f, 0.86f, -0.15f, 0.18f, 0.02f, 0.16f);
            if (restoreStage)
            {
                stage = CutsceneStageBuilder.Build(active.stage, transform, active.playerStart, active.playerYaw);
                if (stage != null && Game.Player != null)
                {
                    Game.Player.Teleport(stage.playerStart, stage.playerYaw);
                    Game.Player.SetControlled(true);
                }
            }

            if (active.timeline != null)
            {
                if (director == null) director = gameObject.AddComponent<PlayableDirector>();
                director.Stop();
                director.playableAsset = active.timeline;
                director.time = 0d;
                director.initialTime = 0d;
                director.timeUpdateMode = DirectorUpdateMode.UnscaledGameTime;
                director.extrapolationMode = DirectorWrapMode.None;
                director.Play();
            }

            if (!string.IsNullOrEmpty(active.titleKey)) Game.UI?.ShowBanner(Loc.T(active.titleKey), "");
            EventBus.Publish(new CutsceneEvent { id = active.id, started = true });
            ProcessBeats();
            return true;
        }

        void Update()
        {
            if (!activeCutscene || active == null) return;
            float dt = Time.unscaledDeltaTime;
            if (dt <= 0f) return;
            if (Game.Input != null && Game.Input.SkipPressed && active.id != "ending_choice")
            {
                Finish(true);
                return;
            }
            if (waitingChoice) return;
            elapsed += dt;
            if (director != null && director.playableAsset == active.timeline && director.state == PlayState.Playing)
                elapsed = Mathf.Max(elapsed, (float)director.time);

            if (timeScaleEnd >= 0f && elapsed >= timeScaleEnd)
            {
                if (TimeController.Instance != null) TimeController.Instance.CinematicScale = startTimeScale;
                else Time.timeScale = startTimeScale;
                timeScaleEnd = -1f;
            }
            if (subtitleEnd >= 0f && elapsed >= subtitleEnd)
            {
                Game.UI?.HideSubtitle();
                subtitleEnd = -1f;
            }
            ProcessBeats();
            UpdateShot();
            UpdateStageMove();

            bool timelineFinished = active.timeline != null && director != null && (director.state != PlayState.Playing || director.time >= director.duration);
            if (hasPendingEnd || elapsed >= active.duration || timelineFinished) Finish(false);
        }

        void ProcessBeats()
        {
            if (active == null || waitingChoice) return;
            while (beatIndex < active.beats.Count && active.beats[beatIndex].at <= elapsed + 0.0001f)
            {
                CutsceneBeat beat = active.beats[beatIndex++];
                Execute(beat);
                if (!activeCutscene || waitingChoice || hasPendingEnd) break;
            }
        }

        void Execute(CutsceneBeat beat)
        {
            switch (beat.command)
            {
                case CutsceneCommand.Shot:
                    shotFromPosition = Game.Camera != null ? Game.Camera.transform.position : ResolvePosition(new Vector3(0f, 3f, -5f));
                    shotFromLook = shotActive ? CurrentShotLook() : ResolvePosition(Vector3.zero) + Vector3.up;
                    shotToPosition = ResolvePosition(beat.position);
                    shotToLook = ResolvePosition(beat.target);
                    shotAt = elapsed;
                    shotDuration = Mathf.Max(0.05f, beat.duration);
                    shotFov = beat.values.Length > 0 ? beat.values[0] : 55f;
                    shotActive = true;
                    break;
                case CutsceneCommand.Subtitle:
                    Game.UI?.ShowSubtitle(beat.who, beat.text, beat.color, beat.values.Length > 0 ? beat.values[0] : 1f);
                    subtitleEnd = elapsed + Mathf.Max(0.1f, beat.duration);
                    break;
                case CutsceneCommand.Dialogue:
                    Game.UI?.ShowSubtitle(beat.who, beat.text, beat.color, 1f);
                    if (beat.duration > 0f) subtitleEnd = elapsed + beat.duration;
                    break;
                case CutsceneCommand.Animation:
                    ActorAnimator(beat.actor)?.PlayAction(beat.key, beat.values.Length > 0 ? beat.values[0] : 1f);
                    break;
                case CutsceneCommand.Expression:
                    ActorAnimator(beat.actor)?.SetExpression(beat.value);
                    break;
                case CutsceneCommand.Move:
                    BeginMove(beat);
                    break;
                case CutsceneCommand.Vfx:
                    Vfx.Play(beat.key, ResolvePosition(beat.position), Quaternion.identity, beat.values.Length > 0 ? beat.values[0] : 1f);
                    break;
                case CutsceneCommand.Sfx:
                    Game.Audio?.Sfx2D(beat.key, beat.values.Length > 0 ? beat.values[0] : 1f, beat.values.Length > 1 ? beat.values[1] : 1f);
                    break;
                case CutsceneCommand.Music:
                    Game.Audio?.Music(beat.key, beat.values.Length > 0 ? beat.values[0] : 2f);
                    break;
                case CutsceneCommand.Ambience:
                    Game.Audio?.SetAmbience(beat.key, beat.values.Length > 0 ? beat.values[0] : 0.5f, beat.extra, beat.values.Length > 1 ? beat.values[1] : 0f);
                    break;
                case CutsceneCommand.Environment:
                    if (Game.World != null && Game.World.Env != null) Game.World.Env.Override(
                        beat.values.Length > 0 ? beat.values[0] : 1f,
                        beat.values.Length > 1 ? beat.values[1] : 1f,
                        beat.values.Length > 2 ? beat.values[2] : 1f,
                        beat.color,
                        beat.values.Length > 3 ? beat.values[3] : 0f);
                    if (Game.PostFx != null) Game.PostFx.SetTint(beat.color, beat.values.Length > 3 ? beat.values[3] : 0f);
                    break;
                case CutsceneCommand.DepthOfField:
                    if (Game.PostFx != null && beat.values.Length >= 8)
                        Game.PostFx.SetCinematic(beat.flag, beat.values[0], beat.values[1], beat.values[2], beat.values[3], beat.values[4], beat.values[5], beat.values[6], beat.values[7]);
                    break;
                case CutsceneCommand.Fade:
                    Game.UI?.FadeTo(beat.color, beat.values.Length > 0 ? beat.values[0] : 1f, beat.duration);
                    break;
                case CutsceneCommand.Bars:
                    Game.UI?.SetCinematicBars(beat.flag);
                    break;
                case CutsceneCommand.TimeScale:
                    float scale = beat.values.Length > 0 ? beat.values[0] : 1f;
                    if (TimeController.Instance != null) TimeController.Instance.CinematicScale = scale;
                    else Time.timeScale = scale;
                    timeScaleEnd = beat.duration > 0f ? elapsed + beat.duration : -1f;
                    break;
                case CutsceneCommand.Flag:
                    if (!string.IsNullOrEmpty(beat.key)) Game.Manager?.SetFlag(beat.key, beat.flag);
                    break;
                case CutsceneCommand.Teleport:
                    if (!string.IsNullOrEmpty(beat.key)) Game.Manager?.Teleport(beat.key);
                    break;
                case CutsceneCommand.Title:
                    if (!string.IsNullOrEmpty(beat.key)) Game.Manager?.ShowTitle(beat.key);
                    break;
                case CutsceneCommand.Shake:
                    Game.Camera?.Shake(beat.values.Length > 0 ? beat.values[0] : 0.2f, beat.values.Length > 1 ? beat.values[1] : 0.35f);
                    break;
                case CutsceneCommand.Choice:
                    BeginChoice(beat);
                    break;
                case CutsceneCommand.End:
                    hasPendingEnd = true;
                    if (!string.IsNullOrEmpty(beat.value)) endId = beat.value;
                    break;
            }
        }

        Vector3 ResolvePosition(Vector3 p)
        {
            return stage != null ? stage.origin + p : p;
        }

        Vector3 CurrentShotLook()
        {
            if (!shotActive) return ResolvePosition(Vector3.zero) + Vector3.up;
            float t = Mathf.Clamp01((elapsed - shotAt) / Mathf.Max(0.01f, shotDuration));
            t = t * t * (3f - 2f * t);
            return Vector3.Lerp(shotFromLook, shotToLook, t);
        }

        void UpdateShot()
        {
            if (!shotActive || Game.Camera == null) return;
            float t = Mathf.Clamp01((elapsed - shotAt) / Mathf.Max(0.01f, shotDuration));
            t = t * t * (3f - 2f * t);
            Vector3 p = Vector3.Lerp(shotFromPosition, shotToPosition, t);
            Vector3 look = Vector3.Lerp(shotFromLook, shotToLook, t);
            Vector3 direction = look - p;
            if (direction.sqrMagnitude < 0.001f) direction = Vector3.forward;
            Game.Camera.SetCinematicPose(p, Quaternion.LookRotation(direction.normalized, Vector3.up), shotFov);
        }

        Transform moveTarget;
        Vector3 moveFrom, moveTo;
        float moveAt, moveDuration;
        float moveYaw;
        NpcController movingNpc;

        void BeginMove(CutsceneBeat beat)
        {
            Transform actor = ActorTransform(beat.actor);
            if (actor == null) return;
            moveTarget = actor;
            moveFrom = actor.position;
            moveTo = ResolvePosition(beat.position);
            moveAt = elapsed;
            moveDuration = Mathf.Max(0.01f, beat.duration);
            moveYaw = beat.values.Length > 0 ? beat.values[0] : actor.eulerAngles.y;
            movingNpc = beat.actor == "player" ? null : NpcController.Find(beat.actor);
            if (movingNpc != null) movingNpc.frozen = true;
        }

        void UpdateStageMove()
        {
            if (moveTarget == null) return;
            float t = Mathf.Clamp01((elapsed - moveAt) / moveDuration);
            float s = t * t * (3f - 2f * t);
            moveTarget.position = Vector3.Lerp(moveFrom, moveTo, s);
            moveTarget.rotation = Quaternion.Euler(0f, moveYaw, 0f);
            if (t >= 1f)
            {
                moveTarget = null;
                if (movingNpc != null) movingNpc.frozen = false;
                movingNpc = null;
            }
        }

        Transform ActorTransform(string id)
        {
            if (id == "player") return Game.PlayerObject != null ? Game.PlayerObject.transform : null;
            NpcController actor = NpcController.Find(id);
            return actor != null ? actor.transform : null;
        }

        HumanoidAnimator ActorAnimator(string id)
        {
            if (id == "player") return Game.Player != null ? Game.Player.anim : null;
            NpcController actor = NpcController.Find(id);
            return actor != null ? actor.anim : null;
        }

        void BeginChoice(CutsceneBeat beat)
        {
            if (beat.options == null || beat.options.Length < 2) return;
            waitingChoice = true;
            pendingChoiceFlag = beat.key;
            pendingChoiceValues = beat.options;
            if (director != null && director.state == PlayState.Playing) director.Pause();
            var labels = new List<string>(beat.options.Length);
            for (int i = 0; i < beat.options.Length; i++) labels.Add(Loc.TOrSelf(beat.options[i]));
            Game.UI?.ShowCutsceneChoice(beat.text, labels, OnChoice);
        }

        void OnChoice(int index)
        {
            if (!activeCutscene || !waitingChoice) return;
            index = Mathf.Clamp(index, 0, pendingChoiceValues != null ? pendingChoiceValues.Length - 1 : 0);
            string value = pendingChoiceValues != null && pendingChoiceValues.Length > index ? pendingChoiceValues[index] : index.ToString();
            if (!string.IsNullOrEmpty(pendingChoiceFlag)) Game.Manager?.SetFlag(pendingChoiceFlag + "." + value, true);
            if (active != null && active.id == "ending_choice")
                endId = value != null && value.StartsWith("ending.return", System.StringComparison.Ordinal) ? "return" : "stay";
            Game.Manager?.OnChoiceMade(pendingChoiceFlag, value);
            EventBus.Publish(new ChoiceMadeEvent { key = pendingChoiceFlag, value = value });
            waitingChoice = false;
            pendingChoiceFlag = string.Empty;
            pendingChoiceValues = null;
            if (director != null && active != null && active.timeline != null && director.time < director.duration) director.Play();
        }

        void Finish(bool skipped)
        {
            if (!activeCutscene) return;
            string finishedId = active != null ? active.id : string.Empty;
            GameMode restoreMode = previousMode;
            if (director != null && director.state == PlayState.Playing) director.Stop();
            Game.UI?.HideDialogue();
            Game.UI?.HideDialogueChoices();
            Game.UI?.HideSubtitleImmediate();
            Game.UI?.SetCinematicBars(false);
            Game.UI?.FadeTo(Color.black, 0f, 0.15f);
            if (Game.PostFx != null) Game.PostFx.ClearCinematic();
            if (Game.World != null && Game.World.Env != null)
            {
                Game.World.Env.ClearOverride();
                Game.World.Env.frozen = restoreEnvironmentFrozen;
                Game.World.Env.SetTime(restoreEnvironmentTime);
            }
            if (Game.Audio != null) Game.Audio.DuckMusic(1f);
            if (Game.Camera != null) Game.Camera.EndCinematic(0.9f);
            if (restoreStage && Game.Player != null) Game.Player.Teleport(restorePlayerPosition, restorePlayerYaw);
            if (stage != null && stage.root != null) Destroy(stage.root);
            stage = null;
            restoreStage = false;
            if (movingNpc != null) movingNpc.frozen = false;
            movingNpc = null;
            moveTarget = null;
            if (Game.Player != null) Game.Player.SetControlled(false);
            if (TimeController.Instance != null) TimeController.Instance.CinematicScale = startTimeScale;
            else Time.timeScale = startTimeScale;
            timeScaleEnd = -1f;
            activeCutscene = false;
            waitingChoice = false;
            active = null;
            if (Game.Input != null) Game.Input.PopLock("cutscene");
            if (Game.Mode == GameMode.Cutscene) Game.Mode = restoreMode;
            if (Game.Session != null && !string.IsNullOrEmpty(finishedId)) Game.Session.seenCutscenes.Add(finishedId);
            if (!string.IsNullOrEmpty(endId)) Game.Manager?.OnCutsceneEnding(endId);
            Game.Manager?.OnCutsceneEnd(finishedId, skipped);
            EventBus.Publish(new CutsceneEvent { id = finishedId, started = false });
            if (restoreMode == GameMode.Dialogue) Game.Manager?.ResumeDialogueAfterCutscene();
        }

        public void Stop(bool skipped = true) { Finish(skipped); }

        void OnDestroy()
        {
            if (activeCutscene) Finish(true);
        }
    }

    /// <summary>Small procedural, isolated set used for memory and dream sequences. No imported or generated artwork is required.</summary>
    public sealed class CutsceneStage
    {
        public GameObject root;
        public Vector3 origin;
        public Vector3 playerStart;
        public float playerYaw;
    }

    public static class CutsceneStageBuilder
    {
        public static CutsceneStage Build(string id, Transform parent, Vector3 playerStart, float playerYaw)
        {
            if (string.IsNullOrEmpty(id) || id == "none") return null;
            Vector3 origin = new Vector3(2000f, 0f, 2000f);
            GameObject root = new GameObject("CutsceneStage_" + id);
            if (parent != null) root.transform.SetParent(parent, false);
            root.transform.position = origin;

            Material stone = Mats.Lit(new Color(0.12f, 0.15f, 0.19f), 0.25f);
            Material paleStone = Mats.Lit(new Color(0.38f, 0.43f, 0.48f), 0.35f);
            Material jade = Mats.Glow(new Color(0.23f, 0.83f, 0.72f), 2.4f);
            Material gold = Mats.Glow(new Color(0.9f, 0.65f, 0.23f), 1.4f);
            Material dark = Mats.Lit(new Color(0.035f, 0.055f, 0.09f), 0.15f);
            Cube(root.transform, "MoonlitGround", Vector3.zero + Vector3.down * 0.5f, new Vector3(72f, 1f, 72f), dark);
            Cube(root.transform, "RitualPlinth", new Vector3(0f, -0.08f, 2f), new Vector3(9f, 0.6f, 8f), stone);
            Cylinder(root.transform, "OuterRune", new Vector3(0f, 0.18f, 2f), new Vector3(8.4f, 0.08f, 8.4f), jade, 64);
            Cylinder(root.transform, "InnerRune", new Vector3(0f, 0.24f, 2f), new Vector3(5.3f, 0.06f, 5.3f), gold, 64);
            for (int i = 0; i < 8; i++)
            {
                float a = i * Mathf.PI / 4f;
                Vector3 p = new Vector3(Mathf.Cos(a) * 6f, 0.3f, 2f + Mathf.Sin(a) * 6f);
                Cube(root.transform, "BrokenPillar_" + i, p + Vector3.up * (i % 2 == 0 ? 1.4f : 0.7f), new Vector3(0.55f, i % 2 == 0 ? 2.8f : 1.4f, 0.55f), i % 2 == 0 ? paleStone : stone);
            }
            GameObject shard = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            shard.name = "FloatingMemoryShard";
            shard.transform.SetParent(root.transform, false);
            shard.transform.localPosition = new Vector3(0f, 1.2f, 8f);
            shard.transform.localScale = new Vector3(1.15f, 1.9f, 0.55f);
            Renderer renderer = shard.GetComponent<Renderer>();
            renderer.sharedMaterial = jade;
            Collider collider = shard.GetComponent<Collider>();
            if (collider != null) UnityEngine.Object.Destroy(collider);
            GameObject halo = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            halo.name = "ShardHalo";
            halo.transform.SetParent(root.transform, false);
            halo.transform.localPosition = new Vector3(0f, 1.15f, 8f);
            halo.transform.localScale = new Vector3(2.8f, 0.035f, 2.8f);
            halo.GetComponent<Renderer>().sharedMaterial = gold;
            Collider haloCollider = halo.GetComponent<Collider>();
            if (haloCollider != null) UnityEngine.Object.Destroy(haloCollider);

            Vector3 start = origin + playerStart;
            return new CutsceneStage { root = root, origin = origin, playerStart = start, playerYaw = playerYaw };
        }

        static GameObject Cube(Transform parent, string name, Vector3 local, Vector3 scale, Material material)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = local;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = material;
            return go;
        }

        static GameObject Cylinder(Transform parent, string name, Vector3 local, Vector3 scale, Material material, int segments)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = local;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = material;
            return go;
        }
    }
}
