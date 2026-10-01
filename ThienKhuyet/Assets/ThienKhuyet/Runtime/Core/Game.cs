using System.Collections.Generic;
using ThienKhuyet.Audio;
using ThienKhuyet.Cinematics;
using ThienKhuyet.Combat;
using ThienKhuyet.Gfx;
using ThienKhuyet.Narrative;
using ThienKhuyet.Player;
using ThienKhuyet.UI;
using ThienKhuyet.World;
using UnityEngine;

namespace ThienKhuyet.Core
{
    public enum GameMode { Boot = 0, MainMenu, Loading, Playing, Cutscene, Dialogue, Menu, GameOver, Ending }

    /// <summary>All state of one playthrough that must be saved: flags, quests, player build, world changes, clock.</summary>
    public sealed class GameSession
    {
        public readonly GameFlags flags = new GameFlags();
        public Player.PlayerState player;
        public QuestLog quests;
        public readonly HashSet<string> killedSpawns = new HashSet<string>();
        public readonly HashSet<string> openedContainers = new HashSet<string>();
        public readonly HashSet<string> discoveredPois = new HashSet<string>();
        public readonly HashSet<string> seenCutscenes = new HashSet<string>();
        public readonly Dictionary<string, double> nodeRespawn = new Dictionary<string, double>();
        public int seed = 20260;
        public int act = 1;
        public float timeOfDay = 7.5f;
        public int day = 1;
        public double playSeconds;
        public int deaths;
        public string respawnPoint = "spawn";
        public string endingId = "";
        public float visitedFogResolution;
    }

    /// <summary>Global access to the running game's services. Reset on every play session (domain reload may be disabled).</summary>
    public static class Game
    {
        public static GameManager Manager;
        public static GameSettings Settings;
        public static GameInput Input;
        public static GameSession Session;
        public static WorldManager World;
        public static PlayerController Player;
        public static GameObject PlayerObject;
        public static Vitals PlayerVitals;
        public static CameraRig Camera;
        public static UIRoot UI;
        public static PostFx PostFx;
        public static CutscenePlayer Cutscenes;
        public static AudioManager Audio => AudioManager.Instance;
        public static GameMode Mode = GameMode.Boot;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Manager = null;
            Settings = null;
            Input = null;
            Session = null;
            World = null;
            Player = null;
            PlayerObject = null;
            PlayerVitals = null;
            Camera = null;
            UI = null;
            PostFx = null;
            Cutscenes = null;
            Mode = GameMode.Boot;
        }

        public static QuestLog Quests => Session != null ? Session.quests : null;
        public static bool IsPlaying => Mode == GameMode.Playing;
    }
}
