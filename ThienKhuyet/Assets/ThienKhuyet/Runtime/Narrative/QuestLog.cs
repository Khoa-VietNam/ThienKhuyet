using System;
using System.Collections.Generic;
using ThienKhuyet.Core;
using ThienKhuyet.Data;
using UnityEngine;

namespace ThienKhuyet.Narrative
{
    [Serializable]
    public sealed class QuestSave
    {
        public string id;
        public string status;
        public int[] counts = new int[0];
    }

    public sealed class QuestProgress
    {
        public QuestDef def;
        public string status = "active";   // active | done | failed
        public int[] counts;
        public bool[] complete;

        public bool AllComplete
        {
            get
            {
                for (int i = 0; i < complete.Length; i++) if (!complete[i]) return false;
                return true;
            }
        }
    }

    /// <summary>
    /// Quest state machine. Objectives are advanced by game events (kills, items, flags, locations, dialogue, actions),
    /// rewards and follow-up quests are applied through the shared script effects so quests, dialogue and cutscenes interlock.
    /// </summary>
    public sealed class QuestLog
    {
        readonly Dictionary<string, QuestDef> defs = new Dictionary<string, QuestDef>();
        readonly Dictionary<string, QuestProgress> states = new Dictionary<string, QuestProgress>();
        readonly List<string> order = new List<string>();
        readonly IGameEffects fx;
        bool subscribed;
        bool evaluating;

        public event Action<string> Changed;
        public string TrackedId { get; set; }

        public QuestLog(IGameEffects fx)
        {
            this.fx = fx;
        }

        public IReadOnlyDictionary<string, QuestDef> Defs => defs;

        public void LoadDefs(IEnumerable<QuestDef> list)
        {
            foreach (QuestDef d in list) defs[d.id] = d;
        }

        public QuestDef Def(string id)
        {
            defs.TryGetValue(id, out QuestDef d);
            return d;
        }

        public void Subscribe()
        {
            if (subscribed) return;
            subscribed = true;
            EventBus.Subscribe<EnemyKilledEvent>(OnKill);
            EventBus.Subscribe<ItemGainedEvent>(OnItem);
            EventBus.Subscribe<PoiDiscoveredEvent>(OnPoi);
            EventBus.Subscribe<InteractEvent>(OnInteract);
            EventBus.Subscribe<DialogueEvent>(OnDialogue);
            EventBus.Subscribe<PlayerActionEvent>(OnAction);
            EventBus.Subscribe<CutsceneEvent>(OnCutscene);
            EventBus.Subscribe<RealmChangedEvent>(OnRealm);
            EventBus.Subscribe<FlagChangedEvent>(OnFlag);
        }

        public void Unsubscribe()
        {
            if (!subscribed) return;
            subscribed = false;
            EventBus.Unsubscribe<EnemyKilledEvent>(OnKill);
            EventBus.Unsubscribe<ItemGainedEvent>(OnItem);
            EventBus.Unsubscribe<PoiDiscoveredEvent>(OnPoi);
            EventBus.Unsubscribe<InteractEvent>(OnInteract);
            EventBus.Unsubscribe<DialogueEvent>(OnDialogue);
            EventBus.Unsubscribe<PlayerActionEvent>(OnAction);
            EventBus.Unsubscribe<CutsceneEvent>(OnCutscene);
            EventBus.Unsubscribe<RealmChangedEvent>(OnRealm);
            EventBus.Unsubscribe<FlagChangedEvent>(OnFlag);
        }

        public void Clear()
        {
            states.Clear();
            order.Clear();
            TrackedId = null;
            Changed?.Invoke(null);
        }

        // ------------------------------------------------------------------ queries
        public string Status(string id)
        {
            return states.TryGetValue(id, out QuestProgress p) ? p.status : "none";
        }

        public QuestProgress Progress(string id)
        {
            states.TryGetValue(id, out QuestProgress p);
            return p;
        }

        public IEnumerable<QuestProgress> Active
        {
            get
            {
                for (int i = 0; i < order.Count; i++)
                    if (states.TryGetValue(order[i], out QuestProgress p) && p.status == "active") yield return p;
            }
        }

        public IEnumerable<QuestProgress> Finished
        {
            get
            {
                for (int i = 0; i < order.Count; i++)
                    if (states.TryGetValue(order[i], out QuestProgress p) && p.status != "active") yield return p;
            }
        }

        public QuestProgress Tracked
        {
            get
            {
                if (!string.IsNullOrEmpty(TrackedId) && states.TryGetValue(TrackedId, out QuestProgress p) && p.status == "active") return p;
                foreach (QuestProgress a in Active) if (a.def.main) return a;
                foreach (QuestProgress a in Active) return a;
                return null;
            }
        }

