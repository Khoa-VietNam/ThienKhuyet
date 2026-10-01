using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace ThienKhuyet.Core
{
    /// <summary>
    /// Localization: plain-text tables (<c>key = value</c>, '#' comments, \n escapes) loaded from Resources/Localization.
    /// Vietnamese is the source language; other languages fall back to it key by key.
    /// Voice-over is keyed the same way (see AudioManager.TryGetVoice) so recorded lines can be dropped in later.
    /// </summary>
    public static class Loc
    {
        static readonly Dictionary<string, string> current = new Dictionary<string, string>();
        static readonly Dictionary<string, string> fallback = new Dictionary<string, string>();
        static readonly HashSet<string> warned = new HashSet<string>();

        public static string Language { get; private set; } = "vi";
        public static event Action Changed;

        public static readonly string[] Languages = { "vi", "en" };

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            current.Clear();
            fallback.Clear();
            warned.Clear();
            Language = "vi";
            Changed = null;
        }

        public static int Count => current.Count;

        /// <summary>Parses a table into 'into'. Returns the number of entries read.</summary>
        public static int ParseTable(string text, Dictionary<string, string> into)
        {
            if (string.IsNullOrEmpty(text)) return 0;
            int n = 0;
            string[] lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                if (line.Length == 0) continue;
                int start = 0;
                while (start < line.Length && char.IsWhiteSpace(line[start])) start++;
                if (start >= line.Length || line[start] == '#') continue;
                int eq = line.IndexOf('=', start);
                if (eq <= start) continue;
                string key = line.Substring(start, eq - start).Trim();
                string val = Unescape(line.Substring(eq + 1).Trim());
                into[key] = val;
                n++;
            }
            return n;
        }

        static string Unescape(string s)
        {
            if (s.IndexOf('\\') < 0) return s;
            var sb = new StringBuilder(s.Length);
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (c == '\\' && i + 1 < s.Length)
                {
                    char n = s[i + 1];
                    if (n == 'n') { sb.Append('\n'); i++; continue; }
                    if (n == 't') { sb.Append('\t'); i++; continue; }
                    if (n == '\\') { sb.Append('\\'); i++; continue; }
                }
                sb.Append(c);
            }
            return sb.ToString();
        }

        /// <summary>Loads every TextAsset under Resources/Localization/{lang}/ (any number of files per language).</summary>
        public static void Load(string language)
        {
            Language = language;
            fallback.Clear();
            current.Clear();
            LoadInto("vi", fallback);
            if (language == "vi") foreach (var kv in fallback) current[kv.Key] = kv.Value;
            else LoadInto(language, current);
            warned.Clear();
            Changed?.Invoke();
        }

        static void LoadInto(string lang, Dictionary<string, string> into)
        {
            TextAsset[] assets = Resources.LoadAll<TextAsset>("Localization/" + lang);
            for (int i = 0; i < assets.Length; i++) ParseTable(assets[i].text, into);
        }

        public static void LoadFromStrings(string language, string viText, string otherText)
        {
            Language = language;
            fallback.Clear();
            current.Clear();
            ParseTable(viText, fallback);
            if (language == "vi") foreach (var kv in fallback) current[kv.Key] = kv.Value;
            else ParseTable(otherText, current);
        }

        public static bool Has(string key)
        {
            return !string.IsNullOrEmpty(key) && (current.ContainsKey(key) || fallback.ContainsKey(key));
        }

        public static string T(string key)
        {
            if (string.IsNullOrEmpty(key)) return string.Empty;
            if (current.TryGetValue(key, out string v)) return v;
            if (fallback.TryGetValue(key, out v)) return v;
            if (warned.Add(key)) Debug.LogWarning("[Loc] Missing text key: " + key);
            return "[" + key + "]";
        }

        public static string T(string key, params object[] args)
        {
            string s = T(key);
            try
            {
                return string.Format(s, args);
            }
            catch (FormatException)
            {
                return s;
            }
        }

        /// <summary>Returns the translation if the key exists, otherwise the key itself (for already-resolved text).</summary>
        public static string TOrSelf(string keyOrText)
        {
            return Has(keyOrText) ? T(keyOrText) : keyOrText;
        }
    }
}
