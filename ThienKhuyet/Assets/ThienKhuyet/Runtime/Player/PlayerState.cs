using System;
using System.Collections.Generic;
using ThienKhuyet.Core;
using ThienKhuyet.Cultivation;
using ThienKhuyet.Data;
using ThienKhuyet.Items;
using UnityEngine;

namespace ThienKhuyet.Player
{
    /// <summary>
    /// Everything that defines the protagonist's build: attributes, realm, equipment, techniques, inventory.
    /// Recomputes the shared <see cref="StatBlock"/> from realm + attributes + gear + cultivation path, so cultivation
    /// directly changes HP, damage, defense, Qi and speed in combat.
    /// </summary>
    public sealed class PlayerState
    {
        public const int SkillSlots = 4;

        public readonly StatBlock stats = new StatBlock();
        public readonly AttributeSet attributes = new AttributeSet();
        public readonly Inventory inventory;
        public readonly Equipment equipment = new Equipment();
        public readonly CultivationState cultivation;
        public readonly List<string> learnedSkills = new List<string>();
        public readonly string[] skillSlots = new string[SkillSlots];
        public CultivationPath path = CultivationPath.None;
        public readonly HashSet<string> unlocks = new HashSet<string>();

        public event Action Changed;
        public event Action<string> SkillLearned;

        bool suspend;

        public PlayerState()
        {
            inventory = new Inventory(48, ContentDB.StackLimit);
            cultivation = new CultivationState(ContentDB.Realms);
            cultivation.Advanced += OnAdvanced;
            inventory.Changed += NotifyChanged;
            equipment.Changed += OnEquipmentChanged;
            for (int i = 0; i < skillSlots.Length; i++) skillSlots[i] = string.Empty;
            Recompute();
        }

        void NotifyChanged()
        {
            if (!suspend) Changed?.Invoke();
        }

        void OnEquipmentChanged()
        {
            if (suspend) return;
            Recompute();
        }

        void OnAdvanced(int realm, int stage, bool breakthrough)
        {
            RealmDef r = ContentDB.Realm(realm);
            if (breakthrough)
            {
                attributes.unspent += r.attributePointsOnBreakthrough;
                for (int i = 0; i < r.unlocks.Length; i++) unlocks.Add(r.unlocks[i]);
            }
            else attributes.unspent += r.attributePointsPerStage;
            Recompute();
            EventBus.Publish(new RealmChangedEvent { realm = realm, stage = stage, breakthrough = breakthrough });
        }

        public int Realm => cultivation.Realm;

        public bool HasUnlock(string id) { return unlocks.Contains(id); }

        // ------------------------------------------------------------------ stats
        public void Recompute()
        {
            stats.SetBase(StatId.MaxHp, 55f);
            stats.SetBase(StatId.MaxStamina, 70f);
            stats.SetBase(StatId.MaxQi, 0f);
            stats.SetBase(StatId.Attack, 10f);
            stats.SetBase(StatId.Defense, 0f);
            stats.SetBase(StatId.SpellPower, 6f);
            stats.SetBase(StatId.CritChance, 0.05f);
            stats.SetBase(StatId.CritDamage, 0f);
            stats.SetBase(StatId.MoveSpeed, 1f);
            stats.SetBase(StatId.HpRegen, 0f);
            stats.SetBase(StatId.StaminaRegen, 24f);
            stats.SetBase(StatId.QiRegen, 0.8f);
            stats.SetBase(StatId.Poise, 40f);
            stats.SetBase(StatId.CooldownReduction, 0f);
            stats.SetBase(StatId.ExpGain, 0f);
            stats.SetBase(StatId.LootLuck, 0f);

            stats.SetSource("attr", attributes.ToMods());
            stats.SetSource("realm", RealmMods());
            stats.SetSource("path", PathMods(path));
            stats.SetSource("equip", EquipmentMods());
            Changed?.Invoke();
        }