        /// <summary>Quests whose giver is the NPC and which could be started now (shown with a marker above the NPC).</summary>
        public QuestDef AvailableFrom(string npcId)
        {
            foreach (QuestDef d in defs.Values)
            {
                if (d.giver != npcId || d.auto || Status(d.id) != "none") continue;
                if (!string.IsNullOrEmpty(d.requires) && !Cond.Eval(d.requires, fx)) continue;
                return d;
            }
            return null;
        }

        // ------------------------------------------------------------------ transitions
        public bool Start(string id, bool silent = false)
        {
            if (!defs.TryGetValue(id, out QuestDef d)) { Debug.LogWarning("[QuestLog] unknown quest " + id); return false; }
            if (states.ContainsKey(id)) return false;
            var p = new QuestProgress { def = d, counts = new int[d.objectives.Count], complete = new bool[d.objectives.Count] };
            states[id] = p;
            order.Add(id);
            if (string.IsNullOrEmpty(TrackedId) || d.main) TrackedId = id;
            if (!string.IsNullOrEmpty(d.onStart)) Fx.Apply(d.onStart, fx);
            Evaluate(p);
            if (!silent) fx.Toast("toast.quest_new");
            Changed?.Invoke(id);
            EventBus.Publish(new QuestChangedEvent { questId = id });
            return true;
        }

        public void Complete(string id)
        {
            if (!states.TryGetValue(id, out QuestProgress p) || p.status != "active") return;
            p.status = "done";
            QuestDef d = p.def;
            if (d.rewardExp > 0f) fx.AddExp(d.rewardExp);
            if (d.rewardStones > 0) fx.GiveItem("spirit_stone", d.rewardStones);
            for (int i = 0; i < d.rewardItems.Count; i++) fx.GiveItem(d.rewardItems[i].itemId, d.rewardItems[i].count);
            for (int i = 0; i < d.objectives.Count; i++)
                if (d.objectives[i].type == ObjectiveType.Collect && d.objectives[i].consume) fx.TakeItem(d.objectives[i].target, d.objectives[i].count);
            if (!string.IsNullOrEmpty(d.rewardRepFaction)) fx.AddReputation(d.rewardRepFaction, d.rewardRep);
            if (!string.IsNullOrEmpty(d.rewardFx)) Fx.Apply(d.rewardFx, fx);
            fx.SetFlag("quest." + id + ".done", true);
            if (!string.IsNullOrEmpty(d.onDone)) Fx.Apply(d.onDone, fx);
            if (TrackedId == id) TrackedId = null;
            fx.Toast("toast.quest_done");
            Changed?.Invoke(id);
            EventBus.Publish(new QuestChangedEvent { questId = id });
            if (!string.IsNullOrEmpty(d.next)) Start(d.next);
            StartAutoQuests();
        }

        public void Fail(string id)
        {
            if (!states.TryGetValue(id, out QuestProgress p) || p.status != "active") return;
            p.status = "failed";
            if (TrackedId == id) TrackedId = null;
            Changed?.Invoke(id);
            EventBus.Publish(new QuestChangedEvent { questId = id });
        }

        /// <summary>Starts every 'auto' quest whose requirement now holds.</summary>
        public void StartAutoQuests()
        {
            foreach (QuestDef d in defs.Values)
            {
                if (!d.auto || states.ContainsKey(d.id)) continue;
                if (!string.IsNullOrEmpty(d.requires) && !Cond.Eval(d.requires, fx)) continue;
                if (string.IsNullOrEmpty(d.requires) && !d.main) continue;
                Start(d.id);
            }
        }

        // ------------------------------------------------------------------ events
        void Each(Action<QuestProgress, int, ObjectiveDef> f)
        {
            var list = new List<QuestProgress>();
            foreach (QuestProgress p in Active) list.Add(p);
            for (int q = 0; q < list.Count; q++)
            {
                QuestProgress p = list[q];
                for (int i = 0; i < p.def.objectives.Count; i++)
                {
                    if (p.complete[i]) continue;
                    f(p, i, p.def.objectives[i]);
                }
                Evaluate(p);
            }
        }

        void Bump(QuestProgress p, int i, int by = 1)
        {
            p.counts[i] += by;
            Changed?.Invoke(p.def.id);
        }

        void OnKill(EnemyKilledEvent e)
        {
            Refresh();
        }

        void OnItem(ItemGainedEvent e)
        {
            Refresh();
        }

        void OnPoi(PoiDiscoveredEvent e)
        {
            Each((p, i, o) => { if (o.type == ObjectiveType.Reach && o.target == e.poiId) Bump(p, i); });
        }

