using System.Collections.Generic;
using NUnit.Framework;
using ThienKhuyet.Combat;
using ThienKhuyet.Core;
using ThienKhuyet.Cultivation;
using ThienKhuyet.Data;
using ThienKhuyet.Items;

namespace ThienKhuyet.Tests
{
    /// <summary>Test double for the game state used by conditions and effects.</summary>
    public sealed class FakeGame : IGameEffects
    {
        public readonly GameFlags Flags = new GameFlags();
        public readonly Dictionary<string, int> Items = new Dictionary<string, int>();
        public readonly Dictionary<string, string> Quests = new Dictionary<string, string>();
        public readonly Dictionary<string, int> Rep = new Dictionary<string, int>();
        public readonly List<string> Log = new List<string>();
        public int Realm, Stage = 1, ActNumber = 1;
        public bool Night;
        public string Build = "none";

        public bool HasFlag(string flag) { return Flags.Has(flag); }
        public int GetInt(string key) { return Flags.GetInt(key); }
        public string GetStr(string key) { return Flags.GetStr(key); }
        public int RealmIndex { get { return Realm; } }
        public int RealmStage { get { return Stage; } }
        public int ItemCount(string itemId) { int n; return Items.TryGetValue(itemId, out n) ? n : 0; }
        public string QuestStatus(string questId) { string s; return Quests.TryGetValue(questId, out s) ? s : "none"; }
        public int Reputation(string factionId) { int n; return Rep.TryGetValue(factionId, out n) ? n : 0; }
        public int Act { get { return ActNumber; } }
        public bool IsNight { get { return Night; } }
        public string BuildName { get { return Build; } }

        public void SetFlag(string flag, bool value) { Flags.Set(flag, value); Log.Add("flag:" + flag + "=" + value); }
        public void SetInt(string key, int value) { Flags.SetInt(key, value); Log.Add("int:" + key + "=" + value); }
        public void SetStr(string key, string value) { Flags.SetStr(key, value); Log.Add("str:" + key + "=" + value); }
        public void StartQuest(string questId) { Quests[questId] = "active"; Log.Add("quest.start:" + questId); }
        public void CompleteQuest(string questId) { Quests[questId] = "done"; Log.Add("quest.done:" + questId); }
        public void FailQuest(string questId) { Quests[questId] = "failed"; Log.Add("quest.fail:" + questId); }
        public void GiveItem(string itemId, int count) { Items[itemId] = ItemCount(itemId) + count; Log.Add("give:" + itemId + "*" + count); }
        public void TakeItem(string itemId, int count) { Items[itemId] = ItemCount(itemId) - count; Log.Add("take:" + itemId + "*" + count); }
        public void AddReputation(string factionId, int delta) { Rep[factionId] = Reputation(factionId) + delta; Log.Add("rep:" + factionId + delta); }
        public void AddExp(float amount) { Log.Add("exp:" + amount); }
        public void PlayCutscene(string cutsceneId) { Log.Add("cut:" + cutsceneId); }
        public void HealPlayer() { Log.Add("heal"); }
        public void SetAct(int act) { ActNumber = act; Log.Add("act:" + act); }
        public void LearnSkill(string skillId) { Log.Add("learn:" + skillId); }
        public void Teleport(string anchorId) { Log.Add("tp:" + anchorId); }
        public void EndGame(string endingId) { Log.Add("end:" + endingId); }
        public void Toast(string textKey) { Log.Add("toast:" + textKey); }
        public void Unlock(string id) { Log.Add("unlock:" + id); }
        public void ShowTitle(string textKey) { Log.Add("title:" + textKey); }
        public void SaveGame() { Log.Add("save"); }
    }

    public sealed class FakeRealms : IRealmTable
    {
        readonly int[] stages = { 1, 3, 2 };
        readonly float[][] exp = { new[] { 100f }, new[] { 50f, 80f, 120f }, new[] { 200f, 300f } };
        public int RealmCount { get { return stages.Length; } }
        public int StageCount(int realm) { return stages[realm]; }
        public float ExpForStage(int realm, int stage) { return exp[realm][stage - 1]; }
    }

