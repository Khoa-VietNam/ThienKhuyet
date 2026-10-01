using System;
using System.Collections.Generic;
using System.Globalization;
using ThienKhuyet.Core;
using ThienKhuyet.Data;

namespace ThienKhuyet.Narrative
{
    public enum ObjectiveType { Kill, Collect, Reach, Talk, Interact, Flag, Realm, Action, Cutscene }

    public sealed class ObjectiveDef
    {
        public ObjectiveType type;
        public string target = "";
        public int count = 1;
        public string textKey = "";
        public bool consume;
        public string extra = "";
    }

    public sealed class QuestDef
    {
        public string id;
        public string titleKey;
        public string descKey;
        public bool main;
        public int act = 1;
        public string giver = "";
        public bool auto;                 // starts by itself as soon as 'requires' holds (or when started by a script)
        public string requires = "";
        public string next = "";
        public string zone = "";          // tracker hint (poi id for the map marker)
        public readonly List<ObjectiveDef> objectives = new List<ObjectiveDef>();
        public float rewardExp;
        public int rewardStones;
        public readonly List<ItemCost> rewardItems = new List<ItemCost>();
        public string rewardFx = "";
        public string onStart = "";
        public string onDone = "";
        public string rewardRepFaction = "";
        public int rewardRep;
    }

    /// <summary>
    /// Parser for quest scripts. Example:
    /// <code>
    /// quest m1_wake title=quest.m1_wake.title desc=quest.m1_wake.desc type=main act=1 auto=yes next=m1_wolves zone=forest_road
    ///   obj collect item=berry_wild n=2 text=quest.m1_wake.o1
    ///   obj reach poi=forest_road text=quest.m1_wake.o2
    ///   reward exp=30 item=healing_paste*1 stones=5
    ///   on_done "flag+:found_road"
    /// </code>
    /// </summary>
    public static class QuestParser
    {
        public static List<QuestDef> Parse(string text, string source, List<string> errors = null)
        {
            var result = new List<QuestDef>();
            QuestDef cur = null;
            foreach (ScriptLine l in ScriptReader.Parse(text, source))
            {
                switch (l.Verb)
                {
                    case "quest":
                        cur = new QuestDef
                        {
                            id = l.Arg(0),
                            titleKey = l.Get("title", "quest." + l.Arg(0) + ".title"),
                            descKey = l.Get("desc", "quest." + l.Arg(0) + ".desc"),
                            main = l.Get("type", "side") == "main",
                            act = l.GetInt("act", 1),
                            giver = l.Get("giver", ""),
                            auto = l.GetBool("auto"),
                            requires = l.Get("requires", ""),
                            next = l.Get("next", ""),
                            zone = l.Get("zone", "")
                        };
                        if (string.IsNullOrEmpty(cur.id)) { errors?.Add(l + ": quest without id"); cur = null; break; }
                        result.Add(cur);
                        break;
                    case "obj":
                        if (cur == null) { errors?.Add(l + ": obj outside quest"); break; }
                        cur.objectives.Add(ParseObjective(l, cur, errors));
                        break;
                    case "reward":
                        if (cur == null) break;
                        cur.rewardExp = l.GetFloat("exp", 0f);
                        cur.rewardStones = l.GetInt("stones", 0);
                        cur.rewardFx = l.Get("fx", "");
                        string items = l.Get("item", "");
                        if (!string.IsNullOrEmpty(items))
                        {
                            foreach (string part in items.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
                            {
                                int star = part.IndexOf('*');
                                string id = star > 0 ? part.Substring(0, star) : part;
                                int n = 1;
                                if (star > 0) int.TryParse(part.Substring(star + 1), NumberStyles.Integer, CultureInfo.InvariantCulture, out n);
                                cur.rewardItems.Add(new ItemCost(id.Trim(), Math.Max(1, n)));
                            }
                        }
                        string rep = l.Get("rep", "");
                        if (!string.IsNullOrEmpty(rep))
                        {
                            int p = rep.IndexOfAny(new[] { '+', '-' });
                            if (p > 0 && int.TryParse(rep.Substring(p), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int d))
                            {
                                cur.rewardRepFaction = rep.Substring(0, p);
                                cur.rewardRep = d;
                            }
                        }
                        break;
                    case "on_start":
                        if (cur != null) cur.onStart = l.Arg(0, "");
                        break;
                    case "on_done":
                        if (cur != null) cur.onDone = l.Arg(0, "");
                        break;
                    default:
                        errors?.Add(l + ": unknown verb '" + l.Verb + "'");
                        break;
                }
            }
            return result;
        }

        static ObjectiveDef ParseObjective(ScriptLine l, QuestDef q, List<string> errors)
        {
            var o = new ObjectiveDef { textKey = l.Get("text", ""), count = Math.Max(1, l.GetInt("n", 1)), consume = l.GetBool("consume") };
            string kind = l.Arg(0, "");
            switch (kind)
            {
                case "kill": o.type = ObjectiveType.Kill; o.target = l.Get("enemy", ""); break;
                case "collect": o.type = ObjectiveType.Collect; o.target = l.Get("item", ""); break;
                case "reach": o.type = ObjectiveType.Reach; o.target = l.Get("poi", ""); break;
                case "talk": o.type = ObjectiveType.Talk; o.target = l.Get("npc", ""); o.extra = l.Get("dialogue", ""); break;
                case "interact": o.type = ObjectiveType.Interact; o.target = l.Get("id", ""); break;
                case "flag": o.type = ObjectiveType.Flag; o.target = l.Get("name", l.Get("cond", "")); break;
                case "realm": o.type = ObjectiveType.Realm; o.count = Math.Max(0, l.GetInt("n", 1)); break;
                case "action": o.type = ObjectiveType.Action; o.target = l.Get("name", ""); break;
                case "cutscene": o.type = ObjectiveType.Cutscene; o.target = l.Get("id", ""); break;
                default: errors?.Add(l + ": unknown objective '" + kind + "'"); break;
            }
            if (string.IsNullOrEmpty(o.textKey)) o.textKey = "quest." + q.id + ".o" + (q.objectives.Count + 1);
            return o;
        }
    }
}
