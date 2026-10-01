using System;
using System.Collections;
using System.Collections.Generic;
using ThienKhuyet.Audio;
using ThienKhuyet.Characters;
using ThienKhuyet.Cinematics;
using ThienKhuyet.Combat;
using ThienKhuyet.Cultivation;
using ThienKhuyet.Data;
using ThienKhuyet.Enemies;
using ThienKhuyet.Gfx;
using ThienKhuyet.Items;
using ThienKhuyet.Narrative;
using ThienKhuyet.Player;
using ThienKhuyet.SaveLoad;
using ThienKhuyet.UI;
using ThienKhuyet.World;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ThienKhuyet.Core
{
    /// <summary>Application bootstrap and authoritative bridge between runtime systems, story effects and persistent state.</summary>
    [DefaultExecutionOrder(-1000)]
    public sealed class GameManager : MonoBehaviour, IGameEffects
    {
        const int DefaultSeed = 20260;
        Transform runtimeRoot;
        DialogueRunner dialogueRunner;
        GameMode menuReturnMode = GameMode.Playing;
        Coroutine deathRoutine;
        bool bootStarted;
        int lootRoll;

        public GameMode Mode => Game.Mode;
        public int RealmIndex => Game.Session != null && Game.Session.player != null ? Game.Session.player.cultivation.Realm : 0;
        public int RealmStage => Game.Session != null && Game.Session.player != null ? Game.Session.player.cultivation.Stage : 1;
        public int Act => Game.Session != null ? Game.Session.act : 1;
        public string BuildName => Game.Session != null && Game.Session.player != null ? Game.Session.player.BuildName : "wu";
        public bool IsNight => Game.World != null && Game.World.Env != null ? Game.World.Env.IsNight : Game.Session != null && (Game.Session.timeOfDay < 5.3f || Game.Session.timeOfDay > 19.2f);
        public string QuickHealItemId => FindQuickHeal();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void BootstrapAfterSceneLoad()
        {
            if (FindAnyObjectByType<GameManager>() != null) return;
            new GameObject("ThienKhuyetRuntime").AddComponent<GameManager>();
        }

        void Awake()
        {
            if (Game.Manager != null && Game.Manager != this)
            {
                Destroy(gameObject);
                return;
            }
            Game.Manager = this;
            DontDestroyOnLoad(gameObject);
            name = "ThienKhuyetRuntime";
            runtimeRoot = new GameObject("RuntimeSystems").transform;
            runtimeRoot.SetParent(transform, false);
            Application.targetFrameRate = 60;
            GameLayers.ConfigurePhysics();

            Game.Settings = new GameSettings();
            Game.Settings.Load();
            Game.Settings.Apply();
            Loc.Load(Game.Settings.language);
            ContentDB.Load();
            DialogueLibrary.Load();
            CutsceneLibrary.Load();
            QuestLibrary.Load();

            Game.Input = new GameInput();
            Game.Input.Enable();
            TimeController.Create(runtimeRoot);
            CreateCamera();
            Game.PostFx = PostFx.Create(runtimeRoot);
            AudioManager.Create(runtimeRoot, Game.Settings);
            Game.UI = UIRoot.Create(runtimeRoot);
            Game.Cutscenes = CutscenePlayer.Create(runtimeRoot);
            Game.World = new GameObject("WorldManager").AddComponent<WorldManager>();
            Game.World.transform.SetParent(runtimeRoot, false);
            dialogueRunner = new DialogueRunner();
            ResetSession(DefaultSeed);
            Game.Mode = GameMode.Loading;
            Game.UI.SetVisible(false);
            if (!bootStarted)
            {
                bootStarted = true;
                StartCoroutine(BootRoutine());
            }
        }

        void CreateCamera()
        {
            Camera cam = Camera.main;
            if (cam == null)
            {
                GameObject go = new GameObject("Main Camera");
                go.tag = GameTags.MainCamera;
                cam = go.AddComponent<Camera>();
            }
            cam.enabled = true;
            cam.transform.position = new Vector3(250f, 12f, 184f);
            cam.transform.rotation = Quaternion.Euler(25f, 0f, 0f);
            Game.Camera = CameraRig.Create(cam);
        }

        IEnumerator BootRoutine()
        {
            Game.Mode = GameMode.Loading;
            Game.UI.SetLoading(0.02f, "ui.loading_world");
            int seed = Game.Session != null ? Game.Session.seed : DefaultSeed;
            yield return Game.World.Build(seed, Game.Settings, Game.Camera.cam, (p, stage) => Game.UI.SetLoading(0.04f + p * 0.9f, stage));
            if (Game.World == null || !Game.World.Ready)
            {
                Game.UI.SetLoading(1f, "ui.loading_failed");
                Debug.LogError("[GameManager] World build did not complete.");
                yield break;
            }

            if (Game.World.Env != null) Game.World.Env.SetTime(Game.Session.timeOfDay);
            BuildNpcs();
            Vector3 spawn = Game.World.AnchorPos("spawn_player", new Vector3(250f, Game.World.GroundHeightAt(new Vector3(250f, 0f, 200f)), 200f));
            Game.Camera.transform.position = spawn + new Vector3(0f, 8f, -14f);
            Game.Camera.transform.rotation = Quaternion.LookRotation(spawn + Vector3.up - Game.Camera.transform.position, Vector3.up);
            Game.UI.SetLoading(1f, "ui.ready");
            yield return null;
            Game.UI.FinishLoading();
            Game.Mode = GameMode.MainMenu;
            ShowMainMenu();
            SetCursor(false);
        }

        void BuildNpcs()
        {
            if (Game.World == null || Game.World.Pois == null) return;
            for (int i = 0; i < Game.World.Pois.npcs.Count; i++)
            {
                NpcPlacement placement = Game.World.Pois.npcs[i];
                NpcDef def = NpcLibrary.Get(placement.npcId);
                if (def == null) { Debug.LogWarning("[GameManager] Missing NPC definition: " + placement.npcId); continue; }
                Vector3 position = placement.pos;
                float yaw = placement.yaw;
                if (!string.IsNullOrEmpty(placement.anchor) && Game.World.TryGetAnchor(placement.anchor, out Anchor anchor))
                {
                    position = anchor.pos;
                    yaw = anchor.yaw;
                }
                if (position == Vector3.zero) continue;
                NpcController.Create(def, position, yaw, Game.World.transform);
            }
        }

        void ResetSession(int seed)
        {
            if (Game.Session != null)
            {
                if (Game.Session.quests != null) Game.Session.quests.Unsubscribe();
                Game.Session.flags.Changed -= OnFlagChanged;
            }
            var session = new GameSession { seed = seed, timeOfDay = 7.5f, day = 1, act = 1, respawnPoint = "spawn_player" };
            session.player = new PlayerState();
            session.quests = new QuestLog(this);
            session.quests.LoadDefs(QuestLibrary.All);
            session.quests.Subscribe();
            session.flags.Changed += OnFlagChanged;
            Game.Session = session;
            lootRoll = 0;
        }

        void OnFlagChanged(string key)
        {
            EventBus.Publish(new FlagChangedEvent { key = key });
            if (Game.Quests != null) Game.Quests.Refresh();
        }

        void Update()
        {
            if (Game.Session != null && (Game.Mode == GameMode.Playing || Game.Mode == GameMode.Dialogue || Game.Mode == GameMode.Cutscene))
                Game.Session.playSeconds += Time.unscaledDeltaTime;

            if (Game.Mode == GameMode.Dialogue) dialogueRunner?.Tick();
            if (Game.Input == null || Game.UI == null) return;
            if (Game.Mode == GameMode.Playing)
            {
                if (Game.Input.MenuPressed || Game.Input.ScreenPressed(6))
                {
                    OpenMenu(MenuView.TabPause);
                    return;
                }
                for (int i = 0; i < 6; i++)
                {
                    if (Game.Input.ScreenPressed(i)) { OpenMenu(i); return; }
                }
            }
            if (Game.Mode == GameMode.MainMenu && Game.Input.SkipPressed)
            {
                // Escape has no destructive default on the title screen; native quit is exposed by the button.
            }
        }

        void OnDestroy()
        {
            if (Game.Manager != this) return;
            if (Game.Session != null)
            {
                if (Game.Session.quests != null) Game.Session.quests.Unsubscribe();
                Game.Session.flags.Changed -= OnFlagChanged;
            }
            if (Game.Input != null) Game.Input.Dispose();
            Game.Manager = null;
            Game.Input = null;
        }

        void ShowMainMenu()
        {
            bool hasSave = false;
            for (int i = 0; i < SaveSystem.SlotCount; i++) if (SaveSystem.Read(i) != null) { hasSave = true; break; }
            Game.UI.ShowMainMenu(hasSave, StartNewGame, ContinueGame, OpenTitleSettings, QuitGame);
            Game.UI.SetVisible(false);
        }

        void OpenTitleSettings()
        {
            OpenMenu(MenuView.TabSettings, true);
        }

        public void StartNewGame()
        {
            if (Game.World == null || !Game.World.Ready) return;
            Game.UI.HideMenu();
            Game.UI.HideMainMenu();
            ResetSession(DefaultSeed);
            PrepareNewWorldState();
            PlayerState playerState = Game.Session.player;
            playerState.inventory.Add("branch_staff", 1);
            playerState.inventory.Add("straw_sandals", 1);
            playerState.inventory.Add("berry_wild", 3);
            playerState.inventory.Add("healing_paste", 2);
            playerState.inventory.Add("spirit_stone", 30);
            playerState.inventory.Add("herb_basic", 2);
            // Keep one paste for the first breakthrough after the player consumes the starter paste for insight.
            playerState.inventory.Add("body_paste", 2);
            playerState.Equip("branch_staff");
            playerState.Equip("straw_sandals");
            playerState.LearnSkill("qi_bolt");
            playerState.LearnSkill("gather_qi");
            ActivatePlayer(playerState, null);
            Game.Session.respawnPoint = "spawn_player";
            Game.Session.flags.Set("new_game_started", true);
            Game.Mode = GameMode.Playing;
            if (TimeController.Instance != null) TimeController.Instance.Paused = false;
            Game.UI.HideGameOver();
            Game.UI.SetVisible(true);
            Game.UI.Toast(Loc.T("toast.controls"));
            Game.UI.ShowBanner(Loc.T("ui.title.village"), Loc.T("ui.title.subtitle"));
            Game.World.RevealFog(Game.PlayerObject.transform.position, 100f);
            Game.Quests?.StartAutoQuests();
            Game.World.Streamer?.SetFocus(Game.PlayerObject.transform);
            SetCursor(true);
            if (!Game.Cutscenes.Play("intro_memory")) Game.UI.ShowMessage("ui.narrator", "intro.opening");
        }

        void PrepareNewWorldState()
        {
            Game.Session.killedSpawns.Clear();
            Game.Session.openedContainers.Clear();
            Game.Session.discoveredPois.Clear();
            Game.Session.seenCutscenes.Clear();
            Game.Session.nodeRespawn.Clear();
            Game.Session.respawnPoint = "spawn_player";
            Game.Session.timeOfDay = 7.5f;
            Game.Session.day = 1;
            if (Game.World != null)
            {
                Array.Clear(Game.World.Fog, 0, Game.World.Fog.Length);
                Game.World.FogDirty = true;
                if (Game.World.Env != null) Game.World.Env.SetTime(7.5f);
                Container[] containers = FindObjectsByType<Container>();
                for (int i = 0; i < containers.Length; i++) containers[i].ResetForNewGame();
                Gatherable[] nodes = FindObjectsByType<Gatherable>();
                for (int i = 0; i < nodes.Length; i++) nodes[i].ResetForNewGame();
                if (Game.World.Streamer != null) Game.World.Streamer.RespawnEnemies();
            }
            foreach (NpcController npc in NpcController.All)
                if (npc != null) { npc.busy = false; npc.frozen = false; }
        }

        public void ContinueGame()
        {
            int slot = -1;
            if (SaveSystem.Read(0) != null) slot = 0;
            else
                for (int i = 1; i < SaveSystem.SlotCount; i++) if (SaveSystem.Read(i) != null) { slot = i; break; }
            if (slot < 0) { Game.UI?.Toast("toast.no_save"); return; }
            Game.UI.HideMainMenu();
            LoadFromSlot(slot);
        }

        void ActivatePlayer(PlayerState playerState, GameSaveData save)
        {
            EnsurePlayerObject();
            Game.PlayerObject.SetActive(true);
            Game.PlayerVitals.stats = playerState.stats;
            Game.PlayerVitals.realm = playerState.cultivation.Realm;
            Game.PlayerVitals.SetFull();
            Game.PlayerVitals.hp = save != null ? Mathf.Clamp(save.player.hp, 1f, Game.PlayerVitals.MaxHp) : Game.PlayerVitals.MaxHp;
            Game.PlayerVitals.stamina = save != null ? Mathf.Clamp(save.player.stamina, 0f, Game.PlayerVitals.MaxStamina) : Game.PlayerVitals.MaxStamina;
            Game.PlayerVitals.qi = save != null ? Mathf.Clamp(save.player.qi, 0f, Game.PlayerVitals.MaxQi) : Game.PlayerVitals.MaxQi;
            Game.PlayerVitals.poise = Game.PlayerVitals.MaxPoise;
            Game.Player.SetState(PState.Locomotion);
            Vector3 position;
            float yaw;
            if (save != null && save.hasPlayerPosition)
            {
                position = save.playerPosition;
                yaw = save.playerYaw;
            }
            else if (Game.World.TryGetAnchor(Game.Session.respawnPoint, out Anchor respawn))
            {
                position = respawn.pos;
                yaw = respawn.yaw;
            }
            else if (Game.World.TryGetAnchor("spawn_player", out Anchor start))
            {
                position = start.pos;
                yaw = start.yaw;
            }
            else { position = new Vector3(250f, 30f, 200f); yaw = 20f; }
            Game.Player.Teleport(position, yaw);
            Game.Player.cc.enabled = true;
            Game.Player.SetState(PState.Locomotion);
            Game.Camera.Follow(Game.PlayerObject.transform, true);
            Game.World.Streamer?.SetFocus(Game.PlayerObject.transform);
        }

        void EnsurePlayerObject()
        {
            if (Game.PlayerObject != null) return;
            GameObject go = new GameObject("Player");
            go.layer = GameLayers.Player;
            go.transform.SetParent(runtimeRoot, false);
            CharacterController cc = go.AddComponent<CharacterController>();
            cc.radius = 0.36f;
            cc.height = 1.78f;
            cc.center = new Vector3(0f, 0.89f, 0f);
            cc.stepOffset = 0.32f;
            cc.slopeLimit = 48f;
            cc.skinWidth = 0.045f;
            cc.minMoveDistance = 0f;

            Vitals vitals = go.AddComponent<Vitals>();
            vitals.team = Team.Player;
            vitals.stats = Game.Session.player.stats;
            vitals.realm = Game.Session.player.cultivation.Realm;

            var appearance = new CharacterAppearance
            {
                outfit = OutfitStyle.Wanderer,
                hairStyle = HairStyle.Topknot,
                hair = new Color(0.055f, 0.045f, 0.04f),
                skin = new Color(0.83f, 0.66f, 0.54f),
                primary = new Color(0.58f, 0.72f, 0.68f),
                secondary = new Color(0.18f, 0.27f, 0.31f),
                accent = new Color(0.86f, 0.66f, 0.28f),
                pants = new Color(0.19f, 0.23f, 0.26f),
                accessories = Accessory.Headband,
                height = 1f,
                bulk = 0.96f
            };
            HumanoidRig rig = HumanoidBuilder.Build(appearance, go.transform, "Model");
            HumanoidAnimator animator = go.AddComponent<HumanoidAnimator>();
            animator.rig = rig;
            animator.stanceKind = HumanoidAnimator.CultivationStanceKind.Sword;

            PlayerController controller = go.AddComponent<PlayerController>();
            controller.cc = cc;
            controller.rig = rig;
            controller.anim = animator;
            controller.Bind(vitals);
            PlayerCombat combat = go.AddComponent<PlayerCombat>();
            combat.pc = controller;
            combat.vitals = vitals;
            combat.anim = animator;
            controller.combat = combat;
            controller.interactor = go.AddComponent<Interactor>();
            GameLayers.SetRecursive(go, GameLayers.Player);
            Game.PlayerObject = go;
            Game.Player = controller;
            Game.PlayerVitals = vitals;
            vitals.SetFull();
        }

        public void OpenMenu(int tab, bool preserveTitle = false)
        {
            if (Game.UI == null || (Game.Mode != GameMode.Playing && Game.Mode != GameMode.MainMenu)) return;
            menuReturnMode = Game.Mode;
            Game.Input?.PushLock("menu");
            Game.Mode = GameMode.Menu;
            if (TimeController.Instance != null) TimeController.Instance.Paused = true;
            Game.UI.SetVisible(false);
            Game.UI.OpenMenu(tab, preserveTitle);
            SetCursor(false);
        }

        public void OnMenuClosed()
        {
            if (Game.Input != null) Game.Input.PopLock("menu");
            if (TimeController.Instance != null) TimeController.Instance.Paused = false;
            if (Game.Mode == GameMode.Menu) Game.Mode = menuReturnMode;
            Game.UI?.SetVisible(Game.Mode == GameMode.Playing);
            SetCursor(Game.Mode == GameMode.Playing);
        }

        void SetCursor(bool gameplay)
        {
            Cursor.lockState = gameplay ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !gameplay;
        }

        // ------------------------------------------------------------------ death / rest / time
        public void OnPlayerDied()
        {
            if (Game.Session == null || Game.Mode == GameMode.GameOver) return;
            Game.Session.deaths++;
            if (deathRoutine != null) StopCoroutine(deathRoutine);
            deathRoutine = StartCoroutine(DeathRoutine());
        }

        IEnumerator DeathRoutine()
        {
            yield return new WaitForSecondsRealtime(1.2f);
            deathRoutine = null;
            if (Game.PlayerVitals == null || Game.PlayerVitals.IsAlive) yield break;
            Game.Mode = GameMode.GameOver;
            if (TimeController.Instance != null) TimeController.Instance.Paused = true;
            Game.UI?.SetVisible(false);
            Game.UI?.ShowGameOver(RespawnAtShrine, ReturnToMainMenu);
            SetCursor(false);
        }

        void RespawnAtShrine()
        {
            if (Game.Session == null || Game.Player == null || Game.PlayerVitals == null) return;
            Game.UI?.HideGameOver();
            if (TimeController.Instance != null) TimeController.Instance.Paused = false;
            Vector3 pos = Game.World.AnchorPos(Game.Session.respawnPoint, Game.World.AnchorPos("spawn_player", Game.PlayerObject.transform.position));
            float yaw = Game.World.TryGetAnchor(Game.Session.respawnPoint, out Anchor a) ? a.yaw : 20f;
            Game.Player.Teleport(pos, yaw);
            Game.PlayerVitals.Revive(0.6f);
            Game.Player.SetState(PState.Locomotion);
            Game.Mode = GameMode.Playing;
            Game.World.Streamer?.RespawnEnemies();
            Game.Camera.Follow(Game.PlayerObject.transform, true);
            Game.UI?.SetVisible(true);
            Game.UI?.Toast("toast.respawned");
            SetCursor(true);
            SaveToSlot(0);
        }

        public void OnMeditationTick(bool atSpot)
        {
            AddExp(atSpot ? 4.5f : 1.2f);
        }

        public void RestAtShrine(Shrine shrine)
        {
            if (shrine == null || Game.Session == null) return;
            Game.Session.respawnPoint = "shrine_" + shrine.shrineId;
            if (!Game.World.TryGetAnchor(Game.Session.respawnPoint, out _)) Game.Session.respawnPoint = "spawn_player";
            Game.PlayerVitals?.SetFull();
            Game.Player?.SetState(PState.Locomotion);
            Game.World.Streamer?.RespawnEnemies();
            Game.Session.flags.Set("rested_at_shrine", true);
            Game.UI?.Toast("toast.shrine_rest");
            Game.Audio?.Sfx2D("shrine", 0.7f);
            Game.Quests?.Refresh();
            SaveToSlot(0);
        }

        public void Sleep(Bed bed)
        {
            if (Game.Session == null) return;
            Game.Session.day++;
            Game.Session.timeOfDay = 6.5f;
            if (Game.World != null && Game.World.Env != null) Game.World.Env.SetTime(Game.Session.timeOfDay);
            Game.PlayerVitals?.SetFull();
            Game.Player?.SetState(PState.Locomotion);
            OnNewDay();
            Game.UI?.Toast("toast.slept");
            SaveToSlot(0);
        }

        public void OnNewDay()
        {
            if (Game.Session == null) return;
            Game.Session.flags.AddInt("days_survived", 1);
            Game.Quests?.Refresh();
        }

        // ------------------------------------------------------------------ combat / rewards
        public void OnEnemyKilled(EnemyBrain brain)
        {
            if (brain == null || brain.def == null) return;
            if (Game.Session != null)
            {
                Game.Session.enemyKillCounts.TryGetValue(brain.def.id, out int total);
                Game.Session.enemyKillCounts[brain.def.id] = total + 1;
            }
            EventBus.Publish(new EnemyKilledEvent
            {
                enemyId = brain.def.id,
                position = brain.transform.position,
                boss = brain.def.boss,
                elite = brain.def.elite
            });
            if (Game.Session != null && !brain.isMinion && !string.IsNullOrEmpty(brain.spawnId)) Game.Session.killedSpawns.Add(brain.spawnId);
            if (!brain.isMinion && brain.def.exp > 0f) AddExp(brain.def.exp);
            if (!brain.isMinion && brain.def.spiritStones > 0) GiveItem("spirit_stone", brain.def.spiritStones, true, true);
            if (!brain.isMinion && !string.IsNullOrEmpty(brain.def.lootTable)) RollLoot(brain.def.lootTable, brain.transform.position + Vector3.up * 0.5f, true);
            Game.Camera?.Shake(brain.def.boss ? 0.25f : 0.08f, brain.def.boss ? 0.45f : 0.2f);
            Game.Quests?.Refresh();
        }

        public void AddExp(float amount)
        {
            PlayerState player = Game.Session != null ? Game.Session.player : null;
            if (player == null || amount <= 0f) return;
            int oldRealm = player.cultivation.Realm;
            int oldStage = player.cultivation.Stage;
            float gained = amount * (1f + Mathf.Max(0f, player.stats.Get(StatId.ExpGain)));
            int stages = player.cultivation.AddExp(gained);
            if (stages > 0 || player.cultivation.Realm != oldRealm)
            {
                Game.UI?.Toast(Loc.T("toast.cultivation_progress", Loc.T("realm." + player.cultivation.Realm + ".name"), player.cultivation.Stage));
                Game.Audio?.Sfx2D("realm_up", 0.8f);
                Game.PostFx?.HitPulse(0.22f);
                Game.Quests?.Refresh();
            }
            else if (player.cultivation.Stage != oldStage) Game.Quests?.Refresh();
        }

        public string FindQuickHeal()
        {
            if (Game.Session == null || Game.Session.player == null) return string.Empty;
            Inventory inventory = Game.Session.player.inventory;
            string[] priority = { "healing_paste", "roast_meat", "berry_wild" };
            for (int i = 0; i < priority.Length; i++) if (inventory.Has(priority[i])) return priority[i];
            return string.Empty;
        }

        public void UseQuickHeal()
        {
            string id = FindQuickHeal();
            if (string.IsNullOrEmpty(id)) { Game.UI?.Toast("toast.no_healing_item"); return; }
            UseItem(id);
        }

        public bool UseItem(string id)
        {
            PlayerState player = Game.Session != null ? Game.Session.player : null;
            ItemDef def = ContentDB.Item(id);
            if (player == null || def == null || !def.IsUsable || !player.inventory.Has(id)) return false;
            if (def.type == ItemType.Manual)
            {
                if (!player.LearnSkill(def.teachSkill)) { Game.UI?.Toast("toast.manual_known"); return false; }
                player.inventory.Remove(id, 1);
                Game.UI?.Toast(Loc.T("toast.skill_learned", Loc.T("skill." + def.teachSkill + ".name")));
            }
            else
            {
                float exp = CultivationExpFromItem(id);
                bool usable = false;
                if (def.restoreHp > 0f && Game.PlayerVitals != null && Game.PlayerVitals.hp < Game.PlayerVitals.MaxHp - 0.1f) { Game.PlayerVitals.Heal(def.restoreHp); usable = true; }
                if (def.restoreQi > 0f && Game.PlayerVitals != null && Game.PlayerVitals.qi < Game.PlayerVitals.MaxQi - 0.1f) { Game.PlayerVitals.RestoreQi(def.restoreQi); usable = true; }
                if (def.restoreStamina > 0f && Game.PlayerVitals != null && Game.PlayerVitals.stamina < Game.PlayerVitals.MaxStamina - 0.1f) { Game.PlayerVitals.RestoreStamina(def.restoreStamina); usable = true; }
                if (exp > 0f && !player.cultivation.IsFull) { AddExp(exp); usable = true; }
                if (id == "body_paste" && !Game.Session.flags.Has("learned_body_tempering"))
                {
                    SetFlag("learned_body_tempering", true);
                    usable = true;
                }
                if (!string.IsNullOrEmpty(def.useFx)) usable = true;
                if (!usable) { Game.UI?.Toast("toast.resource_full"); return false; }
                player.inventory.Remove(id, 1);
                if (!string.IsNullOrEmpty(def.useFx)) Fx.Apply(def.useFx, this);
                Game.UI?.Toast(Loc.T("toast.item_used", Loc.T(def.nameKey)));
            }
            EventBus.Publish(new ItemUsedEvent { itemId = id });
            Game.Audio?.Sfx2D("potion", 0.75f);
            Game.Quests?.Refresh();
            return true;
        }

        static float CultivationExpFromItem(string id)
        {
            switch (id)
            {
                case "body_paste": return 28f;
                case "foundation_pill": return 180f;
                case "golden_core_elixir": return 700f;
                case "nascent_soul_fruit": return 2000f;
                default: return 0f;
            }
        }

        public bool GiveItem(string itemId, int count, bool showToast = true, bool dropOverflow = true)
        {
            if (count <= 0 || Game.Session == null || Game.Session.player == null || ContentDB.Item(itemId) == null) return false;
            Inventory inventory = Game.Session.player.inventory;
            if (!dropOverflow && !inventory.CanAdd(itemId, count)) return false;
            int overflow = inventory.Add(itemId, count);
            int added = count - overflow;
            if (added > 0)
            {
                EventBus.Publish(new ItemGainedEvent { itemId = itemId, count = added });
                if (showToast) Game.UI?.Toast(Loc.T("toast.item_gained", Loc.T(ContentDB.Item(itemId).nameKey), added));
                Game.Audio?.Sfx2D(ContentDB.Item(itemId).rarity >= Rarity.Rare ? "pickup_rare" : "pickup", 0.5f);
            }
            if (overflow > 0 && dropOverflow)
            {
                Vector3 pos = Game.PlayerObject != null ? Game.PlayerObject.transform.position + Game.PlayerObject.transform.forward * 1.2f : Vector3.zero;
                PickupItem.Spawn(itemId, overflow, pos, false);
                Game.UI?.Toast("toast.inventory_overflow");
            }
            if (Game.Quests != null) Game.Quests.Refresh();
            return added > 0 && (overflow == 0 || dropOverflow);
        }

        public void GiveItem(string itemId, int count) { GiveItem(itemId, count, true, true); }

        public void TakeItem(string itemId, int count)
        {
            if (Game.Session == null || Game.Session.player == null) return;
            Game.Session.player.inventory.Remove(itemId, count);
            Game.Quests?.Refresh();
        }

        public bool DropItem(string itemId, int count)
        {
            if (Game.Session == null || Game.Session.player == null || count <= 0) return false;
            if (!Game.Session.player.inventory.Remove(itemId, count)) { Game.UI?.Toast("toast.item_not_owned"); return false; }
            Vector3 position = Game.PlayerObject != null ? Game.PlayerObject.transform.position + Game.PlayerObject.transform.forward * 1.1f : Vector3.zero;
            PickupItem.Spawn(itemId, count, position, true);
            Game.Quests?.Refresh();
            return true;
        }

        public void RollLoot(string tableId, Vector3 position, bool drop)
        {
            LootTableDef table = ContentDB.Loot(tableId);
            if (table == null) return;
            float luck = Game.Session != null && Game.Session.player != null ? Game.Session.player.stats.Get(StatId.LootLuck) : 0f;
            int seed = (Game.Session != null ? Game.Session.seed : DefaultSeed) ^ (++lootRoll * 7919) ^ (int)(Game.Session != null ? Game.Session.playSeconds : Time.time);
            Rng rng = new Rng(seed);
            var results = new List<ItemCost>();
            table.Roll(ref rng, luck, results);
            for (int i = 0; i < results.Count; i++)
            {
                ItemCost result = results[i];
                if (drop) PickupItem.Spawn(result.itemId, result.count, position + Vector3.up * 0.2f, true);
                else GiveItem(result.itemId, result.count, true, true);
            }
        }

        public bool EquipItem(string id)
        {
            if (Game.Session == null || Game.Session.player == null) return false;
            if (!Game.Session.player.Equip(id)) { Game.UI?.Toast("toast.cannot_equip"); return false; }
            RefreshPlayerStats();
            Game.Audio?.Sfx2D("equip", 0.7f);
            Game.UI?.Toast("toast.equipped");
            return true;
        }

        public bool UnequipSlot(EquipSlot slot)
        {
            if (Game.Session == null || Game.Session.player == null || !Game.Session.player.Unequip(slot)) return false;
            RefreshPlayerStats();
            return true;
        }

        void RefreshPlayerStats()
        {
            if (Game.Session == null || Game.Session.player == null || Game.PlayerVitals == null) return;
            Game.PlayerVitals.stats = Game.Session.player.stats;
            Game.PlayerVitals.realm = Game.Session.player.cultivation.Realm;
            Game.PlayerVitals.hp = Mathf.Min(Game.PlayerVitals.hp, Game.PlayerVitals.MaxHp);
            Game.PlayerVitals.stamina = Mathf.Min(Game.PlayerVitals.stamina, Game.PlayerVitals.MaxStamina);
            Game.PlayerVitals.qi = Mathf.Min(Game.PlayerVitals.qi, Game.PlayerVitals.MaxQi);
        }

        public void SpendAttribute(AttributeId id)
        {
            if (Game.Session == null || Game.Session.player == null) return;
            if (!Game.Session.player.attributes.Spend(id)) { Game.UI?.Toast("toast.no_attribute_points"); return; }
            Game.Session.player.Recompute();
            RefreshPlayerStats();
        }

        // ------------------------------------------------------------------ cultivation
        public float BreakthroughChance()
        {
            PlayerState player = Game.Session != null ? Game.Session.player : null;
            if (player == null) return 0f;
            RealmDef current = ContentDB.Realm(player.cultivation.Realm);
            if (current == null) return 0f;
            float insight = player.attributes.insight * 0.008f;
            return Mathf.Clamp01(current.breakthroughBaseChance + insight - player.cultivation.Realm * 0.035f);
        }

        public bool CanBreakthrough(out string reasonKey)
        {
            reasonKey = string.Empty;
            PlayerState player = Game.Session != null ? Game.Session.player : null;
            if (player == null) { reasonKey = "ui.breakthrough_unavailable"; return false; }
            if (player.cultivation.AtLastRealm) { reasonKey = "ui.breakthrough_last_realm"; return false; }
            if (!player.cultivation.ReadyForBreakthrough) { reasonKey = "ui.breakthrough_need_exp"; return false; }
            RealmDef req = ContentDB.Realm(player.cultivation.Realm);
            if (req.requiresMeditationSpot && (Game.PlayerObject == null || Game.World == null || !Game.World.IsMeditationSpot(Game.PlayerObject.transform.position)))
            { reasonKey = "ui.breakthrough_need_spot"; return false; }
            if (!string.IsNullOrEmpty(req.breakthroughFlag) && !Game.Session.flags.Has(req.breakthroughFlag))
            { reasonKey = "ui.breakthrough_need_flag"; return false; }
            if (Game.PlayerVitals != null && Game.PlayerVitals.qi < Game.PlayerVitals.MaxQi * req.breakthroughQiPercent)
            { reasonKey = "ui.breakthrough_need_qi"; return false; }
            for (int i = 0; i < req.breakthroughItems.Length; i++)
                if (!player.inventory.Has(req.breakthroughItems[i].itemId, req.breakthroughItems[i].count)) { reasonKey = "ui.breakthrough_need_items"; return false; }
            return true;
        }

        public bool TryBreakthrough()
        {
            if (!CanBreakthrough(out string reason)) { Game.UI?.Toast(reason); return false; }
            PlayerState player = Game.Session.player;
            RealmDef req = ContentDB.Realm(player.cultivation.Realm);
            for (int i = 0; i < req.breakthroughItems.Length; i++) player.inventory.Remove(req.breakthroughItems[i].itemId, req.breakthroughItems[i].count);
            if (Game.PlayerVitals != null) Game.PlayerVitals.RestoreQi(-Game.PlayerVitals.MaxQi * req.breakthroughQiPercent);
            float chance = BreakthroughChance();
            Rng rng = new Rng(Game.Session.seed ^ (int)Game.Session.playSeconds ^ (player.cultivation.Realm * 83492791) ^ Game.Session.day);
            bool success = rng.Value() <= chance;
            int realmBefore = player.cultivation.Realm;
            if (success)
            {
                player.cultivation.Breakthrough();
                Game.PlayerVitals.realm = player.cultivation.Realm;
                player.Recompute();
                Game.PlayerVitals.SetFull();
                Game.UI?.Toast(Loc.T("toast.breakthrough_success", Loc.T("realm." + player.cultivation.Realm + ".name")));
                Game.Audio?.Sfx2D("realm_up", 1f);
                Game.PostFx?.HitPulse(0.5f);
                Game.Camera?.Shake(0.4f, 0.8f);
                Vfx.Play("aura_gold", Game.PlayerObject != null ? Game.PlayerObject.transform.position : Vector3.zero, Quaternion.identity, 2f);
                GameFeel.SlowMo(0.2f, 0.4f);
            }
            else
            {
                player.cultivation.ApplyFailurePenalty(0.2f);
                Game.UI?.Toast(Loc.T("toast.breakthrough_failed", Mathf.RoundToInt(chance * 100f)));
                Game.Audio?.Sfx2D("hit_heavy", 0.75f);
                Game.Camera?.Shake(0.22f, 0.5f);
            }
            EventBus.Publish(new BreakthroughResultEvent { success = success, realm = success ? player.cultivation.Realm : realmBefore });
            Game.Quests?.Refresh();
            return success;
        }

        // ------------------------------------------------------------------ save/load
        public bool SaveToSlot(int slot)
        {
            if (Game.Session == null || Game.PlayerObject == null || Game.PlayerVitals == null) return false;
            string location = Game.World != null ? Game.World.CurrentZone : "unknown";
            bool saved = SaveSystem.Write(slot, Game.Session, Game.PlayerObject.transform.position, Game.PlayerObject.transform.eulerAngles.y, Game.PlayerVitals, location, Game.World);
            if (saved) Game.UI?.Toast(Loc.T("toast.saved_slot", slot + 1));
            else Game.UI?.Toast("toast.save_failed");
            return saved;
        }

        public bool LoadFromSlot(int slot)
        {
            GameSaveData data = SaveSystem.Read(slot);
            if (data == null) { Game.UI?.Toast("toast.load_failed"); return false; }
            if (Game.Session == null) ResetSession(data.seed);
            SaveSystem.Apply(data, Game.Session);
            ActivatePlayer(Game.Session.player, data);
            if (Game.World != null)
            {
                Game.World.LoadFog(data.fog);
                if (Game.World.Streamer != null) Game.World.Streamer.RespawnEnemies();
            }
            Game.UI?.HideMenu();
            Game.UI?.HideMainMenu();
            Game.UI?.HideGameOver();
            Game.Mode = GameMode.Playing;
            Game.Input?.ClearLocks();
            if (TimeController.Instance != null) TimeController.Instance.Paused = false;
            Game.UI?.SetVisible(true);
            Game.Quests?.Refresh();
            SetCursor(true);
            Game.UI?.Toast(Loc.T("toast.loaded_slot", slot + 1));
            return true;
        }

        public void SaveGame()
        {
            if (Game.Mode == GameMode.Playing || Game.Mode == GameMode.Menu) SaveToSlot(0);
        }

        public void ReturnToMainMenu()
        {
            if (Game.PlayerObject != null) Game.PlayerObject.SetActive(false);
            Game.UI?.HideMenu();
            Game.UI?.HideGameOver();
            Game.UI?.HideDialogue();
            Game.Cutscenes?.Stop(true);
            Game.Input?.ClearLocks();
            if (TimeController.Instance != null) TimeController.Instance.Paused = false;
            Game.Mode = GameMode.MainMenu;
            Game.UI?.SetVisible(false);
            ShowMainMenu();
            SetCursor(false);
        }

        public void QuitGame()
        {
            Game.Settings?.Save();
            Application.Quit();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#endif
        }

        // ------------------------------------------------------------------ IGameQuery
        public bool HasFlag(string flag) => Game.Session != null && Game.Session.flags.Has(flag);
        public int GetInt(string key) => Game.Session != null ? Game.Session.flags.GetInt(key) : 0;
        public string GetStr(string key) => Game.Session != null ? Game.Session.flags.GetStr(key) : string.Empty;
        public int ItemCount(string itemId) => Game.Session != null && Game.Session.player != null ? Game.Session.player.inventory.Count(itemId) : 0;
        public string QuestStatus(string questId) => Game.Quests != null ? Game.Quests.Status(questId) : "none";
        public int Reputation(string factionId) => Game.Session != null ? Game.Session.flags.GetInt("rep." + factionId) : 0;

        // ------------------------------------------------------------------ IGameEffects
        public void SetFlag(string flag, bool value = true) { if (Game.Session != null) Game.Session.flags.Set(flag, value); }
        public void SetInt(string key, int value) { if (Game.Session != null) Game.Session.flags.SetInt(key, value); }
        public void SetStr(string key, string value)
        {
            if (Game.Session == null) return;
            if (key == "path" && Game.Session.player != null)
            {
                string normalized = (value ?? string.Empty).ToLowerInvariant();
                CultivationPath path = normalized == "wu" ? CultivationPath.Wu : normalized == "jian" ? CultivationPath.Jian : normalized == "fa" ? CultivationPath.Fa : normalized == "hybrid" ? CultivationPath.Hybrid : CultivationPath.None;
                Game.Session.player.path = path;
                Game.Session.player.Recompute();
                RefreshPlayerStats();
                Game.UI?.Toast(Loc.T("toast.path_chosen", Loc.T("path." + normalized)));
            }
            Game.Session.flags.SetStr(key, value);
        }
        public void StartQuest(string questId) { Game.Quests?.Start(questId); }
        public void CompleteQuest(string questId) { Game.Quests?.Complete(questId); }
        public void FailQuest(string questId) { Game.Quests?.Fail(questId); }
        public void AddReputation(string factionId, int delta)
        {
            if (Game.Session == null) return;
            Game.Session.flags.AddInt("rep." + factionId, delta);
        }
        void IGameEffects.AddExp(float amount) { AddExp(amount); }
        public void PlayCutscene(string cutsceneId) { Game.Cutscenes?.Play(cutsceneId); }
        public void HealPlayer() { Game.PlayerVitals?.SetFull(); }
        public void SetAct(int act)
        {
            if (Game.Session == null) return;
            int next = Mathf.Clamp(act, 1, 4);
            if (Game.Session.act == next) return;
            Game.Session.act = next;
            Game.Quests?.StartAutoQuests();
        }
        public void LearnSkill(string skillId)
        {
            if (Game.Session != null && Game.Session.player.LearnSkill(skillId)) Game.UI?.Toast(Loc.T("toast.skill_learned", Loc.T("skill." + skillId + ".name")));
        }
        public void Teleport(string anchorId)
        {
            if (Game.Player == null || Game.World == null) return;
            if (!Game.World.TryGetAnchor(anchorId, out Anchor anchor))
            {
                Game.UI?.Toast("toast.anchor_missing");
                return;
            }
            Game.Player.Teleport(anchor.pos, anchor.yaw);
            Game.World.RevealFog(anchor.pos, 75f);
        }
        public void EndGame(string endingId) { ShowEnding(endingId); }
        public void Toast(string textKey) { Game.UI?.Toast(Loc.Has(textKey) ? Loc.T(textKey) : textKey); }
        public void Unlock(string id)
        {
            if (Game.Session == null || string.IsNullOrEmpty(id)) return;
            Game.Session.player.unlocks.Add(id);
            SetFlag("unlock." + id, true);
            Game.UI?.Toast(Loc.T("toast.unlocked", Loc.TOrSelf(id)));
        }
        public void ShowTitle(string textKey) { Game.UI?.ShowBanner(Loc.TOrSelf(textKey), ""); }

        public void StartDialogue(string id, NpcController speaker)
        {
            if (dialogueRunner == null || !dialogueRunner.Begin(id, speaker)) return;
            EventBus.Publish(new InteractEvent { objectId = speaker != null && speaker.def != null ? "npc:" + speaker.def.id : "dialogue:" + id });
        }

        // Dialogue/cutscene hand-off
        public void OnCutsceneBegin(string id)
        {
            if (dialogueRunner != null && dialogueRunner.Active) dialogueRunner.SuspendForCutscene();
        }

        public void OnCutsceneEnd(string id, bool skipped)
        {
            if (skipped) Game.UI?.Toast("toast.cutscene_skipped");
        }

        public void ResumeDialogueAfterCutscene() { dialogueRunner?.ResumeAfterCutscene(); }
        public void OnCutsceneEnding(string endingId) { ShowEnding(endingId); }
        public void OnChoiceMade(string key, string value)
        {
            if (Game.Session != null && !string.IsNullOrEmpty(key)) Game.Session.flags.SetStr("choice." + key, value);
        }

        void ShowEnding(string endingId)
        {
            if (Game.Session != null) Game.Session.endingId = endingId ?? "";
            Game.UI?.HideDialogue();
            Game.UI?.HideMenu();
            Game.Mode = GameMode.Ending;
            if (TimeController.Instance != null) TimeController.Instance.Paused = true;
            Game.UI?.SetVisible(false);
            Game.UI?.ShowEnding(endingId, ReturnToMainMenu);
            SetCursor(false);
            SaveToSlot(0);
        }

        // convenience wrappers retained for the view and world components
        public void OnMenuOpen(int tab) { OpenMenu(tab); }
    }
}
