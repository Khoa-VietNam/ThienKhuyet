using System.Collections.Generic;
using ThienKhuyet.Characters;
using ThienKhuyet.Core;
using ThienKhuyet.Data;
using ThienKhuyet.Gfx;
using ThienKhuyet.Player;
using UnityEngine;
using UnityEngine.Rendering;

namespace ThienKhuyet.Narrative
{
    /// <summary>
    /// A talkable character: procedural humanoid, idle gestures, wandering, looks at the player, shows a quest marker, and starts the
    /// first dialogue whose condition holds. Cutscenes can take over the transform (busy = true).
    /// </summary>
    public sealed class NpcController : MonoBehaviour, IInteractable
    {
        static readonly List<NpcController> all = new List<NpcController>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            all.Clear();
        }

        public static IReadOnlyList<NpcController> All => all;

        public static NpcController Find(string id)
        {
            for (int i = 0; i < all.Count; i++) if (all[i] != null && all[i].def.id == id) return all[i];
            return null;
        }

        public NpcDef def;
        public HumanoidRig rig;
        public HumanoidAnimator anim;
        public bool busy;                 // in dialogue or cutscene
        public bool frozen;               // standing still (cutscene staging)

        Vector3 home;
        Vector3 wanderTarget;
        bool walking;
        float wanderTimer, gestureTimer, markerTimer;
        Transform marker;
        MeshRenderer markerRenderer;
        int markerState;
        float yawTarget;
        float groundOffset;

        public string PromptKey => "prompt.talk";
        public bool CanInteract => !busy && Game.Mode == GameMode.Playing;
        public Vector3 InteractPoint => transform.position;
        public float InteractRadius => 3.4f;
        public string DisplayName => Loc.T(def.NameKey);

