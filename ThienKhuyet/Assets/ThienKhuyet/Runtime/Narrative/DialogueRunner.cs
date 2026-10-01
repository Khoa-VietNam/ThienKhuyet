using System;
using System.Collections.Generic;
using ThienKhuyet.Core;
using ThienKhuyet.Data;
using ThienKhuyet.Player;
using ThienKhuyet.UI;
using UnityEngine;

namespace ThienKhuyet.Narrative
{
    /// <summary>Executes a parsed dialogue graph against the live game state and the runtime dialogue view.</summary>
    public sealed class DialogueRunner
    {
        DialogueGraph graph;
        DialogueNode current;
        NpcController npc;
        bool active;
        bool suspended;
        bool entryEffectsApplied;
        bool choicesActive;
        bool hasPendingTarget;
        string pendingTarget;
        int transitionGuard;
        readonly List<int> visibleChoiceIndices = new List<int>();

        public bool Active => active;
        public string CurrentId => graph != null ? graph.id : string.Empty;

        public bool Begin(string id, NpcController speaker)
        {
            if (active) End(false);
            DialogueGraph loaded = DialogueLibrary.Get(id);
            if (loaded == null)
            {
                Debug.LogWarning("[DialogueRunner] Dialogue not found: " + id);
                Game.UI?.Toast("toast.dialogue_missing");
                return false;
            }
            graph = loaded;
            npc = speaker;
            active = true;
            suspended = false;
            transitionGuard = 0;
            if (npc != null) npc.busy = true;
            if (Game.Input != null) Game.Input.PushLock("dialogue");
            Game.Mode = GameMode.Dialogue;
            EventBus.Publish(new DialogueEvent { npcId = npc != null && npc.def != null ? npc.def.id : string.Empty, dialogueId = id, started = true });
            Jump(graph.startId);
            return true;
        }

        public void Tick()
        {
            if (!active || suspended || Game.UI == null || Game.Input == null) return;
            int selected = Game.UI.ChosenDialogueChoice;
            if (selected >= 0)
            {
                ResolveChoice(selected);
                return;
            }

            if (Game.Input.AdvancePressed || Game.UI.ConsumeDialogueClick())
            {
                if (Game.UI.DialogueIsTyping) Game.UI.CompleteDialogueLine();
                else if (!Game.UI.HasDialogueChoices) Advance();
            }
        }

        void Jump(string id)
        {
            if (!active) return;
            if (string.IsNullOrEmpty(id) || id == "end") { End(true); return; }
            if (++transitionGuard > 64)
            {
                Debug.LogError("[DialogueRunner] Too many automatic transitions in " + graph.id + ". Check branch/next cycles.");
                End(true);
                return;
            }
            if (!graph.nodes.TryGetValue(id, out current))
            {
                Debug.LogError("[DialogueRunner] Missing node '" + id + "' in " + graph.id + ".");
                End(true);
                return;
            }
            entryEffectsApplied = false;
            choicesActive = false;
            if (!string.IsNullOrEmpty(current.condition) && !Cond.Eval(current.condition, Game.Manager))
            {
                Jump(current.next);
                return;
            }
            if (current.branches.Count > 0 && current.IsBranch)
            {
                string target = "end";
                for (int i = 0; i < current.branches.Count; i++)
                {
                    BranchRule branch = current.branches[i];
                    if (string.IsNullOrEmpty(branch.condition) || Cond.Eval(branch.condition, Game.Manager)) { target = branch.target; break; }
                }
                Jump(target);
                return;
            }
            ShowCurrent();
        }

        void ShowCurrent()
        {
            if (!active || current == null || Game.UI == null) return;
            string speakerName;
            Color speakerColor;
            bool narration = current.narration || current.speaker == "narrator";
            ResolveSpeaker(current.speaker, out speakerName, out speakerColor);
            string text = string.IsNullOrEmpty(current.textKey) ? string.Empty : Loc.TOrSelf(current.textKey);
            Game.UI.BeginDialogueLine(speakerName, text, speakerColor, narration);
            if (npc != null && npc.anim != null) npc.anim.SetSpeaking(!narration);
            HumanoidAnimator actor = FindAnimator(current.speaker);
            if (actor != null)
            {
                if (!string.IsNullOrEmpty(current.expression)) actor.SetExpression(current.expression);
                if (!string.IsNullOrEmpty(current.animation)) actor.PlayAction(current.animation, 1f);
            }
            if (!entryEffectsApplied && !string.IsNullOrEmpty(current.effects))
            {
                entryEffectsApplied = true;
                Fx.Apply(current.effects, Game.Manager);
                if (Game.Mode == GameMode.Cutscene) return;
            }
            if (current.choices.Count > 0) BuildChoices();
            else Game.UI.HideDialogueChoices();
            transitionGuard = 0;
        }

