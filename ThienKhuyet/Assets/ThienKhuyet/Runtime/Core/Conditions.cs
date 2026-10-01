using System;
using System.Collections.Generic;
using System.Globalization;

namespace ThienKhuyet.Core
{
    /// <summary>Read-only view of the game state used by dialogue/quest/cutscene conditions (mockable in unit tests).</summary>
    public interface IGameQuery
    {
        bool HasFlag(string flag);
        int GetInt(string key);
        string GetStr(string key);
        int RealmIndex { get; }
        int RealmStage { get; }
        int ItemCount(string itemId);
        /// <summary>One of: none, active, done, failed.</summary>
        string QuestStatus(string questId);
        int Reputation(string factionId);
        int Act { get; }
        bool IsNight { get; }
        string BuildName { get; }
    }

    /// <summary>Mutating operations that script effects may trigger (implemented by the game, mocked in tests).</summary>
    public interface IGameEffects : IGameQuery
    {
        void SetFlag(string flag, bool value);
        void SetInt(string key, int value);
        void SetStr(string key, string value);
        void StartQuest(string questId);
        void CompleteQuest(string questId);
        void FailQuest(string questId);
        void GiveItem(string itemId, int count);
        void TakeItem(string itemId, int count);
        void AddReputation(string factionId, int delta);
        void AddExp(float amount);
        void PlayCutscene(string cutsceneId);
        void HealPlayer();
        void SetAct(int act);
        void LearnSkill(string skillId);
        void Teleport(string anchorId);
        void EndGame(string endingId);
        void Toast(string textKey);
        void Unlock(string id);
        void ShowTitle(string textKey);
        void SaveGame();
    }

    /// <summary>
    /// Condition expressions: clauses joined with '&amp;'. Each clause may start with '!'.
    /// Examples: <c>flag:met_elder</c>, <c>!flag:asked</c>, <c>realm&gt;=1</c>, <c>item:herb_basic&gt;=3</c>,
    /// <c>quest:main_01=done</c>, <c>rep:village&gt;=10</c>, <c>int:wolves_killed&gt;=3</c>, <c>str:path=sword</c>, <c>night</c>.
    /// </summary>
    public static class Cond
    {
        public static bool Eval(string expr, IGameQuery q)
        {
            if (string.IsNullOrWhiteSpace(expr)) return true;
            string[] clauses = expr.Split(new[] { '&' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < clauses.Length; i++)
            {
                if (!EvalClause(clauses[i].Trim(), q)) return false;
            }
            return true;
        }

        static bool EvalClause(string c, IGameQuery q)
        {
            if (c.Length == 0) return true;
            bool neg = false;
            if (c[0] == '!')
            {
                neg = true;
                c = c.Substring(1).Trim();
            }
            return EvalAtom(c, q) != neg;
        }

        static bool EvalAtom(string c, IGameQuery q)
        {
            if (c == "night") return q.IsNight;
            if (c == "day") return !q.IsNight;
            if (c == "true" || c == "always") return true;
            if (c == "false" || c == "never") return false;

            int colon = c.IndexOf(':');
            if (colon > 0)
            {
                string head = c.Substring(0, colon);
                string rest = c.Substring(colon + 1);
                switch (head)
                {
                    case "flag": return q.HasFlag(rest.Trim());
                    case "item": return CompareNamed(rest, q.ItemCount, 1);
                    case "int": return CompareNamed(rest, q.GetInt, 1);
                    case "rep": return CompareNamed(rest, q.Reputation, 1);
                    case "quest":
                        {
                            int eq = rest.IndexOf('=');
                            if (eq < 0) return q.QuestStatus(rest.Trim()) == "done";
                            return q.QuestStatus(rest.Substring(0, eq).Trim()) == rest.Substring(eq + 1).Trim();
                        }
                    case "str":
                        {
                            int eq = rest.IndexOf('=');
                            if (eq < 0) return !string.IsNullOrEmpty(q.GetStr(rest.Trim()));
                            return q.GetStr(rest.Substring(0, eq).Trim()) == rest.Substring(eq + 1).Trim();
                        }
                    case "build": return string.Equals(q.BuildName, rest.Trim(), StringComparison.OrdinalIgnoreCase);
                }
            }

            if (StartsWithWord(c, "realm")) return Compare(q.RealmIndex, c.Substring(5));
            if (StartsWithWord(c, "stage")) return Compare(q.RealmStage, c.Substring(5));
            if (StartsWithWord(c, "act")) return Compare(q.Act, c.Substring(3));
            // bare word == flag name
            return q.HasFlag(c);
        }

        static bool StartsWithWord(string s, string w)
        {
            if (!s.StartsWith(w, StringComparison.Ordinal) || s.Length == w.Length) return false;
            char n = s[w.Length];
            return n == '>' || n == '<' || n == '=' || n == '!';
        }

        static bool CompareNamed(string rest, Func<string, int> getter, int defaultMin)
        {
            int op = FindOp(rest, out string opStr);
            if (op < 0) return getter(rest.Trim()) >= defaultMin;
            string name = rest.Substring(0, op).Trim();
            return Compare(getter(name), rest.Substring(op));
        }

        /// <summary>Compares 'value' against an expression tail like "&gt;=3".</summary>
        public static bool Compare(int value, string tail)
        {
            tail = tail.Trim();
            int op = FindOp(tail, out string opStr);
            if (op != 0) return false;
            string num = tail.Substring(opStr.Length).Trim();
            if (!int.TryParse(num, NumberStyles.Integer, CultureInfo.InvariantCulture, out int n)) return false;
            switch (opStr)
            {
                case ">=": return value >= n;
                case "<=": return value <= n;
                case "!=": return value != n;
                case "==":
                case "=": return value == n;
                case ">": return value > n;
                case "<": return value < n;
            }
            return false;
        }

        static int FindOp(string s, out string op)
        {
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (c == '>' || c == '<' || c == '=' || c == '!')
                {
                    if (i + 1 < s.Length && s[i + 1] == '=')
                    {
                        op = s.Substring(i, 2);
                        return i;
                    }
                    if (c == '!')
                    {
                        continue;
                    }
                    op = c.ToString();
                    return i;
                }
            }
            op = null;
            return -1;
        }
    }

