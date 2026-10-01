using System.Collections.Generic;
using UnityEngine;

namespace ThienKhuyet.Characters
{
    /// <summary>Loads pose scripts from Resources/Animation once and serves clips by id.</summary>
    public static class PoseLibrary
    {
        static Dictionary<string, PoseClip> humanoid;
        static Dictionary<string, PoseClip> quadruped;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            humanoid = null;
            quadruped = null;
        }

        public static void Load()
        {
            if (humanoid != null && quadruped != null) return;
            var errors = new List<string>();
            humanoid = LoadFile("Animation/humanoid_poses", Rigs.Humanoid, errors);
            quadruped = LoadFile("Animation/quadruped_poses", Rigs.Quadruped, errors);
            for (int i = 0; i < errors.Count; i++) Debug.LogWarning("[PoseLibrary] " + errors[i]);
        }

        static Dictionary<string, PoseClip> LoadFile(string path, RigSpec rig, List<string> errors)
        {
            var ta = Resources.Load<TextAsset>(path);
            if (ta == null)
            {
                Debug.LogError("[PoseLibrary] missing pose file Resources/" + path);
                return new Dictionary<string, PoseClip>();
            }
            return PoseParser.Parse(ta.text, rig, path, errors);
        }

        public static PoseClip Humanoid(string id)
        {
            Load();
            return humanoid.TryGetValue(id, out PoseClip c) ? c : null;
        }

        public static PoseClip Quadruped(string id)
        {
            Load();
            return quadruped.TryGetValue(id, out PoseClip c) ? c : null;
        }

        public static IEnumerable<string> HumanoidIds
        {
            get
            {
                Load();
                return humanoid.Keys;
            }
        }

        public static IEnumerable<string> QuadrupedIds
        {
            get
            {
                Load();
                return quadruped.Keys;
            }
        }
    }
}