        void OnInteract(InteractEvent e)
        {
            Each((p, i, o) => { if (o.type == ObjectiveType.Interact && o.target == e.objectId) Bump(p, i); });
        }

        void OnDialogue(DialogueEvent e)
        {
            if (e.started) return;
            Each((p, i, o) =>
            {
                if (o.type != ObjectiveType.Talk || o.target != e.npcId) return;
                if (!string.IsNullOrEmpty(o.extra) && o.extra != e.dialogueId) return;
                Bump(p, i);
            });
        }

        void OnAction(PlayerActionEvent e)
        {
            Each((p, i, o) => { if (o.type == ObjectiveType.Action && o.target == e.action) Bump(p, i); });
        }

        void OnCutscene(CutsceneEvent e)
        {
            if (e.started) return;
            Each((p, i, o) => { if (o.type == ObjectiveType.Cutscene && o.target == e.id) Bump(p, i); });
        }

        void OnRealm(RealmChangedEvent e)
        {
            Refresh();
            StartAutoQuests();
        }

        void OnFlag(FlagChangedEvent e)
        {
            Refresh();
            StartAutoQuests();
        }

        /// <summary>Re-checks state-based objectives (items, flags, realm). Public so the inventory UI can refresh after changes.</summary>
        public void Refresh()
        {
            var list = new List<QuestProgress>();
            foreach (QuestProgress p in Active) list.Add(p);
            for (int i = 0; i < list.Count; i++) Evaluate(list[i]);
        }

        void Evaluate(QuestProgress p)
        {
            if (evaluating) return;
            evaluating = true;
            try
            {
                bool changed = false;
                for (int i = 0; i < p.def.objectives.Count; i++)
                {
                    ObjectiveDef o = p.def.objectives[i];
                    int count = p.counts[i];
                    bool done;
                    switch (o.type)
                    {
                        case ObjectiveType.Collect:
                            count = Mathf.Min(o.count, fx.ItemCount(o.target));
                            done = count >= o.count;
                            break;
                        case ObjectiveType.Kill:
                            if (Game.Session != null && !string.IsNullOrEmpty(o.target) && Game.Session.enemyKillCounts.TryGetValue(o.target, out int totalKills))
                                count = Mathf.Max(count, Mathf.Min(o.count, totalKills));
                            done = count >= o.count;
                            break;
                        case ObjectiveType.Reach:
                            if (Game.Session != null && !string.IsNullOrEmpty(o.target) && Game.Session.discoveredPois.Contains(o.target))
                                count = Mathf.Max(count, o.count);
                            done = count >= o.count;
                            break;
                        case ObjectiveType.Flag:
                            done = Cond.Eval(o.target, fx);
                            count = done ? 1 : 0;
                            break;
                        case ObjectiveType.Realm:
                            done = fx.RealmIndex >= o.count;
                            count = done ? 1 : 0;
                            break;
                        default:
                            done = count >= o.count;
                            break;
                    }
                    if (p.counts[i] != count) { p.counts[i] = count; changed = true; }
                    if (p.complete[i] != done) { p.complete[i] = done; changed = true; }
                }
                if (changed) Changed?.Invoke(p.def.id);
                if (p.status == "active" && p.def.objectives.Count > 0 && p.AllComplete)
                {
                    evaluating = false;
                    Complete(p.def.id);
                    return;
                }
            }
            finally
            {
                evaluating = false;
            }
        }

        // ------------------------------------------------------------------ save / load
        public List<QuestSave> ToSave()
        {
            var list = new List<QuestSave>();
            for (int i = 0; i < order.Count; i++)
            {
                QuestProgress p = states[order[i]];
                list.Add(new QuestSave { id = p.def.id, status = p.status, counts = (int[])p.counts.Clone() });
            }
            return list;
        }

        public void FromSave(List<QuestSave> list, string tracked)
        {
            states.Clear();
            order.Clear();
            if (list != null)
            {
                for (int i = 0; i < list.Count; i++)
                {
                    if (!defs.TryGetValue(list[i].id, out QuestDef d)) continue;
                    var p = new QuestProgress { def = d, status = list[i].status, counts = new int[d.objectives.Count], complete = new bool[d.objectives.Count] };
                    int[] savedCounts = list[i].counts ?? new int[0];
                    for (int k = 0; k < p.counts.Length && k < savedCounts.Length; k++) p.counts[k] = Mathf.Max(0, savedCounts[k]);
                    for (int k = 0; k < p.counts.Length; k++) p.complete[k] = p.counts[k] >= d.objectives[k].count;
                    states[d.id] = p;
                    order.Add(d.id);
                }
            }
            TrackedId = tracked;
            Changed?.Invoke(null);
        }
    }
}
