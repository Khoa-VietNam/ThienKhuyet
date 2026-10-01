using System;
using System.Collections.Generic;
using NUnit.Framework;
using ThienKhuyet.Cinematics;
using ThienKhuyet.Core;
using ThienKhuyet.Data;
using ThienKhuyet.Narrative;
using ThienKhuyet.World;
using UnityEngine;

namespace ThienKhuyet.Tests
{
    public sealed class StoryResourceTests
    {
        [SetUp]
        public void SetUp()
        {
            ContentDB.Load();
            Loc.Load("vi");
        }

        [Test]
        public void VietnameseAndEnglishLocalizationHaveTheSameUniqueKeys()
        {
            Dictionary<string, string> vi = ReadLocale("vi");
            Dictionary<string, string> en = ReadLocale("en");
            CollectionAssert.AreEquivalent(vi.Keys, en.Keys, "Every localized key needs a Vietnamese and English value.");
            Assert.AreEqual(vi.Count, new HashSet<string>(vi.Keys).Count);
            Assert.AreEqual(en.Count, new HashSet<string>(en.Keys).Count);
            Assert.IsTrue(vi.ContainsKey("toast.final_path"));
            Assert.IsTrue(en.ContainsKey("toast.final_path"));
        }

        [Test]
        public void DialogueQuestAndCutsceneScriptsParseAndResolveTheirReferences()
        {
            var errors = new List<string>();
            var dialogues = new Dictionary<string, DialogueGraph>(StringComparer.Ordinal);
            foreach (TextAsset asset in Resources.LoadAll<TextAsset>("Story/Dialogue"))
            {
                foreach (DialogueGraph graph in DialogueParser.Parse(asset.text, asset.name, errors))
                {
                    Assert.IsFalse(dialogues.ContainsKey(graph.id), "Duplicate dialogue graph: " + graph.id);
                    dialogues.Add(graph.id, graph);
                    DialogueParser.Validate(graph, Loc.Has, errors);
                    ValidateDialogueEffects(graph, errors);
                }
            }
            Assert.IsNotEmpty(dialogues, "No dialogue scripts were found under Resources/Story/Dialogue.");

            var quests = new Dictionary<string, QuestDef>(StringComparer.Ordinal);
            foreach (TextAsset asset in Resources.LoadAll<TextAsset>("Story/Quests"))
            {
                foreach (QuestDef quest in QuestParser.Parse(asset.text, asset.name, errors))
                {
                    Assert.IsFalse(quests.ContainsKey(quest.id), "Duplicate quest id: " + quest.id);
                    quests.Add(quest.id, quest);
                }
            }
            Assert.IsNotEmpty(quests, "No quest scripts were found under Resources/Story/Quests.");
            ValidateQuests(quests, dialogues, errors);

            foreach (TextAsset asset in Resources.LoadAll<TextAsset>("Story/Cutscenes"))
            {
                CutsceneDefinition cutscene = CutsceneParser.Parse(asset.text, asset.name, errors);
                try
                {
                    Assert.IsTrue(Loc.Has(cutscene.titleKey), cutscene.id + " has missing title key " + cutscene.titleKey);
                    ValidateCutsceneText(cutscene, errors);
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(cutscene);
                }
            }

            Assert.IsEmpty(errors, string.Join("\n", errors));
        }

        [Test]
        public void ActOneQuestSequenceStartsCombatAndLocksTheVeinBehindTheBandits()
        {
            var errors = new List<string>();
            var defs = new List<QuestDef>();
            foreach (TextAsset asset in Resources.LoadAll<TextAsset>("Story/Quests"))
                defs.AddRange(QuestParser.Parse(asset.text, asset.name, errors));
            Assert.IsEmpty(errors, string.Join("\n", errors));

            var game = new FakeGame();
            var log = new QuestLog(game);
            log.LoadDefs(defs);
            game.Flags.Set("path_chosen");
            log.StartAutoQuests();
            Assert.AreEqual("active", log.Status("m1_train"));
            Assert.AreEqual("none", log.Status("m1_wolves"), "Wolves must wait for the training objective.");

            log.Complete("m1_train");
            Assert.AreEqual("done", log.Status("m1_train"));
            Assert.AreEqual("active", log.Status("m1_wolves"), "Training should unlock the wolf quest.");
            Assert.AreEqual("none", log.Status("m1_altar"), "The spirit vein should remain locked before Blackwind Camp is cleared.");

            game.Quests["m1_bandits"] = "done";
            game.Realm = 1;
            log.StartAutoQuests();
            Assert.AreEqual("active", log.Status("m1_altar"), "The vein quest should activate after the bandits and a breakthrough.");
        }