        StatMod[] RealmMods()
        {
            var sum = new float[(int)StatId.Count];
            var pct = new float[(int)StatId.Count];
            for (int r = 0; r <= cultivation.Realm && r < ContentDB.RealmCount; r++)
            {
                RealmDef d = ContentDB.Realm(r);
                int stagesDone = r < cultivation.Realm ? d.stages - 1 : cultivation.Stage - 1;
                for (int i = 0; i < d.realmMods.Length; i++) { sum[(int)d.realmMods[i].stat] += d.realmMods[i].flat; pct[(int)d.realmMods[i].stat] += d.realmMods[i].pct; }
                for (int i = 0; i < d.stageMods.Length; i++) { sum[(int)d.stageMods[i].stat] += d.stageMods[i].flat * stagesDone; pct[(int)d.stageMods[i].stat] += d.stageMods[i].pct * stagesDone; }
            }
            var mods = new List<StatMod>();
            for (int i = 0; i < sum.Length; i++)
                if (Math.Abs(sum[i]) > 1e-5f || Math.Abs(pct[i]) > 1e-5f) mods.Add(new StatMod((StatId)i, sum[i], pct[i]));
            return mods.ToArray();
        }

        public static StatMod[] PathMods(CultivationPath p)
        {
            switch (p)
            {
                case CultivationPath.Wu: return new[] { new StatMod(StatId.MaxHp, 0f, 0.15f), new StatMod(StatId.Poise, 0f, 0.25f), new StatMod(StatId.Defense, 3f, 0f), new StatMod(StatId.StaminaRegen, 4f, 0f) };
                case CultivationPath.Jian: return new[] { new StatMod(StatId.Attack, 0f, 0.1f), new StatMod(StatId.CritChance, 0.06f, 0f), new StatMod(StatId.MoveSpeed, 0f, 0.04f) };
                case CultivationPath.Fa: return new[] { new StatMod(StatId.SpellPower, 0f, 0.25f), new StatMod(StatId.MaxQi, 0f, 0.3f), new StatMod(StatId.QiRegen, 0.8f, 0f), new StatMod(StatId.CooldownReduction, 0.08f, 0f) };
                case CultivationPath.Hybrid: return new[] { new StatMod(StatId.Attack, 0f, 0.05f), new StatMod(StatId.SpellPower, 0f, 0.1f), new StatMod(StatId.MaxHp, 0f, 0.06f), new StatMod(StatId.MaxQi, 0f, 0.1f), new StatMod(StatId.ExpGain, 0f, 0.06f) };
                default: return new StatMod[0];
            }
        }

        StatMod[] EquipmentMods()
        {
            var mods = new List<StatMod>();
            foreach (EquipSlot slot in Equipment.AllSlots)
            {
                ItemDef item = ContentDB.Item(equipment.Get(slot));
                if (item != null) mods.AddRange(item.mods);
            }
            return mods.ToArray();
        }

        // ------------------------------------------------------------------ equipment
        public ItemDef WeaponItem => ContentDB.Item(equipment.Get(EquipSlot.Weapon));

        public WeaponFamily WeaponFamilyNow
        {
            get
            {
                ItemDef w = WeaponItem;
                return w != null ? w.family : WeaponFamily.Fist;
            }
        }

        public bool CanEquip(ItemDef item, out string reasonKey)
        {
            reasonKey = null;
            if (item == null || !item.IsEquipment) { reasonKey = "ui.cannot_equip"; return false; }
            if (item.reqRealm > cultivation.Realm) { reasonKey = "ui.realm_too_low"; return false; }
            return true;
        }

        /// <summary>Equips from the inventory; the previous item goes back to the inventory.</summary>
        public bool Equip(string itemId)
        {
            ItemDef item = ContentDB.Item(itemId);
            if (!CanEquip(item, out _)) return false;
            if (!inventory.Has(itemId)) return false;
            string old = equipment.Get(item.slot);
            suspend = true;
            inventory.Remove(itemId, 1);
            if (!string.IsNullOrEmpty(old)) inventory.Add(old, 1);
            suspend = false;
            equipment.Set(item.slot, itemId);
            return true;
        }