        public static NpcController Create(NpcDef def, Vector3 pos, float yaw, Transform parent)
        {
            var go = new GameObject("NPC_" + def.id);
            go.layer = GameLayers.Npc;
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));
            float scale = Mathf.Max(0.5f, def.look.height);
            var col = go.AddComponent<CapsuleCollider>();
            col.radius = 0.38f * Mathf.Max(0.7f, scale);
            col.height = 1.75f * scale;
            col.center = new Vector3(0f, col.height * 0.5f, 0f);
            var rb = go.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            var npc = go.AddComponent<NpcController>();
            npc.def = def;
            npc.home = pos;
            npc.rig = HumanoidBuilder.Build(def.look, go.transform, "Model");
            if (def.hasWeapon) WeaponBuilder.Attach(npc.rig, def.weapon, def.weaponColor, new Color(0.8f, 0.65f, 0.3f));
            npc.anim = go.AddComponent<HumanoidAnimator>();
            npc.anim.rig = npc.rig;
            npc.anim.stanceKind = HumanoidAnimator.CultivationStanceKind.None;
            npc.gestureTimer = Random.Range(3f, 9f);
            npc.BuildMarker();
            all.Add(npc);
            return npc;
        }

        void OnDestroy()
        {
            all.Remove(this);
        }

        // ------------------------------------------------------------------ marker
        static Texture2D questMark, readyMark;

        static Texture2D MarkTexture(bool exclamation)
        {
            if (exclamation && questMark != null) return questMark;
            if (!exclamation && readyMark != null) return readyMark;
            Texture2D t = ProcTex.Sprite(64, (u, v) =>
            {
                float a;
                if (exclamation)
                {
                    float bar = Mathf.Clamp01(1f - (Mathf.Abs(u) - (0.1f + (v + 0.1f) * 0.12f)) / 0.05f) * (v > -0.15f && v < 0.75f ? 1f : 0f);
                    float dot = Mathf.Clamp01(1f - (Mathf.Sqrt(u * u + (v + 0.5f) * (v + 0.5f)) - 0.14f) / 0.05f);
                    a = Mathf.Max(bar, dot);
                }
                else
                {
                    float d = Mathf.Abs(u) + Mathf.Abs(v);
                    a = Mathf.Clamp01(1f - (d - 0.55f) / 0.06f) * Mathf.Clamp01((d - 0.28f) / 0.06f + 0.3f);
                    float inner = Mathf.Clamp01(1f - (d - 0.16f) / 0.05f);
                    a = Mathf.Max(a, inner);
                }
                return new Color(1f, 1f, 1f, Mathf.Clamp01(a));
            }, exclamation ? "mark_exclamation" : "mark_ready");
            if (exclamation) questMark = t; else readyMark = t;
            return t;
        }

        void BuildMarker()
        {
            var go = new GameObject("QuestMarker");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, 2.25f * def.look.height + 0.3f, 0f);
            var mb = new MeshBuilder();
            mb.Quad(new Vector3(-0.3f, -0.3f, 0f), new Vector3(-0.3f, 0.3f, 0f), new Vector3(0.3f, 0.3f, 0f), new Vector3(0.3f, -0.3f, 0f), Vector3.back, new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0));
            go.AddComponent<MeshFilter>().sharedMesh = mb.ToMesh("marker", false);
            markerRenderer = go.AddComponent<MeshRenderer>();
            markerRenderer.shadowCastingMode = ShadowCastingMode.Off;
            markerRenderer.receiveShadows = false;
            marker = go.transform;
            marker.gameObject.SetActive(false);
        }

        void UpdateMarker()
        {
            int state = 0;
            QuestLog q = Game.Quests;
            if (q != null && !busy)
            {
                if (q.AvailableFrom(def.id) != null) state = 1;
                else
                {
                    foreach (QuestProgress p in q.Active)
                    {
                        for (int i = 0; i < p.def.objectives.Count; i++)
                        {
                            ObjectiveDef o = p.def.objectives[i];
                            if (o.type == ObjectiveType.Talk && o.target == def.id && !p.complete[i]) { state = 2; break; }
                        }
                        if (state == 2) break;
                    }
                }
            }
            if (state == markerState) return;
            markerState = state;
            marker.gameObject.SetActive(state != 0);
            if (state != 0)
            {
                Color c = state == 1 ? new Color(1f, 0.82f, 0.3f, 1f) : new Color(0.5f, 0.9f, 1f, 1f);
                markerRenderer.sharedMaterial = Mats.Cached("npc_marker_" + state, () => Mats.Particle(MarkTexture(state == 1), c, true));
            }
        }

        // ------------------------------------------------------------------ dialogue selection
        public string ResolveDialogue()
        {
            for (int i = 0; i < def.rules.Count; i++)
            {
                DialogueRule r = def.rules[i];
                if (string.IsNullOrEmpty(r.condition) || Cond.Eval(r.condition, Game.Manager)) return r.dialogueId;
            }
            return null;
        }

        public void Interact(PlayerController player)
        {
            string d = ResolveDialogue();
            if (string.IsNullOrEmpty(d)) return;
            Game.Manager.StartDialogue(d, this);
        }

        // ------------------------------------------------------------------ behaviour
        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            markerTimer -= dt;
            if (markerTimer <= 0f)
            {
                markerTimer = 0.7f;
                UpdateMarker();
            }
            if (markerState != 0 && marker != null && Camera.main != null)
            {
                Vector3 toCam = Camera.main.transform.position - marker.position;
                toCam.y = 0f;
                if (toCam.sqrMagnitude > 0.01f) marker.rotation = Quaternion.LookRotation(-toCam.normalized, Vector3.up);
                marker.localPosition = new Vector3(0f, 2.25f * def.look.height + 0.3f + Mathf.Sin(Time.time * 2.4f) * 0.07f, 0f);
            }
            if (busy || frozen) return;

            Transform player = Game.PlayerObject != null ? Game.PlayerObject.transform : null;
            float distP = player != null ? Vector3.Distance(transform.position, player.position) : 999f;
            if (player != null && distP < 7f && Game.Mode == GameMode.Playing)
            {
                anim.SetLookTarget(rig.headBone != null && Game.Player != null && Game.Player.rig != null ? Game.Player.rig.headBone : player, Mathf.Clamp01((7f - distP) / 3f));
                if (!walking && distP < 5f)
                {
                    Vector3 to = player.position - transform.position;
                    to.y = 0f;
                    if (to.sqrMagnitude > 0.01f) yawTarget = Mathx.DirToYaw(to);
                }
            }
            else anim.SetLookTarget(null, 0f);

            if (def.wanderRadius > 0f && (player == null || distP > 4f)) UpdateWander(dt);
            else walking = false;

            gestureTimer -= dt;
            if (gestureTimer <= 0f && !walking)
            {
                gestureTimer = Random.Range(7f, 16f);
                if (def.gestures != null && def.gestures.Length > 0 && !anim.IsPlayingAction("talk"))
                    anim.PlayAction(def.gestures[Random.Range(0, def.gestures.Length)], 1f);
            }

            float y = Mathx.DampAngle(transform.eulerAngles.y, yawTarget, 6f, dt);
            transform.rotation = Quaternion.Euler(0f, y, 0f);
            SnapToGround();
        }

        void UpdateWander(float dt)
        {
            wanderTimer -= dt;
            if (!walking && wanderTimer <= 0f)
            {
                Vector2 r = Random.insideUnitCircle * def.wanderRadius;
                wanderTarget = home + new Vector3(r.x, 0f, r.y);
                walking = true;
                wanderTimer = Random.Range(5f, 12f);
            }
            if (!walking) return;
            Vector3 to = wanderTarget - transform.position;
            to.y = 0f;
            float d = to.magnitude;
            if (d < 0.4f) { walking = false; return; }
            Vector3 dir = to / d;
            if (Physics.Raycast(transform.position + Vector3.up * 0.6f, dir, 1.2f, GameLayers.ObstacleMask, QueryTriggerInteraction.Ignore))
            {
                walking = false;
                return;
            }
            transform.position += dir * (1.25f * dt);
            yawTarget = Mathx.DirToYaw(dir);
        }

        void SnapToGround()
        {
            if (Game.World == null) return;
            Vector3 p = transform.position;
            float g = Game.World.GroundHeightAt(p);
            if (Physics.Raycast(p + Vector3.up * 1.2f, Vector3.down, out RaycastHit hit, 3f, GameLayers.GroundMask, QueryTriggerInteraction.Ignore)) g = hit.point.y;
            p.y = Mathf.Lerp(p.y, g, 0.5f);
            transform.position = p;
        }

        // ------------------------------------------------------------------ cutscene / dialogue helpers
        public void FaceTowards(Vector3 worldPoint)
        {
            Vector3 to = worldPoint - transform.position;
            to.y = 0f;
            if (to.sqrMagnitude > 0.01f) yawTarget = Mathx.DirToYaw(to);
        }

        public void ReturnHome()
        {
            transform.position = home;
        }
    }
}