        static Dictionary<string, string> ReadLocale(string language)
        {
            var table = new Dictionary<string, string>(StringComparer.Ordinal);
            TextAsset[] assets = Resources.LoadAll<TextAsset>("Localization/" + language);
            Assert.IsNotEmpty(assets, "No localization TextAsset for language " + language);
            for (int i = 0; i < assets.Length; i++)
            {
                var perAsset = new Dictionary<string, string>(StringComparer.Ordinal);
                int parsedEntries = Loc.ParseTable(assets[i].text, perAsset);
                Assert.AreEqual(parsedEntries, perAsset.Count, "Duplicate keys in " + assets[i].name + " (" + language + ").");
                foreach (KeyValuePair<string, string> entry in perAsset)
                {
                    Assert.IsFalse(table.ContainsKey(entry.Key), "Duplicate localization key across " + language + " assets: " + entry.Key);
                    table.Add(entry.Key, entry.Value);
                }
            }
            return table;
        }

        static void ValidateQuests(Dictionary<string, QuestDef> quests, Dictionary<string, DialogueGraph> dialogues, List<string> errors)
        {
            foreach (QuestDef quest in quests.Values)
            {
                RequireKey(quest.titleKey, quest.id + " title", errors);
                RequireKey(quest.descKey, quest.id + " description", errors);
                if (!string.IsNullOrEmpty(quest.giver) && NpcLibrary.Get(quest.giver) == null)
                    errors.Add(quest.id + ": unknown giver NPC " + quest.giver);
                if (!string.IsNullOrEmpty(quest.next) && !quests.ContainsKey(quest.next))
                    errors.Add(quest.id + ": missing next quest " + quest.next);
                ValidateEffects(quest.onStart, quest.id + " on_start", errors);
                ValidateEffects(quest.onDone, quest.id + " on_done", errors);
                ValidateEffects(quest.rewardFx, quest.id + " reward fx", errors);
                for (int i = 0; i < quest.objectives.Count; i++)
                {
                    ObjectiveDef objective = quest.objectives[i];
                    RequireKey(objective.textKey, quest.id + " objective " + (i + 1), errors);
                    switch (objective.type)
                    {
                        case ObjectiveType.Kill:
                            if (ContentDB.Enemy(objective.target) == null) errors.Add(quest.id + ": unknown enemy " + objective.target);
                            break;
                        case ObjectiveType.Collect:
                            if (ContentDB.Item(objective.target) == null) errors.Add(quest.id + ": unknown item " + objective.target);
                            break;
                        case ObjectiveType.Reach:
                            if (WorldLayout.Poi(objective.target) == null) errors.Add(quest.id + ": unknown POI " + objective.target);
                            break;
                        case ObjectiveType.Talk:
                            if (NpcLibrary.Get(objective.target) == null) errors.Add(quest.id + ": unknown NPC " + objective.target);
                            if (!string.IsNullOrEmpty(objective.extra) && !dialogues.ContainsKey(objective.extra)) errors.Add(quest.id + ": unknown dialogue " + objective.extra);
                            break;
                    }
                }
                for (int i = 0; i < quest.rewardItems.Count; i++)
                    if (ContentDB.Item(quest.rewardItems[i].itemId) == null) errors.Add(quest.id + ": unknown reward item " + quest.rewardItems[i].itemId);
            }
        }

        static void ValidateDialogueEffects(DialogueGraph graph, List<string> errors)
        {
            foreach (DialogueNode node in graph.order)
            {
                ValidateEffects(node.effects, graph.id + "/" + node.id, errors);
                foreach (DialogueChoice choice in node.choices) ValidateEffects(choice.effects, graph.id + "/" + node.id + " choice", errors);
            }
        }

        static void ValidateCutsceneText(CutsceneDefinition cutscene, List<string> errors)
        {
            for (int i = 0; i < cutscene.beats.Count; i++)
            {
                CutsceneBeat beat = cutscene.beats[i];
                if ((beat.command == CutsceneCommand.Subtitle || beat.command == CutsceneCommand.Dialogue) && !string.IsNullOrEmpty(beat.text))
                    RequireKey(beat.text, cutscene.id + " subtitle", errors);
                if (beat.command == CutsceneCommand.Choice)
                    for (int k = 0; k < beat.options.Length; k++) RequireKey(beat.options[k], cutscene.id + " choice", errors);
            }
        }

        static void ValidateEffects(string expression, string owner, List<string> errors)
        {
            if (string.IsNullOrWhiteSpace(expression)) return;
            string[] commands = expression.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
            int applied = Fx.Apply(expression, new FakeGame());
            if (applied != commands.Length) errors.Add(owner + ": unsupported effect syntax in '" + expression + "'");
        }

        static void RequireKey(string key, string owner, List<string> errors)
        {
            if (!string.IsNullOrEmpty(key) && !Loc.Has(key)) errors.Add(owner + ": missing localization key " + key);
        }
    }
}