        public bool Unequip(EquipSlot slot)
        {
            string id = equipment.Get(slot);
            if (string.IsNullOrEmpty(id)) return false;
            if (!inventory.CanAdd(id, 1)) return false;
            suspend = true;
            inventory.Add(id, 1);
            suspend = false;
            equipment.Set(slot, null);
            return true;
        }

        // ------------------------------------------------------------------ techniques
        public bool KnowsSkill(string id) { return learnedSkills.Contains(id); }

        public bool LearnSkill(string id)
        {
            if (ContentDB.Skill(id) == null || learnedSkills.Contains(id)) return false;
            learnedSkills.Add(id);
            // auto-equip into the first empty slot
            for (int i = 0; i < skillSlots.Length; i++)
            {
                if (string.IsNullOrEmpty(skillSlots[i])) { skillSlots[i] = id; break; }
            }
            Changed?.Invoke();
            SkillLearned?.Invoke(id);
            return true;
        }

        public void SetSkillSlot(int slot, string id)
        {
            if (slot < 0 || slot >= skillSlots.Length) return;
            if (!string.IsNullOrEmpty(id))
                for (int i = 0; i < skillSlots.Length; i++) if (skillSlots[i] == id) skillSlots[i] = string.Empty;
            skillSlots[slot] = id ?? string.Empty;
            Changed?.Invoke();
        }

        public bool SkillUsable(SkillDef s)
        {
            return s != null && s.reqRealm <= cultivation.Realm;
        }

        public float QiCost(SkillDef s)
        {
            float cost = s.qiCost;
            if (path == CultivationPath.Fa) cost *= 0.85f;
            else if (path == CultivationPath.Hybrid) cost *= 0.95f;
            return cost;
        }

        public float Cooldown(SkillDef s)
        {
            return s.cooldown * (1f - Mathf.Clamp(stats.Get(StatId.CooldownReduction), 0f, 0.6f));
        }

        /// <summary>Label key for the build: derived from the chosen path or the equipped weapon.</summary>
        public string BuildName
        {
            get
            {
                switch (path)
                {
                    case CultivationPath.Wu: return "wu";
                    case CultivationPath.Jian: return "jian";
                    case CultivationPath.Fa: return "fa";
                    case CultivationPath.Hybrid: return "hybrid";
                }
                return "none";
            }
        }

        /// <summary>Multiplier applied to weapon attacks from the cultivation path (rewards matching weapons).</summary>
        public float PathWeaponMultiplier(WeaponFamily f)
        {
            switch (path)
            {
                case CultivationPath.Wu: return f == WeaponFamily.Fist ? 1.2f : 0.95f;
                case CultivationPath.Jian: return f == WeaponFamily.Sword || f == WeaponFamily.Blade ? 1.15f : 0.95f;
                case CultivationPath.Fa: return f == WeaponFamily.Staff ? 1.1f : 0.92f;
                case CultivationPath.Hybrid: return 1.05f;
                default: return 1f;
            }
        }

        public void SetPath(CultivationPath p)
        {
            path = p;
            Recompute();
        }

        public void GrantStarterSkills(CultivationPath p)
        {
            switch (p)
            {
                case CultivationPath.Wu: LearnSkill("palm_shock"); LearnSkill("iron_body"); break;
                case CultivationPath.Jian: LearnSkill("sword_wave"); LearnSkill("swift_step"); break;
                case CultivationPath.Fa: LearnSkill("fire_talisman"); LearnSkill("mend"); break;
                case CultivationPath.Hybrid: LearnSkill("qi_bolt"); LearnSkill("swift_step"); LearnSkill("gather_qi"); break;
            }
        }

        // ------------------------------------------------------------------ save support
        public void Suspend(bool value)
        {
            suspend = value;
            if (!value) Recompute();
        }
    }
}