    public class ScriptReaderTests
    {
        [Test]
        public void ParsesVerbArgsAndKeyValues()
        {
            var lines = ScriptReader.Parse("at 1.5 shot kind=close target=\"the elder\"  # comment\n\n// nothing\nsay player dlg.key", "t");
            Assert.AreEqual(2, lines.Count);
            Assert.AreEqual("at", lines[0].Verb);
            Assert.AreEqual("1.5", lines[0].Arg(0));
            Assert.AreEqual("shot", lines[0].Arg(1));
            Assert.AreEqual("close", lines[0].Get("kind"));
            Assert.AreEqual("the elder", lines[0].Get("target"));
            Assert.AreEqual("say", lines[1].Verb);
            Assert.AreEqual("dlg.key", lines[1].Arg(1));
        }

        [Test]
        public void ConditionsWithOperatorsStayInsideQuotedValue()
        {
            var l = ScriptReader.ParseLine("choice text=a.b if=\"realm>=2&flag:x\" do=\"flag+:y;give:herb_basic*2\"", 1, "t");
            Assert.AreEqual("realm>=2&flag:x", l.Get("if"));
            Assert.AreEqual("flag+:y;give:herb_basic*2", l.Get("do"));
        }

        [Test]
        public void HashInsideValueIsNotAComment()
        {
            var l = ScriptReader.ParseLine("env color=#ff0000 fog=0.02", 1, "t");
            Assert.AreEqual("#ff0000", l.Get("color"));
            Assert.AreEqual(0.02f, l.GetFloat("fog"), 1e-5f);
        }

        [Test]
        public void EmptyQuotedValueIsKept()
        {
            var l = ScriptReader.ParseLine("node a text=\"\" next=b", 1, "t");
            Assert.IsTrue(l.Has("text"));
            Assert.AreEqual("", l.Get("text"));
            Assert.AreEqual("b", l.Get("next"));
        }

        [Test]
        public void ParseFloatsUsesDefaults()
        {
            var f = ScriptReader.ParseFloats("1.5, 2", 0f, 0f, 9f);
            Assert.AreEqual(1.5f, f[0], 1e-5f);
            Assert.AreEqual(2f, f[1], 1e-5f);
            Assert.AreEqual(9f, f[2], 1e-5f);
        }
    }

    public class LocTests
    {
        [Test]
        public void ParsesTableWithEscapesAndComments()
        {
            var d = new Dictionary<string, string>();
            int n = Loc.ParseTable("# header\nkey.a = Xin chào\\nthế giới\n\nkey.b=Giá trị = 5\n  # indented comment\n", d);
            Assert.AreEqual(2, n);
            Assert.AreEqual("Xin chào\nthế giới", d["key.a"]);
            Assert.AreEqual("Giá trị = 5", d["key.b"]);
        }

        [Test]
        public void FallsBackToSourceLanguage()
        {
            Loc.LoadFromStrings("en", "a=Một\nb=Hai", "a=One");
            Assert.AreEqual("One", Loc.T("a"));
            Assert.AreEqual("Hai", Loc.T("b"));
            Assert.IsTrue(Loc.Has("b"));
            Assert.IsFalse(Loc.Has("zzz"));
            Loc.LoadFromStrings("vi", "", "");
        }
    }

    public class ConditionTests
    {
        [Test]
        public void EvaluatesAtomsAndConjunctions()
        {
            var g = new FakeGame();
            g.Flags.Set("met_elder");
            g.Items["herb_basic"] = 3;
            g.Realm = 1;
            g.Rep["village"] = 12;
            g.Quests["q1"] = "active";
            g.Flags.SetInt("wolves", 4);
            g.Flags.SetStr("path", "sword");

            Assert.IsTrue(Cond.Eval("flag:met_elder", g));
            Assert.IsFalse(Cond.Eval("!flag:met_elder", g));
            Assert.IsTrue(Cond.Eval("item:herb_basic>=3", g));
            Assert.IsFalse(Cond.Eval("item:herb_basic>=4", g));
            Assert.IsTrue(Cond.Eval("item:herb_basic", g));
            Assert.IsTrue(Cond.Eval("realm>=1&rep:village>=10", g));
            Assert.IsFalse(Cond.Eval("realm>=2", g));
            Assert.IsTrue(Cond.Eval("quest:q1=active", g));
            Assert.IsFalse(Cond.Eval("quest:q1=done", g));
            Assert.IsTrue(Cond.Eval("quest:q2=none", g));
            Assert.IsTrue(Cond.Eval("int:wolves>=3&int:wolves<5", g));
            Assert.IsTrue(Cond.Eval("str:path=sword", g));
            Assert.IsFalse(Cond.Eval("str:path=fist", g));
            Assert.IsTrue(Cond.Eval("day", g));
            g.Night = true;
            Assert.IsTrue(Cond.Eval("night", g));
            Assert.IsTrue(Cond.Eval("", g));
            Assert.IsTrue(Cond.Eval("met_elder", g), "bare word is a flag name");
        }

