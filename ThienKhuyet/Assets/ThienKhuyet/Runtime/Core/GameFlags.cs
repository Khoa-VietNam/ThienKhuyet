using System;
using System.Collections.Generic;

namespace ThienKhuyet.Core
{
    /// <summary>Serializable snapshot of <see cref="GameFlags"/> (JsonUtility cannot serialize dictionaries).</summary>
    [Serializable]
    public sealed class GameFlagsData
    {
        public List<string> flags = new List<string>();
        public List<string> intKeys = new List<string>();
        public List<int> intValues = new List<int>();
        public List<string> strKeys = new List<string>();
        public List<string> strValues = new List<string>();
    }

    /// <summary>Story state: boolean flags, integer counters and string values (player choices, ending, ...).</summary>
    public sealed class GameFlags
    {
        readonly HashSet<string> flags = new HashSet<string>();
        readonly Dictionary<string, int> ints = new Dictionary<string, int>();
        readonly Dictionary<string, string> strs = new Dictionary<string, string>();

        /// <summary>Raised whenever any flag/int/string changes (argument = key).</summary>
        public event Action<string> Changed;

        public bool Has(string flag)
        {
            return flags.Contains(flag);
        }

        public void Set(string flag, bool value = true)
        {
            bool changed = value ? flags.Add(flag) : flags.Remove(flag);
            if (changed) Changed?.Invoke(flag);
        }

        public int GetInt(string key)
        {
            return ints.TryGetValue(key, out int v) ? v : 0;
        }

        public void SetInt(string key, int value)
        {
            if (ints.TryGetValue(key, out int old) && old == value) return;
            ints[key] = value;
            Changed?.Invoke(key);
        }

        public int AddInt(string key, int delta)
        {
            int v = GetInt(key) + delta;
            SetInt(key, v);
            return v;
        }

        public string GetStr(string key)
        {
            return strs.TryGetValue(key, out string v) ? v : string.Empty;
        }

        public void SetStr(string key, string value)
        {
            if (strs.TryGetValue(key, out string old) && old == value) return;
            strs[key] = value;
            Changed?.Invoke(key);
        }

        public void Clear()
        {
            flags.Clear();
            ints.Clear();
            strs.Clear();
        }

        public GameFlagsData ToData()
        {
            var d = new GameFlagsData();
            d.flags.AddRange(flags);
            foreach (var kv in ints) { d.intKeys.Add(kv.Key); d.intValues.Add(kv.Value); }
            foreach (var kv in strs) { d.strKeys.Add(kv.Key); d.strValues.Add(kv.Value); }
            return d;
        }

        public void FromData(GameFlagsData d)
        {
            Clear();
            if (d == null) return;
            if (d.flags != null) for (int i = 0; i < d.flags.Count; i++) if (!string.IsNullOrEmpty(d.flags[i])) flags.Add(d.flags[i]);
            if (d.intKeys != null && d.intValues != null)
                for (int i = 0; i < d.intKeys.Count && i < d.intValues.Count; i++) if (!string.IsNullOrEmpty(d.intKeys[i])) ints[d.intKeys[i]] = d.intValues[i];
            if (d.strKeys != null && d.strValues != null)
                for (int i = 0; i < d.strKeys.Count && i < d.strValues.Count; i++) if (!string.IsNullOrEmpty(d.strKeys[i])) strs[d.strKeys[i]] = d.strValues[i];
        }
    }
}