    /// <summary>
    /// Effect expressions separated by ';'. Supported:
    /// flag+:x, flag-:x, int:k=5, int:k+=1, str:k=v, quest.start:id, quest.done:id, quest.fail:id, give:item*3, take:item*2,
    /// rep:faction+5, exp:100, cut:id, heal, act:2, learn:skill, tp:anchor, end:ending, toast:key, unlock:id, title:key, save.
    /// </summary>
    public static class Fx
    {
        public static int Apply(string expr, IGameEffects fx)
        {
            if (string.IsNullOrWhiteSpace(expr)) return 0;
            string[] parts = expr.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
            int applied = 0;
            for (int i = 0; i < parts.Length; i++)
            {
                if (ApplyOne(parts[i].Trim(), fx)) applied++;
            }
            return applied;
        }

        static bool ApplyOne(string e, IGameEffects fx)
        {
            if (e.Length == 0) return false;
            if (e == "heal") { fx.HealPlayer(); return true; }
            if (e == "save") { fx.SaveGame(); return true; }
            int colon = e.IndexOf(':');
            if (colon <= 0) return false;
            string head = e.Substring(0, colon);
            string arg = e.Substring(colon + 1).Trim();
            switch (head)
            {
                case "flag+": fx.SetFlag(arg, true); return true;
                case "flag-": fx.SetFlag(arg, false); return true;
                case "int":
                    {
                        int add = arg.IndexOf("+=", StringComparison.Ordinal);
                        if (add > 0)
                        {
                            string k = arg.Substring(0, add).Trim();
                            if (int.TryParse(arg.Substring(add + 2), NumberStyles.Integer, CultureInfo.InvariantCulture, out int d)) { fx.SetInt(k, fx.GetInt(k) + d); return true; }
                            return false;
                        }
                        int eq = arg.IndexOf('=');
                        if (eq > 0 && int.TryParse(arg.Substring(eq + 1), NumberStyles.Integer, CultureInfo.InvariantCulture, out int v)) { fx.SetInt(arg.Substring(0, eq).Trim(), v); return true; }
                        return false;
                    }
                case "str":
                    {
                        int eq = arg.IndexOf('=');
                        if (eq <= 0) return false;
                        fx.SetStr(arg.Substring(0, eq).Trim(), arg.Substring(eq + 1).Trim());
                        return true;
                    }
                case "quest.start": fx.StartQuest(arg); return true;
                case "quest.done": fx.CompleteQuest(arg); return true;
                case "quest.fail": fx.FailQuest(arg); return true;
                case "give": { SplitCount(arg, out string id, out int n); fx.GiveItem(id, n); return true; }
                case "take": { SplitCount(arg, out string id, out int n); fx.TakeItem(id, n); return true; }
                case "rep":
                    {
                        int p = arg.IndexOfAny(new[] { '+', '-' });
                        if (p <= 0) return false;
                        if (!int.TryParse(arg.Substring(p), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int d)) return false;
                        fx.AddReputation(arg.Substring(0, p).Trim(), d);
                        return true;
                    }
                case "exp":
                    if (ScriptReader.TryFloat(arg, out float xp)) { fx.AddExp(xp); return true; }
                    return false;
                case "cut": fx.PlayCutscene(arg); return true;
                case "act":
                    if (int.TryParse(arg, NumberStyles.Integer, CultureInfo.InvariantCulture, out int act)) { fx.SetAct(act); return true; }
                    return false;
                case "learn": fx.LearnSkill(arg); return true;
                case "tp": fx.Teleport(arg); return true;
                case "end": fx.EndGame(arg); return true;
                case "toast": fx.Toast(arg); return true;
                case "unlock": fx.Unlock(arg); return true;
                case "title": fx.ShowTitle(arg); return true;
            }
            return false;
        }

        static void SplitCount(string arg, out string id, out int count)
        {
            int star = arg.IndexOf('*');
            count = 1;
            if (star > 0)
            {
                id = arg.Substring(0, star).Trim();
                int.TryParse(arg.Substring(star + 1), NumberStyles.Integer, CultureInfo.InvariantCulture, out count);
            }
            else id = arg.Trim();
            if (count < 1) count = 1;
        }
    }
}
