using System;
using System.Collections.Generic;
using ThienKhuyet.Core;
using UnityEngine;

namespace ThienKhuyet.Narrative
{
    public sealed class DialogueChoice
    {
        public string textKey;
        public string target = "end";
        public string condition = "";
        public string effects = "";
        public bool once;
        public string id;       // unique key for 'once' tracking
    }

    public sealed class BranchRule
    {
        public string condition;
        public string target;
    }

    public sealed class DialogueNode
    {
        public string id;
        public string speaker = "narrator";
        public string textKey = "";
        public string expression = "";
        public string animation = "";
        public string next = "";
        public string condition = "";
        public string effects = "";
        public bool narration;
        public readonly List<DialogueChoice> choices = new List<DialogueChoice>();
        public readonly List<BranchRule> branches = new List<BranchRule>();
        public bool IsBranch => branches.Count > 0 && string.IsNullOrEmpty(textKey);
    }

    public sealed class DialogueGraph
    {
        public string id;
        public string startId;
        public readonly Dictionary<string, DialogueNode> nodes = new Dictionary<string, DialogueNode>();
        public readonly List<DialogueNode> order = new List<DialogueNode>();
    }

    /// <summary>
    /// Parser for dialogue scripts.
    /// <code>
    /// dialogue elder_first
    ///   node start speaker=elder_vu text=dlg.elder_first.1 expr=smile next=n2
    ///   node n2 speaker=player text=dlg.elder_first.2
    ///     choice text=dlg.elder_first.c1 goto=n3 if="flag:x" do="flag+:y"
    ///     choice text=dlg.elder_first.c2 goto=end
    ///   node n3 branch="realm&gt;=1-&gt;n4;else-&gt;n5"
    /// </code>
    /// </summary>
    public static class DialogueParser
    {
        public static List<DialogueGraph> Parse(string text, string source, List<string> errors = null)
        {
            var graphs = new List<DialogueGraph>();
            DialogueGraph g = null;
            DialogueNode n = null;
            foreach (ScriptLine l in ScriptReader.Parse(text, source))
            {
                switch (l.Verb)
                {
                    case "dialogue":
                        g = new DialogueGraph { id = l.Arg(0) };
                        graphs.Add(g);
                        n = null;
                        break;
                    case "node":
                        if (g == null) { errors?.Add(l + ": node outside dialogue"); break; }
                        n = new DialogueNode
                        {
                            id = l.Arg(0),
                            speaker = l.Get("speaker", "narrator"),
                            textKey = l.Get("text", ""),
                            expression = l.Get("expr", ""),
                            animation = l.Get("anim", ""),
                            next = l.Get("next", ""),
                            condition = l.Get("if", ""),
                            effects = l.Get("do", ""),
                            narration = l.GetBool("narration")
                        };
                        string branch = l.Get("branch", "");
                        if (!string.IsNullOrEmpty(branch))
                        {
                            foreach (string part in branch.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
                            {
                                int arrow = part.LastIndexOf("->", StringComparison.Ordinal);
                                if (arrow < 0) { errors?.Add(l + ": bad branch '" + part + "'"); continue; }
                                string cond = part.Substring(0, arrow).Trim();
                                n.branches.Add(new BranchRule { condition = cond == "else" ? "" : cond, target = part.Substring(arrow + 2).Trim() });
                            }
                        }
                        if (g.nodes.ContainsKey(n.id)) errors?.Add(l + ": duplicate node id " + n.id);
                        g.nodes[n.id] = n;
                        g.order.Add(n);
                        if (string.IsNullOrEmpty(g.startId)) g.startId = n.id;
                        break;
                    case "choice":
                        if (n == null) { errors?.Add(l + ": choice outside node"); break; }
                        n.choices.Add(new DialogueChoice
                        {
                            textKey = l.Get("text", ""),
                            target = l.Get("goto", "end"),
                            condition = l.Get("if", ""),
                            effects = l.Get("do", ""),
                            once = l.GetBool("once"),
                            id = g.id + "/" + n.id + "/" + n.choices.Count
                        });
                        break;
                    default:
                        errors?.Add(l + ": unknown verb '" + l.Verb + "'");
                        break;
                }
            }
            return graphs;
        }

        /// <summary>Static checks: dangling targets, unreachable nodes, missing text keys.</summary>
        public static void Validate(DialogueGraph g, Func<string, bool> hasKey, List<string> problems)
        {
            if (string.IsNullOrEmpty(g.startId)) { problems.Add(g.id + ": empty dialogue"); return; }
            var reach = new HashSet<string>();
            var stack = new Stack<string>();
            stack.Push(g.startId);
            while (stack.Count > 0)
            {
                string id = stack.Pop();
                if (id == "end" || string.IsNullOrEmpty(id) || !reach.Add(id)) continue;
                if (!g.nodes.TryGetValue(id, out DialogueNode node)) { problems.Add(g.id + ": missing node '" + id + "'"); continue; }
                if (!string.IsNullOrEmpty(node.next)) stack.Push(node.next);
                for (int i = 0; i < node.choices.Count; i++) stack.Push(node.choices[i].target);
                for (int i = 0; i < node.branches.Count; i++) stack.Push(node.branches[i].target);
                if (!string.IsNullOrEmpty(node.textKey) && !hasKey(node.textKey)) problems.Add(g.id + "/" + node.id + ": missing text key " + node.textKey);
                for (int i = 0; i < node.choices.Count; i++)
                    if (!hasKey(node.choices[i].textKey)) problems.Add(g.id + "/" + node.id + ": missing choice key " + node.choices[i].textKey);
            }
            foreach (DialogueNode n in g.order) if (!reach.Contains(n.id)) problems.Add(g.id + ": node '" + n.id + "' is unreachable");
        }
    }

    /// <summary>All dialogue graphs, loaded from Resources/Story/Dialogue.</summary>
    public static class DialogueLibrary
    {
        static readonly Dictionary<string, DialogueGraph> graphs = new Dictionary<string, DialogueGraph>();
        static bool loaded;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            graphs.Clear();
            loaded = false;
        }

        public static void Load()
        {
            if (loaded) return;
            loaded = true;
            var errors = new List<string>();
            foreach (TextAsset ta in Resources.LoadAll<TextAsset>("Story/Dialogue"))
                foreach (DialogueGraph g in DialogueParser.Parse(ta.text, ta.name, errors)) graphs[g.id] = g;
            for (int i = 0; i < errors.Count; i++) Debug.LogWarning("[Dialogue] " + errors[i]);
        }

        public static void Register(DialogueGraph g)
        {
            graphs[g.id] = g;
        }

        public static DialogueGraph Get(string id)
        {
            Load();
            graphs.TryGetValue(id, out DialogueGraph g);
            return g;
        }

        public static IEnumerable<DialogueGraph> All
        {
            get
            {
                Load();
                return graphs.Values;
            }
        }
    }
}