        void BuildChoices()
        {
            visibleChoiceIndices.Clear();
            var texts = new List<string>();
            var enabled = new List<bool>();
            for (int i = 0; i < current.choices.Count && texts.Count < 4; i++)
            {
                DialogueChoice choice = current.choices[i];
                bool isEnabled = string.IsNullOrEmpty(choice.condition) || Cond.Eval(choice.condition, Game.Manager);
                if (choice.once && Game.Manager.HasFlag("dialogue.once." + choice.id)) isEnabled = false;
                visibleChoiceIndices.Add(i);
                texts.Add(Loc.TOrSelf(choice.textKey));
                enabled.Add(isEnabled);
            }
            Game.UI.ShowDialogueChoices(texts, enabled);
            bool any = false;
            for (int i = 0; i < enabled.Count; i++) any |= enabled[i];
            choicesActive = any;
            if (!any) Game.UI.HideDialogueChoices();
        }

        void ResolveChoice(int visibleIndex)
        {
            if (current == null || visibleIndex < 0 || visibleIndex >= visibleChoiceIndices.Count) return;
            int choiceIndex = visibleChoiceIndices[visibleIndex];
            DialogueChoice choice = current.choices[choiceIndex];
            if (!string.IsNullOrEmpty(choice.condition) && !Cond.Eval(choice.condition, Game.Manager)) { ShowCurrent(); return; }
            if (choice.once && Game.Manager.HasFlag("dialogue.once." + choice.id)) { ShowCurrent(); return; }
            if (choice.once) Game.Manager.SetFlag("dialogue.once." + choice.id, true);
            Fx.Apply(choice.effects, Game.Manager);
            EventBus.Publish(new ChoiceMadeEvent { key = "dialogue." + graph.id + "." + current.id, value = choice.id });
            Game.UI.HideDialogueChoices();
            choicesActive = false;
            if (npc != null && npc.anim != null) npc.anim.SetSpeaking(false);
            transitionGuard = 0;
            if (Game.Mode == GameMode.Cutscene)
            {
                hasPendingTarget = true;
                pendingTarget = choice.target;
                return;
            }
            Jump(choice.target);
        }

        void Advance()
        {
            if (current == null) { End(true); return; }
            if (choicesActive) return;
            if (npc != null && npc.anim != null) npc.anim.SetSpeaking(false);
            string next = current.next;
            if (current.branches.Count > 0 && !current.IsBranch)
            {
                for (int i = 0; i < current.branches.Count; i++)
                {
                    BranchRule branch = current.branches[i];
                    if (string.IsNullOrEmpty(branch.condition) || Cond.Eval(branch.condition, Game.Manager)) { next = branch.target; break; }
                }
            }
            transitionGuard = 0;
            Jump(next);
        }

        void ResolveSpeaker(string id, out string displayName, out Color color)
        {
            color = UIKit.Paper;
            if (id == "player")
            {
                displayName = Loc.T("ui.player");
                color = new Color(0.65f, 0.86f, 1f);
                return;
            }
            if (id == "narrator")
            {
                displayName = Loc.T("ui.narrator");
                color = new Color(0.82f, 0.84f, 0.88f);
                return;
            }
            NpcDef def = NpcLibrary.Get(id);
            if (def != null)
            {
                displayName = Loc.T(def.NameKey);
                color = def.look.primary;
                return;
            }
            displayName = Loc.TOrSelf("npc." + id + ".name");
        }

        HumanoidAnimator FindAnimator(string id)
        {
            if (id == "player") return Game.Player != null ? Game.Player.anim : null;
            NpcController actor = NpcController.Find(id);
            return actor != null ? actor.anim : null;
        }

        public void SuspendForCutscene()
        {
            if (!active || suspended) return;
            suspended = true;
            Game.UI?.HideDialogue();
            Game.UI?.HideDialogueChoices();
            if (npc != null && npc.anim != null) npc.anim.SetSpeaking(false);
        }

        public void ResumeAfterCutscene()
        {
            if (!active || !suspended) return;
            suspended = false;
            if (hasPendingTarget)
            {
                string target = pendingTarget;
                hasPendingTarget = false;
                pendingTarget = null;
                Jump(target);
            }
            else ShowCurrent();
        }

        public void End(bool publish)
        {
            if (!active) return;
            string id = graph != null ? graph.id : string.Empty;
            string npcId = npc != null && npc.def != null ? npc.def.id : string.Empty;
            active = false;
            suspended = false;
            if (npc != null)
            {
                npc.busy = false;
                if (npc.anim != null) npc.anim.SetSpeaking(false);
            }
            Game.UI?.HideDialogue();
            Game.UI?.HideDialogueChoices();
            if (Game.Input != null) Game.Input.PopLock("dialogue");
            if (Game.Mode == GameMode.Dialogue) Game.Mode = GameMode.Playing;
            if (publish) EventBus.Publish(new DialogueEvent { npcId = npcId, dialogueId = id, started = false });
            graph = null;
            current = null;
            npc = null;
            hasPendingTarget = false;
            pendingTarget = null;
            visibleChoiceIndices.Clear();
        }
    }
}