        [Test]
        public void FlagNamesStartingWithKeywordsAreFlags()
        {
            var g = new FakeGame();
            g.Flags.Set("act1_done");
            g.Flags.Set("realm_seen");
            Assert.IsTrue(Cond.Eval("act1_done", g));
            Assert.IsTrue(Cond.Eval("realm_seen", g));
        }

        [Test]
        public void EffectsApplyThroughInterface()
        {
            var g = new FakeGame();
            int n = Fx.Apply("flag+:a; give:herb_basic*3; take:herb_basic; quest.start:q1; rep:village+5; exp:120; cut:the_fall; act:2; int:k+=2; int:k+=3; str:path=sword; heal; toast:ui.x; learn:sword_wave", g);
            Assert.AreEqual(14, n);
            Assert.IsTrue(g.Flags.Has("a"));
            Assert.AreEqual(2, g.ItemCount("herb_basic"));
            Assert.AreEqual("active", g.QuestStatus("q1"));
            Assert.AreEqual(5, g.Reputation("village"));
            Assert.AreEqual(2, g.Act);
            Assert.AreEqual(5, g.Flags.GetInt("k"));
            Assert.AreEqual("sword", g.Flags.GetStr("path"));
            CollectionAssert.Contains(g.Log, "cut:the_fall");
            CollectionAssert.Contains(g.Log, "learn:sword_wave");
            Fx.Apply("flag-:a", g);
            Assert.IsFalse(g.Flags.Has("a"));
        }

        [Test]
        public void UnknownEffectsAreIgnored()
        {
            Assert.AreEqual(0, Fx.Apply("bogus:thing; nothing", new FakeGame()));
        }
    }

    public class FlagsTests
    {
        [Test]
        public void RoundTripsThroughData()
        {
            var f = new GameFlags();
            f.Set("a");
            f.SetInt("n", 7);
            f.SetStr("s", "x");
            var g = new GameFlags();
            g.FromData(f.ToData());
            Assert.IsTrue(g.Has("a"));
            Assert.AreEqual(7, g.GetInt("n"));
            Assert.AreEqual("x", g.GetStr("s"));
        }

        [Test]
        public void ChangedEventFiresOnlyOnRealChanges()
        {
            var f = new GameFlags();
            int count = 0;
            f.Changed += _ => count++;
            f.Set("a");
            f.Set("a");
            f.SetInt("n", 1);
            f.SetInt("n", 1);
            Assert.AreEqual(2, count);
        }
    }

    public class StatTests
    {
        [Test]
        public void CombinesBaseFlatAndPercent()
        {
            var s = new StatBlock();
            s.SetBase(StatId.Attack, 10f);
            s.SetSource("weapon", new[] { new StatMod(StatId.Attack, 5f, 0f) });
            s.SetSource("buff", new[] { new StatMod(StatId.Attack, 0f, 0.5f) });
            Assert.AreEqual(22.5f, s.Get(StatId.Attack), 1e-4f);
            s.RemoveSource("buff");
            Assert.AreEqual(15f, s.Get(StatId.Attack), 1e-4f);
            Assert.IsFalse(s.HasSource("buff"));
        }

        [Test]
        public void AttributeSpendingNeedsPoints()
        {
            var a = new AttributeSet();
            Assert.IsFalse(a.Spend(AttributeId.Strength));
            a.unspent = 2;
            Assert.IsTrue(a.Spend(AttributeId.Strength));
            Assert.AreEqual(6, a.strength);
            Assert.AreEqual(1, a.unspent);
        }
    }

    public class DamageTests
    {
        [Test]
        public void DefenseHasDiminishingReturns()
        {
            Assert.AreEqual(50f, DamageCalc.Mitigate(100f, 100f), 1e-3f);
            Assert.AreEqual(100f, DamageCalc.Mitigate(100f, 0f), 1e-3f);
            Assert.Greater(DamageCalc.Mitigate(100f, 50f), DamageCalc.Mitigate(100f, 150f));
        }

