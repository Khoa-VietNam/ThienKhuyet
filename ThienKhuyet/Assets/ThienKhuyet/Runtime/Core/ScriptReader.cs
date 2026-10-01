using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace ThienKhuyet.Core
{
    /// <summary>One parsed script line: a verb, positional arguments and key=value pairs.</summary>
    public sealed class ScriptLine
    {
        public int No;
        public string Source;
        public string Raw;
        public string Verb;
        public readonly List<string> Args = new List<string>();
        public readonly Dictionary<string, string> Kv = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public bool Has(string key)
        {
            return Kv.ContainsKey(key);
        }

        public string Get(string key, string def = null)
        {
            return Kv.TryGetValue(key, out string v) ? v : def;
        }

        public string Arg(int i, string def = null)
        {
            return i >= 0 && i < Args.Count ? Args[i] : def;
        }

        public float GetFloat(string key, float def = 0f)
        {
            if (Kv.TryGetValue(key, out string v) && ScriptReader.TryFloat(v, out float f)) return f;
            return def;
        }

        public int GetInt(string key, int def = 0)
        {
            if (Kv.TryGetValue(key, out string v) && int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out int i)) return i;
            return def;
        }

        public bool GetBool(string key, bool def = false)
        {
            if (!Kv.TryGetValue(key, out string v)) return def;
            v = v.ToLowerInvariant();
            return v == "1" || v == "true" || v == "yes" || v == "on";
        }

        public override string ToString()
        {
            return Source + ":" + No + " " + Raw;
        }
    }

    /// <summary>
    /// Tokenizer shared by the cutscene, dialogue and quest script formats.
    /// Syntax: <c>verb arg arg key=value key="quoted value"</c>; '#' and '//' start comments; blank lines are ignored.
    /// </summary>
    public static class ScriptReader
    {
        public static bool TryFloat(string s, out float f)
        {
            return float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out f);
        }

        public static float ParseFloat(string s, float def = 0f)
        {
            return TryFloat(s, out float f) ? f : def;
        }

        public static List<ScriptLine> Parse(string text, string sourceName)
        {
            var result = new List<ScriptLine>();
            if (string.IsNullOrEmpty(text)) return result;
            string[] lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                ScriptLine sl = ParseLine(lines[i], i + 1, sourceName);
                if (sl != null) result.Add(sl);
            }
            return result;
        }

        public static ScriptLine ParseLine(string line, int lineNo, string sourceName)
        {
            if (line == null) return null;
            var tokens = new List<string>();
            var sb = new StringBuilder();
            bool inQuote = false;
            bool hadContent = false;
            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (inQuote)
                {
                    if (c == '\\' && i + 1 < line.Length && (line[i + 1] == '"' || line[i + 1] == '\\'))
                    {
                        sb.Append(line[i + 1]);
                        i++;
                    }
                    else if (c == '"') inQuote = false;
                    else sb.Append(c);
                    continue;
                }
                if (c == '"')
                {
                    inQuote = true;
                    hadContent = true;
                    sb.Append('\u0001'); // marks that this token contained a quote (keeps empty strings)
                    continue;
                }
                if (!hadContent && (c == '#' || (c == '/' && i + 1 < line.Length && line[i + 1] == '/'))) break;
                if (char.IsWhiteSpace(c))
                {
                    if (hadContent)
                    {
                        tokens.Add(sb.ToString());
                        sb.Length = 0;
                        hadContent = false;
                    }
                    continue;
                }
                hadContent = true;
                sb.Append(c);
            }
            if (hadContent) tokens.Add(sb.ToString());
            if (tokens.Count == 0) return null;

            var sl = new ScriptLine { No = lineNo, Source = sourceName, Raw = line.Trim() };
            sl.Verb = Clean(tokens[0]);
            for (int i = 1; i < tokens.Count; i++)
            {
                string tok = tokens[i];
                int eq = FindKvSeparator(tok);
                if (eq > 0)
                {
                    sl.Kv[tok.Substring(0, eq)] = Clean(tok.Substring(eq + 1));
                }
                else sl.Args.Add(Clean(tok));
            }
            return sl;
        }

        static string Clean(string s)
        {
            return s.IndexOf('\u0001') >= 0 ? s.Replace("\u0001", string.Empty) : s;
        }

        static int FindKvSeparator(string tok)
        {
            int eq = tok.IndexOf('=');
            if (eq <= 0) return -1;
            for (int i = 0; i < eq; i++)
            {
                char c = tok[i];
                bool ok = char.IsLetterOrDigit(c) || c == '_' || c == '.' || c == '-';
                if (!ok) return -1;
            }
            return eq;
        }

        /// <summary>Splits "a,b,c" into floats; missing entries use the supplied defaults.</summary>
        public static float[] ParseFloats(string s, params float[] defaults)
        {
            var res = new float[defaults.Length];
            Array.Copy(defaults, res, defaults.Length);
            if (string.IsNullOrEmpty(s)) return res;
            string[] parts = s.Split(',');
            for (int i = 0; i < parts.Length && i < res.Length; i++)
            {
                if (TryFloat(parts[i].Trim(), out float f)) res[i] = f;
            }
            return res;
        }
    }
}
