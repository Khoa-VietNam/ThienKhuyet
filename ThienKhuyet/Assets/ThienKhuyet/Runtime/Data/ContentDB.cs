using System.Collections.Generic;
using ThienKhuyet.Cultivation;
using UnityEngine;

namespace ThienKhuyet.Data
{
    /// <summary>
    /// Runtime registry of all definitions. Defaults come from <see cref="ContentLibrary"/>; any ScriptableObject placed under
    /// Resources/Content with the same id overrides the default (data-driven rebalancing without code changes).
    /// </summary>
    public static class ContentDB
    {
        static readonly Dictionary<string, ItemDef> items = new Dictionary<string, ItemDef>();
        static readonly Dictionary<string, SkillDef> skills = new Dictionary<string, SkillDef>();
        static readonly Dictionary<string, EnemyDef> enemies = new Dictionary<string, EnemyDef>();
        static readonly Dictionary<string, LootTableDef> loot = new Dictionary<string, LootTableDef>();
        static readonly Dictionary<WeaponFamily, WeaponFamilyDef> families = new Dictionary<WeaponFamily, WeaponFamilyDef>();
        static RealmDef[] realms = new RealmDef[0];
        static readonly RealmTable table = new RealmTable();

        public static bool Loaded { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            items.Clear();
            skills.Clear();
            enemies.Clear();
            loot.Clear();
            families.Clear();
            realms = new RealmDef[0];
            Loaded = false;
        }

        public static IRealmTable Realms => table;
        public static int RealmCount => realms.Length;
        public static IEnumerable<ItemDef> AllItems => items.Values;
        public static IEnumerable<SkillDef> AllSkills => skills.Values;
        public static IEnumerable<EnemyDef> AllEnemies => enemies.Values;
        public static IEnumerable<LootTableDef> AllLoot => loot.Values;

        public static void Load()
        {
            if (Loaded) return;
            foreach (var d in ContentLibrary.BuildItems()) items[d.id] = d;
            foreach (var d in ContentLibrary.BuildSkills()) skills[d.id] = d;
            foreach (var d in ContentLibrary.BuildEnemies()) enemies[d.id] = d;
            foreach (var d in ContentLibrary.BuildLoot()) loot[d.id] = d;
            foreach (var d in ContentLibrary.BuildFamilies()) families[d.family] = d;
            realms = ContentLibrary.BuildRealms();

            // designer overrides
            foreach (var d in Resources.LoadAll<ItemDef>("Content")) if (!string.IsNullOrEmpty(d.id)) items[d.id] = d;
            foreach (var d in Resources.LoadAll<SkillDef>("Content")) if (!string.IsNullOrEmpty(d.id)) skills[d.id] = d;
            foreach (var d in Resources.LoadAll<EnemyDef>("Content")) if (!string.IsNullOrEmpty(d.id)) enemies[d.id] = d;
            foreach (var d in Resources.LoadAll<LootTableDef>("Content")) if (!string.IsNullOrEmpty(d.id)) loot[d.id] = d;
            foreach (var d in Resources.LoadAll<WeaponFamilyDef>("Content")) families[d.family] = d;
            foreach (var d in Resources.LoadAll<RealmDef>("Content"))
            {
                if (d.index >= 0 && d.index < realms.Length) realms[d.index] = d;
            }
            table.Bind(realms);
            Loaded = true;
        }

        public static ItemDef Item(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            items.TryGetValue(id, out ItemDef d);
            return d;
        }

        public static SkillDef Skill(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            skills.TryGetValue(id, out SkillDef d);
            return d;
        }

        public static EnemyDef Enemy(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            enemies.TryGetValue(id, out EnemyDef d);
            return d;
        }

        public static LootTableDef Loot(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            loot.TryGetValue(id, out LootTableDef d);
            return d;
        }

        public static WeaponFamilyDef Family(WeaponFamily f)
        {
            families.TryGetValue(f, out WeaponFamilyDef d);
            return d;
        }

        public static RealmDef Realm(int index)
        {
            if (realms.Length == 0) return null;
            return realms[Mathf.Clamp(index, 0, realms.Length - 1)];
        }

        public static int StackLimit(string itemId)
        {
            ItemDef d = Item(itemId);
            return d != null ? Mathf.Max(1, d.maxStack) : 99;
        }

        sealed class RealmTable : IRealmTable
        {
            RealmDef[] defs = new RealmDef[0];

            public void Bind(RealmDef[] d)
            {
                defs = d;
            }

            public int RealmCount => defs.Length;

            public int StageCount(int realm)
            {
                return defs[Mathf.Clamp(realm, 0, defs.Length - 1)].stages;
            }

            public float ExpForStage(int realm, int stage)
            {
                return defs[Mathf.Clamp(realm, 0, defs.Length - 1)].ExpForStage(stage);
            }
        }
    }
}