        [Test]
        public void RealmGapChangesDamage()
        {
            Assert.AreEqual(1f, DamageCalc.RealmFactor(2, 2), 1e-4f);
            Assert.AreEqual(1.4f, DamageCalc.RealmFactor(3, 1), 1e-4f);
            Assert.AreEqual(0.6f, DamageCalc.RealmFactor(0, 2), 1e-4f);
            Assert.AreEqual(0.55f, DamageCalc.RealmFactor(0, 5), 1e-4f);
            Assert.AreEqual(1.9f, DamageCalc.RealmFactor(9, 0), 1e-4f);
        }

        [Test]
        public void FinalDamageAppliesCritAndGuard()
        {
            float plain = DamageCalc.Final(100f, 0f, 1, 1, DamageType.Physical, 0f, false, 0f, 0f);
            float crit = DamageCalc.Final(100f, 0f, 1, 1, DamageType.Physical, 0f, true, 0f, 0f);
            float guarded = DamageCalc.Final(100f, 0f, 1, 1, DamageType.Physical, 0f, false, 0f, 0.7f);
            Assert.AreEqual(100f, plain, 1e-3f);
            Assert.AreEqual(150f, crit, 1e-3f);
            Assert.AreEqual(30f, guarded, 1e-3f);
            Assert.AreEqual(100f, DamageCalc.Final(100f, 500f, 1, 1, DamageType.True, 0f, false, 0f, 0f), 1e-3f);
        }
    }

    public class InventoryTests
    {
        static Inventory Make(int cap = 4)
        {
            return new Inventory(cap, id => id == "gear" ? 1 : 10);
        }

        [Test]
        public void StacksUpToLimitAndOverflowsToNewSlots()
        {
            var inv = Make();
            Assert.AreEqual(0, inv.Add("herb", 25));
            Assert.AreEqual(25, inv.Count("herb"));
            Assert.AreEqual(3, inv.UsedSlots);
        }

        [Test]
        public void ReturnsLeftoverWhenFull()
        {
            var inv = Make(2);
            Assert.AreEqual(5, inv.Add("herb", 25));
            Assert.AreEqual(20, inv.Count("herb"));
            Assert.IsFalse(inv.CanAdd("herb", 1));
            Assert.IsFalse(inv.CanAdd("gear", 1));
        }

        [Test]
        public void RemoveSpansStacksAndFailsAtomically()
        {
            var inv = Make();
            inv.Add("herb", 25);
            Assert.IsFalse(inv.Remove("herb", 30));
            Assert.AreEqual(25, inv.Count("herb"));
            Assert.IsTrue(inv.Remove("herb", 12));
            Assert.AreEqual(13, inv.Count("herb"));
        }

        [Test]
        public void SortMergesStacks()
        {
            var inv = Make();
            inv.Add("herb", 5);
            inv.Add("gear", 1);
            inv.Add("herb", 8);
            inv.Sort(null);
            Assert.AreEqual(13, inv.Count("herb"));
            Assert.AreEqual("gear", inv.GetSlot(0).itemId);
            Assert.AreEqual(3, inv.UsedSlots);
        }

        [Test]
        public void SerializesToArrayAndBack()
        {
            var inv = Make();
            inv.Add("herb", 15);
            inv.Add("gear", 1);
            var copy = Make();
            copy.FromArray(inv.ToArray());
            Assert.AreEqual(15, copy.Count("herb"));
            Assert.AreEqual(1, copy.Count("gear"));
        }

        [Test]
        public void ChangedEventFires()
        {
            var inv = Make();
            int n = 0;
            inv.Changed += () => n++;
            inv.Add("herb", 1);
            inv.Remove("herb", 1);
            Assert.AreEqual(2, n);
        }
    }

    public class CultivationTests
    {
        [Test]
        public void ExpAdvancesStagesWithCarryOver()
        {
            var c = new CultivationState(new FakeRealms());
            c.Set(1, 1, 0f);
            int gained = c.AddExp(140f); // 50 -> stage 2 (90 left) -> 80 -> stage 3 (10 left)
            Assert.AreEqual(2, gained);
            Assert.AreEqual(3, c.Stage);
            Assert.AreEqual(10f, c.Exp, 1e-3f);
            Assert.IsFalse(c.ReadyForBreakthrough);
        }

        [Test]
        public void BreakthroughOnlyWhenBarFullAtLastStage()
        {
            var c = new CultivationState(new FakeRealms());
            c.Set(0, 1, 0f);
            Assert.IsFalse(c.Breakthrough());
            c.AddExp(1000f);
            Assert.AreEqual(100f, c.Exp, 1e-3f);
            Assert.IsTrue(c.ReadyForBreakthrough);
            bool raised = false;
            c.Advanced += (r, s, b) => raised = b;
            Assert.IsTrue(c.Breakthrough());
            Assert.IsTrue(raised);
            Assert.AreEqual(1, c.Realm);
            Assert.AreEqual(1, c.Stage);
            Assert.AreEqual(0f, c.Exp, 1e-3f);
        }

        [Test]
        public void LastRealmCannotBreakthrough()
        {
            var c = new CultivationState(new FakeRealms());
            c.Set(2, 2, 0f);
            c.AddExp(99999f);
            Assert.IsTrue(c.AtLastRealm);
            Assert.IsFalse(c.ReadyForBreakthrough);
            Assert.IsFalse(c.Breakthrough());
        }

        [Test]
        public void FailurePenaltyReducesExp()
        {
            var c = new CultivationState(new FakeRealms());
            c.Set(0, 1, 100f);
            c.ApplyFailurePenalty(0.3f);
            Assert.AreEqual(70f, c.Exp, 1e-3f);
        }
    }

    public class RandomAndLootTests
    {
        [Test]
        public void RngIsDeterministicPerSeed()
        {
            var a = new Rng(42);
            var b = new Rng(42);
            var c = new Rng(43);
            bool anyDiff = false;
            for (int i = 0; i < 16; i++)
            {
                uint x = a.Next();
                Assert.AreEqual(x, b.Next());
                if (x != c.Next()) anyDiff = true;
            }
            Assert.IsTrue(anyDiff);
        }

        [Test]
        public void RngRangesAreRespected()
        {
            var r = new Rng(7);
            for (int i = 0; i < 500; i++)
            {
                float v = r.Value();
                Assert.GreaterOrEqual(v, 0f);
                Assert.Less(v, 1f);
                int n = r.Int(3, 5);
                Assert.GreaterOrEqual(n, 3);
                Assert.LessOrEqual(n, 5);
            }
        }

        [Test]
        public void WeightedPickFollowsWeights()
        {
            var r = new Rng(5);
            var w = new List<float> { 0f, 1f, 0f };
            for (int i = 0; i < 50; i++) Assert.AreEqual(1, r.Weighted(w));
        }

        [Test]
        public void NoiseStaysInRangeAndTiles()
        {
            for (int i = 0; i < 200; i++)
            {
                float v = Noise.Fbm(i * 0.37f, i * 0.11f, 5, 2f, 0.5f, 3);
                Assert.GreaterOrEqual(v, -1.0001f);
                Assert.LessOrEqual(v, 1.0001f);
            }
            float a = Noise.FbmTile(0.0f, 0.3f, 4, 3, 0.5f, 9);
            float b = Noise.FbmTile(1.0f, 0.3f, 4, 3, 0.5f, 9);
            Assert.AreEqual(a, b, 1e-3f);
        }

        [Test]
        public void LootRollIsDeterministicAndHonoursChances()
        {
            var entries = new[]
            {
                new LootEntry("always", 0f, 1f, 2, 2),
                new LootEntry("never", 0f, 0.0001f, 1, 1),
                new LootEntry("w1", 1f, 0f, 1, 1),
                new LootEntry("w2", 0f, 0f, 1, 1)
            };
            var r1 = new Rng(11);
            var r2 = new Rng(11);
            var a = new List<ItemCost>();
            var b = new List<ItemCost>();
            LootRoller.Roll(entries, 1, 0f, ref r1, 0f, a);
            LootRoller.Roll(entries, 1, 0f, ref r2, 0f, b);
            Assert.AreEqual(a.Count, b.Count);
            Assert.AreEqual("always", a[0].itemId);
            Assert.AreEqual(2, a[0].count);
            Assert.AreEqual("w1", a[a.Count - 1].itemId);
        }
    }

    public class EventBusTests
    {
        struct Ping { public int value; }

        [Test]
        public void DeliversAndUnsubscribes()
        {
            EventBus.Clear();
            int sum = 0;
            System.Action<Ping> h = p => sum += p.value;
            EventBus.Subscribe(h);
            EventBus.Publish(new Ping { value = 2 });
            EventBus.Publish(new Ping { value = 3 });
            EventBus.Unsubscribe(h);
            EventBus.Publish(new Ping { value = 100 });
            Assert.AreEqual(5, sum);
        }
    }
}
