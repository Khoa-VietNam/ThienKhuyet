using System.Collections.Generic;
using UnityEngine;

namespace ThienKhuyet.Data
{
    /// <summary>
    /// Code-defined default content (ScriptableObject instances created at runtime). Everything here can be overridden by
    /// assets placed under Resources/Content (same id wins) so designers can rebalance without touching code.
    /// </summary>
    public static class ContentLibrary
    {
        // ------------------------------------------------------------------ helpers
        static StatMod M(StatId s, float flat) { return new StatMod(s, flat, 0f); }
        static StatMod P(StatId s, float pct) { return new StatMod(s, 0f, pct); }

        static AttackDef A(string id, string clip, float dur, float hs, float he, float cancel, float dmg, float poise, float kb,
            float range, float arc, float lunge, float stamina, bool heavy = false, string vfx = "slash", string sfx = "swing")
        {
            return new AttackDef
            {
                id = id, clip = clip, duration = dur, hitStart = hs, hitEnd = he, cancelFrom = cancel, damageMul = dmg,
                poiseDamage = poise, knockback = kb, range = range, arc = arc, lunge = lunge, staminaCost = stamina,
                heavy = heavy, vfx = vfx, sfx = sfx, maxRange = range + 0.6f, qiGain = heavy ? 4f : 2f
            };
        }

        static AttackDef E(string id, string clip, AttackKind kind, float dur, float hs, float he, float dmg, float poise, float kb,
            float range, float arc, float minR, float maxR, float cd, float weight = 1f)
        {
            return new AttackDef
            {
                id = id, clip = clip, kind = kind, duration = dur, hitStart = hs, hitEnd = he, cancelFrom = dur, damageMul = dmg,
                poiseDamage = poise, knockback = kb, range = range, arc = arc, minRange = minR, maxRange = maxR, cooldown = cd,
                weight = weight, staminaCost = 0f, lunge = kind == AttackKind.Melee ? 0.8f : 0f, moveSpeedMul = 0f
            };
        }

        static T New<T>() where T : ScriptableObject { return ScriptableObject.CreateInstance<T>(); }

        // ------------------------------------------------------------------ realms
        public static RealmDef[] BuildRealms()
        {
            var r = new RealmDef[6];
            r[0] = Realm(0, 1, new[] { 90f }, new StatMod[0], new StatMod[0], new ItemCost[] { new ItemCost("body_paste", 1) },
                0f, 1f, "learned_body_tempering", "realm.0.hint", 0, 3, 1f, 1f);
            r[1] = Realm(1, 5, new[] { 120f, 190f, 280f, 400f, 560f },
                new[] { M(StatId.MaxHp, 40), M(StatId.MaxStamina, 20), M(StatId.Defense, 4), M(StatId.Attack, 4), M(StatId.Poise, 10), M(StatId.MaxQi, 30), M(StatId.QiRegen, 0.5f) },
                new[] { M(StatId.MaxHp, 12), M(StatId.Attack, 2), M(StatId.Defense, 1), M(StatId.MaxStamina, 3), M(StatId.MaxQi, 6) },
                new[] { new ItemCost("qi_pill_low", 3), new ItemCost("spirit_stone", 30) }, 0.4f, 0.85f, "", "realm.1.hint", 2, 3, 1.15f, 1.05f,
                "qi_pool", "skills");
            r[2] = Realm(2, 6, new[] { 700f, 900f, 1150f, 1450f, 1800f, 2300f },
                new[] { M(StatId.MaxQi, 80), M(StatId.QiRegen, 1.2f), M(StatId.SpellPower, 10), M(StatId.Attack, 8), M(StatId.MaxHp, 50), M(StatId.MaxStamina, 10) },
                new[] { M(StatId.MaxQi, 15), M(StatId.SpellPower, 3), M(StatId.QiRegen, 0.15f), M(StatId.MaxHp, 10), M(StatId.Attack, 1.5f) },
                new[] { new ItemCost("foundation_pill", 1), new ItemCost("spirit_stone", 120), new ItemCost("beast_core_low", 3) }, 0.5f, 0.7f, "", "realm.2.hint", 3, 4, 1.35f, 1.1f,
                "qi_sense", "advanced_skills");
            r[3] = Realm(3, 4, new[] { 2800f, 3500f, 4300f, 5200f },
                new[] { M(StatId.MaxHp, 120), M(StatId.MaxQi, 160), M(StatId.Attack, 20), M(StatId.SpellPower, 24), M(StatId.Defense, 10), M(StatId.CritChance, 0.04f), P(StatId.MoveSpeed, 0.08f) },
                new[] { M(StatId.MaxHp, 25), M(StatId.MaxQi, 25), M(StatId.Attack, 4), M(StatId.SpellPower, 5) },
                new[] { new ItemCost("golden_core_elixir", 1), new ItemCost("spirit_stone", 400) }, 0.6f, 0.55f, "", "realm.3.hint", 4, 5, 1.6f, 1.15f,
                "air_dash", "foundation_arts");
            r[4] = Realm(4, 3, new[] { 6500f, 8000f, 10000f },
                new[] { M(StatId.MaxHp, 250), M(StatId.MaxQi, 300), M(StatId.Attack, 45), M(StatId.SpellPower, 55), M(StatId.Defense, 22), M(StatId.Poise, 40), M(StatId.CritDamage, 0.25f) },
                new[] { M(StatId.MaxHp, 45), M(StatId.MaxQi, 50), M(StatId.Attack, 8), M(StatId.SpellPower, 10) },
                new[] { new ItemCost("nascent_soul_fruit", 1), new ItemCost("spirit_stone", 1200) }, 0.7f, 0.4f, "truth_known", "realm.4.hint", 5, 6, 1.9f, 1.2f,
                "golden_core_art", "core_arts");
            r[5] = Realm(5, 3, new[] { 12000f, 15000f, 19000f },
                new[] { M(StatId.MaxHp, 450), M(StatId.MaxQi, 600), M(StatId.Attack, 90), M(StatId.SpellPower, 110), M(StatId.Defense, 40), M(StatId.Poise, 80), M(StatId.CooldownReduction, 0.15f) },
                new[] { M(StatId.MaxHp, 80), M(StatId.MaxQi, 90), M(StatId.Attack, 14), M(StatId.SpellPower, 18) },
                new ItemCost[0], 0f, 1f, "", "realm.5.hint", 6, 6, 2.4f, 1.3f,
                "spirit_sense", "nascent_arts");
            return r;
        }

        static RealmDef Realm(int index, int stages, float[] exp, StatMod[] realmMods, StatMod[] stageMods, ItemCost[] req, float qiPct, float chance,
            string flag, string hintKey, int attrPerStage, int attrBreak, float absorb, float dmgMul, params string[] unlocks)
        {
            var d = New<RealmDef>();
            d.name = "Realm" + index;
            d.index = index;
            d.nameKey = "realm." + index + ".name";
            d.descKey = "realm." + index + ".desc";
            d.stages = stages;
            d.expPerStage = exp;
            d.realmMods = realmMods;
            d.stageMods = stageMods;
            d.breakthroughItems = req;
            d.breakthroughQiPercent = qiPct;
            d.breakthroughBaseChance = chance;
            d.breakthroughFlag = flag;
            d.breakthroughHintKey = hintKey;
            d.attributePointsPerStage = Mathf.Min(attrPerStage, 4);
            d.attributePointsOnBreakthrough = attrBreak;
            d.absorbEfficiency = absorb;
            d.outgoingDamageMul = dmgMul;
            d.unlocks = unlocks;
            return d;
        }

        // ------------------------------------------------------------------ weapon families
        public static WeaponFamilyDef[] BuildFamilies()
        {
            var list = new List<WeaponFamilyDef>();

            list.Add(Family(WeaponFamily.Fist, "guard_fist", 0.6f, 0.26f, 2.0f,
                new[]
                {
                    A("fist_1", "fist_l1", 0.42f, 0.12f, 0.24f, 0.26f, 0.85f, 14, 1.5f, 2.0f, 80, 0.8f, 6, false, "impact", "punch"),
                    A("fist_2", "fist_l2", 0.44f, 0.13f, 0.25f, 0.28f, 0.9f, 14, 1.5f, 2.0f, 80, 0.9f, 6, false, "impact", "punch"),
                    A("fist_3", "fist_l3", 0.62f, 0.2f, 0.34f, 0.40f, 1.4f, 30, 4f, 2.3f, 100, 1.4f, 10, false, "impact", "kick")
                },
                A("fist_heavy", "fist_heavy", 1.0f, 0.42f, 0.56f, 0.7f, 2.4f, 65, 7f, 2.5f, 90, 1.6f, 22, true, "shockwave", "heavy_punch")));

            list.Add(Family(WeaponFamily.Sword, "guard_sword", 0.7f, 0.22f, 2.5f,
                new[]
                {
                    A("sword_1", "sword_l1", 0.60f, 0.20f, 0.34f, 0.38f, 1.0f, 18, 2f, 2.5f, 120, 0.9f, 9),
                    A("sword_2", "sword_l2", 0.58f, 0.19f, 0.33f, 0.37f, 1.05f, 18, 2f, 2.5f, 120, 0.9f, 9),
                    A("sword_3", "sword_l3", 0.80f, 0.28f, 0.42f, 0.52f, 1.55f, 36, 5f, 2.9f, 70, 1.9f, 13)
                },
                A("sword_heavy", "sword_heavy", 1.15f, 0.55f, 0.72f, 0.85f, 2.5f, 62, 6.5f, 2.7f, 130, 1.3f, 24, true, "slash_big", "heavy_swing")));

            list.Add(Family(WeaponFamily.Blade, "guard_sword", 0.75f, 0.22f, 2.5f,
                new[]
                {
                    A("blade_1", "sword_l1", 0.68f, 0.24f, 0.38f, 0.44f, 1.2f, 24, 2.5f, 2.6f, 125, 0.9f, 11),
                    A("blade_2", "sword_l2", 0.66f, 0.23f, 0.37f, 0.43f, 1.25f, 24, 2.5f, 2.6f, 125, 0.9f, 11),
                    A("blade_3", "sword_heavy", 0.95f, 0.38f, 0.55f, 0.64f, 1.9f, 45, 6f, 2.8f, 110, 1.4f, 15, false, "slash_big", "heavy_swing")
                },
                A("blade_heavy", "sword_heavy", 1.3f, 0.62f, 0.8f, 0.95f, 2.9f, 72, 7f, 2.8f, 140, 1.3f, 26, true, "slash_big", "heavy_swing")));

            list.Add(Family(WeaponFamily.Spear, "guard_spear", 0.65f, 0.2f, 3.4f,
                new[]
                {
                    A("spear_1", "spear_l1", 0.58f, 0.2f, 0.34f, 0.38f, 1.0f, 16, 2f, 3.4f, 40, 1.3f, 9, false, "thrust", "thrust"),
                    A("spear_2", "spear_l2", 0.66f, 0.24f, 0.4f, 0.44f, 1.05f, 20, 3f, 3.2f, 150, 0.7f, 10),
                    A("spear_3", "spear_l1", 0.62f, 0.2f, 0.34f, 0.42f, 1.5f, 34, 5.5f, 3.8f, 36, 2.2f, 13, false, "thrust", "thrust")
                },
                A("spear_heavy", "spear_heavy", 1.2f, 0.5f, 0.72f, 0.88f, 2.3f, 60, 6f, 3.4f, 360, 0.6f, 24, true, "slash_big", "heavy_swing")));

            list.Add(Family(WeaponFamily.Staff, "guard_staff", 0.6f, 0.24f, 2.8f,
                new[]
                {
                    A("staff_1", "staff_l1", 0.6f, 0.22f, 0.36f, 0.4f, 0.9f, 18, 2.5f, 2.8f, 100, 0.7f, 8, false, "impact", "staff"),
                    A("staff_2", "staff_l2", 0.6f, 0.22f, 0.36f, 0.4f, 0.95f, 18, 2.5f, 2.8f, 100, 0.7f, 8, false, "impact", "staff"),
                    A("staff_3", "staff_l3", 0.85f, 0.34f, 0.5f, 0.58f, 1.5f, 38, 5f, 3.0f, 120, 1.0f, 12, false, "shockwave", "staff")
                },
                A("staff_heavy", "staff_heavy", 1.15f, 0.5f, 0.68f, 0.85f, 2.3f, 60, 6f, 3.1f, 150, 0.9f, 22, true, "shockwave", "heavy_swing")));

            return list.ToArray();
        }

        static WeaponFamilyDef Family(WeaponFamily fam, string guardClip, float blockReduction, float parryWindow, float reach, AttackDef[] light, AttackDef heavy)
        {
            var d = New<WeaponFamilyDef>();
            d.name = "Family_" + fam;
            d.family = fam;
            d.nameKey = "family." + fam.ToString().ToLowerInvariant();
            d.light = light;
            d.heavy = heavy;
            d.guardClip = guardClip;
            d.blockReduction = blockReduction;
            d.parryWindow = parryWindow;
            d.reach = reach;
            return d;
        }

        // ------------------------------------------------------------------ items
        static ItemDef Item(string id, ItemType type, Rarity rarity, IconShape icon, Color color, int value, int maxStack = 99)
        {
            var d = New<ItemDef>();
            d.name = id;
            d.id = id;
            d.nameKey = "item." + id + ".name";
            d.descKey = "item." + id + ".desc";
            d.type = type;
            d.rarity = rarity;
            d.icon = icon;
            d.iconColor = color;
            d.value = value;
            d.maxStack = maxStack;
            d.slot = EquipSlot.None;
            return d;
        }

        static ItemDef Gear(string id, EquipSlot slot, Rarity rarity, IconShape icon, Color color, int value, int reqRealm, Color tint, params StatMod[] mods)
        {
            ItemType type = slot == EquipSlot.Weapon ? ItemType.Weapon : (slot == EquipSlot.Ring || slot == EquipSlot.Pendant ? ItemType.Accessory : ItemType.Armor);
            ItemDef d = Item(id, type, rarity, icon, color, value, 1);
            d.slot = slot;
            d.mods = mods;
            d.reqRealm = reqRealm;
            d.tint = tint;
            return d;
        }

        static ItemDef Weapon(string id, WeaponFamily fam, float damage, Rarity rarity, IconShape icon, Color color, int value, int reqRealm, params StatMod[] extra)
        {
            var mods = new List<StatMod> { M(StatId.Attack, damage) };
            mods.AddRange(extra);
            ItemDef d = Gear(id, EquipSlot.Weapon, rarity, icon, color, value, reqRealm, color, mods.ToArray());
            d.family = fam;
            d.weaponDamage = damage;
            d.visual = fam.ToString().ToLowerInvariant();
            return d;
        }

        static ItemDef Consumable(string id, Rarity rarity, IconShape icon, Color color, int value, float hp, float qi, float stamina, string fx = "")
        {
            ItemDef d = Item(id, ItemType.Consumable, rarity, icon, color, value, 30);
            d.restoreHp = hp;
            d.restoreQi = qi;
            d.restoreStamina = stamina;
            d.useFx = fx;
            return d;
        }

        static ItemDef Manual(string skillId, int value, Rarity rarity)
        {
            ItemDef d = Item("manual_" + skillId, ItemType.Manual, rarity, IconShape.Book, new Color(0.85f, 0.75f, 0.45f), value, 1);
            d.teachSkill = skillId;
            return d;
        }

        public static ItemDef[] BuildItems()
        {
            var c = new Color(0.78f, 0.78f, 0.74f);
            var wood = new Color(0.55f, 0.38f, 0.2f);
            var steel = new Color(0.74f, 0.78f, 0.82f);
            var jade = new Color(0.45f, 0.85f, 0.7f);
            var gold = new Color(0.95f, 0.78f, 0.35f);
            var red = new Color(0.85f, 0.3f, 0.3f);
            var blue = new Color(0.4f, 0.65f, 1f);
            var items = new List<ItemDef>();

            // materials
            items.Add(Item("spirit_stone", ItemType.Material, Rarity.Fine, IconShape.Stone, new Color(0.5f, 0.9f, 1f), 1, 999));
            items.Add(Item("herb_basic", ItemType.Material, Rarity.Common, IconShape.Herb, new Color(0.45f, 0.8f, 0.4f), 3));
            items.Add(Item("wolf_pelt", ItemType.Material, Rarity.Common, IconShape.Pelt, new Color(0.6f, 0.58f, 0.55f), 4));
            items.Add(Item("wolf_fang", ItemType.Material, Rarity.Common, IconShape.Fang, new Color(0.92f, 0.9f, 0.82f), 5));
            items.Add(Item("beast_core_low", ItemType.Material, Rarity.Fine, IconShape.Orb, new Color(0.9f, 0.55f, 0.25f), 25));
            items.Add(Item("iron_ore", ItemType.Material, Rarity.Common, IconShape.Ore, new Color(0.6f, 0.62f, 0.68f), 6));
            items.Add(Item("bandit_token", ItemType.Material, Rarity.Common, IconShape.Token, new Color(0.7f, 0.25f, 0.2f), 8));
            items.Add(Item("glyph_fragment", ItemType.Quest, Rarity.Rare, IconShape.Shard, new Color(0.5f, 0.85f, 1f), 0, 9));
            items.Add(Item("memory_shard", ItemType.Quest, Rarity.Epic, IconShape.Shard, new Color(0.15f, 0.15f, 0.2f), 0, 1));
            items.Add(Item("elder_letter", ItemType.Quest, Rarity.Common, IconShape.Scroll, new Color(0.9f, 0.85f, 0.7f), 0, 1));
            items.Add(Item("hunter_token", ItemType.Quest, Rarity.Common, IconShape.Token, new Color(0.6f, 0.45f, 0.25f), 0, 1));

            // consumables
            items.Add(Consumable("berry_wild", Rarity.Common, IconShape.Berry, new Color(0.75f, 0.2f, 0.4f), 2, 14, 0, 12));
            items.Add(Consumable("roast_meat", Rarity.Common, IconShape.Fang, new Color(0.8f, 0.5f, 0.3f), 6, 38, 0, 25));
            items.Add(Consumable("healing_paste", Rarity.Common, IconShape.Paste, new Color(0.85f, 0.4f, 0.4f), 12, 60, 0, 0));
            items.Add(Consumable("stamina_tonic", Rarity.Common, IconShape.Flask, new Color(0.95f, 0.85f, 0.4f), 12, 0, 0, 80));
            items.Add(Consumable("qi_pill_low", Rarity.Fine, IconShape.Pill, new Color(0.45f, 0.75f, 1f), 30, 0, 70, 0));
            items.Add(Consumable("body_paste", Rarity.Fine, IconShape.Paste, new Color(0.9f, 0.7f, 0.35f), 40, 0, 0, 0));
            items.Add(Consumable("foundation_pill", Rarity.Rare, IconShape.Pill, new Color(0.95f, 0.85f, 0.5f), 220, 0, 0, 0));
            items.Add(Consumable("golden_core_elixir", Rarity.Epic, IconShape.Flask, gold, 900, 0, 0, 0));
            items.Add(Consumable("nascent_soul_fruit", Rarity.Legendary, IconShape.Berry, new Color(0.7f, 0.5f, 1f), 3000, 0, 0, 0));

            // manuals
            items.Add(Manual("palm_shock", 80, Rarity.Fine));
            items.Add(Manual("iron_body", 90, Rarity.Fine));
            items.Add(Manual("sword_wave", 80, Rarity.Fine));
            items.Add(Manual("sword_rain", 260, Rarity.Rare));
            items.Add(Manual("fire_talisman", 80, Rarity.Fine));
            items.Add(Manual("frost_ring", 240, Rarity.Rare));
            items.Add(Manual("thunder_call", 280, Rarity.Rare));
            items.Add(Manual("swift_step", 70, Rarity.Fine));
            items.Add(Manual("mend", 90, Rarity.Fine));
            items.Add(Manual("gather_qi", 90, Rarity.Fine));
            items.Add(Manual("qi_bolt", 60, Rarity.Fine));

            // weapons
            items.Add(Weapon("branch_staff", WeaponFamily.Staff, 4, Rarity.Common, IconShape.Staff, wood, 2, 0));
            items.Add(Weapon("rusty_sword", WeaponFamily.Sword, 7, Rarity.Common, IconShape.Sword, new Color(0.55f, 0.5f, 0.45f), 15, 0));
            items.Add(Weapon("hunter_spear", WeaponFamily.Spear, 9, Rarity.Common, IconShape.Spear, wood, 22, 0));
            items.Add(Weapon("bronze_knuckles", WeaponFamily.Fist, 8, Rarity.Common, IconShape.Fist, new Color(0.75f, 0.55f, 0.3f), 20, 0));
            items.Add(Weapon("iron_sword", WeaponFamily.Sword, 13, Rarity.Fine, IconShape.Sword, steel, 60, 1));
            items.Add(Weapon("bandit_saber", WeaponFamily.Blade, 15, Rarity.Fine, IconShape.Blade, new Color(0.65f, 0.65f, 0.7f), 70, 1));
            items.Add(Weapon("oak_staff", WeaponFamily.Staff, 9, Rarity.Fine, IconShape.Staff, new Color(0.45f, 0.6f, 0.35f), 65, 1, M(StatId.SpellPower, 7)));
            items.Add(Weapon("jade_sword", WeaponFamily.Sword, 24, Rarity.Rare, IconShape.Sword, jade, 320, 2, M(StatId.CritChance, 0.05f)));
            items.Add(Weapon("spirit_gauntlets", WeaponFamily.Fist, 27, Rarity.Rare, IconShape.Fist, new Color(0.55f, 0.75f, 0.95f), 340, 2, M(StatId.Poise, 20)));
            items.Add(Weapon("crane_staff", WeaponFamily.Staff, 18, Rarity.Rare, IconShape.Staff, new Color(0.95f, 0.95f, 0.85f), 360, 2, M(StatId.SpellPower, 26), M(StatId.MaxQi, 40)));
            items.Add(Weapon("ruin_blade", WeaponFamily.Blade, 38, Rarity.Epic, IconShape.Blade, new Color(0.5f, 0.85f, 1f), 900, 2, M(StatId.CritDamage, 0.2f)));

            // armor and accessories
            var cloth = new Color(0.62f, 0.56f, 0.46f);
            var leather = new Color(0.45f, 0.3f, 0.2f);
            items.Add(Gear("cloth_hood", EquipSlot.Head, Rarity.Common, IconShape.Hood, cloth, 8, 0, cloth, M(StatId.Defense, 1.5f)));
            items.Add(Gear("bamboo_hat", EquipSlot.Head, Rarity.Common, IconShape.Hood, new Color(0.85f, 0.75f, 0.45f), 14, 0, new Color(0.85f, 0.75f, 0.45f), M(StatId.Defense, 2f), P(StatId.MoveSpeed, 0.02f)));
            items.Add(Gear("sect_headband", EquipSlot.Head, Rarity.Rare, IconShape.Hood, blue, 180, 2, blue, M(StatId.MaxQi, 25), M(StatId.Defense, 3f)));
            items.Add(Gear("hemp_robe", EquipSlot.Body, Rarity.Common, IconShape.Robe, cloth, 12, 0, cloth, M(StatId.Defense, 3f), M(StatId.MaxHp, 10)));
            items.Add(Gear("leather_vest", EquipSlot.Body, Rarity.Fine, IconShape.Robe, leather, 55, 1, leather, M(StatId.Defense, 6f), M(StatId.MaxHp, 20)));
            items.Add(Gear("sect_robe", EquipSlot.Body, Rarity.Rare, IconShape.Robe, new Color(0.85f, 0.9f, 0.95f), 260, 2, new Color(0.85f, 0.9f, 0.95f), M(StatId.Defense, 9f), M(StatId.MaxQi, 35), M(StatId.MaxHp, 30)));
            items.Add(Gear("straw_sandals", EquipSlot.Boots, Rarity.Common, IconShape.Boots, new Color(0.8f, 0.7f, 0.4f), 5, 0, new Color(0.8f, 0.7f, 0.4f), M(StatId.Defense, 0.5f), P(StatId.MoveSpeed, 0.02f)));
            items.Add(Gear("leather_boots", EquipSlot.Boots, Rarity.Fine, IconShape.Boots, leather, 45, 1, leather, M(StatId.Defense, 2.5f), M(StatId.MaxStamina, 10)));
            items.Add(Gear("cloud_boots", EquipSlot.Boots, Rarity.Rare, IconShape.Boots, new Color(0.9f, 0.92f, 0.98f), 250, 2, new Color(0.9f, 0.92f, 0.98f), M(StatId.Defense, 4f), P(StatId.MoveSpeed, 0.06f), M(StatId.MaxStamina, 20)));
            items.Add(Gear("copper_ring", EquipSlot.Ring, Rarity.Common, IconShape.Ring, new Color(0.85f, 0.55f, 0.3f), 20, 0, c, M(StatId.Attack, 2)));
            items.Add(Gear("jade_ring", EquipSlot.Ring, Rarity.Rare, IconShape.Ring, jade, 280, 2, c, M(StatId.QiRegen, 0.4f), M(StatId.MaxQi, 20)));
            items.Add(Gear("wolf_fang_charm", EquipSlot.Pendant, Rarity.Fine, IconShape.Fang, new Color(0.92f, 0.9f, 0.82f), 50, 0, c, M(StatId.CritChance, 0.03f), M(StatId.Attack, 2)));
            items.Add(Gear("jade_pendant", EquipSlot.Pendant, Rarity.Rare, IconShape.Pendant, jade, 200, 1, c, M(StatId.HpRegen, 0.5f), M(StatId.Defense, 2f)));
            items.Add(Gear("memory_shard_pendant", EquipSlot.Pendant, Rarity.Epic, IconShape.Shard, new Color(0.35f, 0.4f, 0.6f), 0, 0, c, M(StatId.SpellPower, 10), P(StatId.ExpGain, 0.1f)));
            return items.ToArray();
        }

        // ------------------------------------------------------------------ skills
        static SkillDef Skill(string id, SkillKind kind, DamageType el, CultivationPath path, float qi, float cd, float dmg, bool spell,
            string clip, string vfx, int reqRealm, IconShape icon, Color color)
        {
            var d = New<SkillDef>();
            d.name = id;
            d.id = id;
            d.nameKey = "skill." + id + ".name";
            d.descKey = "skill." + id + ".desc";
            d.kind = kind;
            d.element = el;
            d.path = path;
            d.qiCost = qi;
            d.cooldown = cd;
            d.damageMul = dmg;
            d.useSpellPower = spell;
            d.clip = clip;
            d.vfx = vfx;
            d.reqRealm = reqRealm;
            d.icon = icon;
            d.color = color;
            return d;
        }

        public static SkillDef[] BuildSkills()
        {
            var list = new List<SkillDef>();
            SkillDef s;

            s = Skill("palm_shock", SkillKind.Cone, DamageType.Qi, CultivationPath.Wu, 18, 5f, 2.3f, false, "cast_palm", "shockwave", 1, IconShape.Fist, new Color(1f, 0.7f, 0.35f));
            s.radius = 5.2f; s.range = 5.2f; s.poiseDamage = 50; s.knockback = 8f; s.castTime = 0.3f; s.duration = 0.7f; s.sfx = "heavy_punch";
            list.Add(s);

            s = Skill("iron_body", SkillKind.Buff, DamageType.Physical, CultivationPath.Wu, 22, 24f, 0f, false, "cast_buff", "aura_gold", 1, IconShape.Robe, new Color(1f, 0.85f, 0.4f));
            s.buffDuration = 12f; s.buffMods = new[] { P(StatId.Defense, 0.45f), M(StatId.Poise, 60) }; s.castTime = 0.25f; s.duration = 0.8f;
            list.Add(s);

            s = Skill("sword_wave", SkillKind.Projectile, DamageType.Qi, CultivationPath.Jian, 16, 4f, 1.9f, false, "cast_slash", "sword_wave", 1, IconShape.Sword, new Color(0.55f, 0.9f, 1f));
            s.speed = 28f; s.range = 22f; s.poiseDamage = 24; s.knockback = 4f; s.radius = 1.4f; s.castTime = 0.22f; s.duration = 0.55f; s.sfx = "sword_wave";
            list.Add(s);

            s = Skill("sword_rain", SkillKind.Barrage, DamageType.Qi, CultivationPath.Jian, 40, 16f, 1.1f, false, "cast_up", "sword_rain", 2, IconShape.Sword, new Color(0.7f, 0.95f, 1f));
            s.count = 8; s.radius = 4.5f; s.range = 16f; s.castTime = 0.5f; s.duration = 1.1f; s.poiseDamage = 14;
            list.Add(s);

            s = Skill("fire_talisman", SkillKind.Projectile, DamageType.Fire, CultivationPath.Fa, 14, 3f, 1.7f, true, "cast_forward", "fireball", 1, IconShape.Scroll, new Color(1f, 0.5f, 0.2f));
            s.speed = 20f; s.range = 26f; s.radius = 2.2f; s.poiseDamage = 24; s.status = "burn"; s.statusDuration = 4f; s.statusPower = 0.25f; s.castTime = 0.3f; s.duration = 0.6f; s.sfx = "fire_cast";
            list.Add(s);

            s = Skill("frost_ring", SkillKind.Nova, DamageType.Ice, CultivationPath.Fa, 28, 12f, 1.4f, true, "cast_slam", "frost_nova", 2, IconShape.Orb, new Color(0.6f, 0.85f, 1f));
            s.radius = 6.5f; s.status = "slow"; s.statusDuration = 4f; s.statusPower = 0.5f; s.castTime = 0.4f; s.duration = 0.9f; s.poiseDamage = 30;
            list.Add(s);

            s = Skill("thunder_call", SkillKind.Strike, DamageType.Lightning, CultivationPath.Fa, 35, 14f, 3.2f, true, "cast_up", "lightning", 2, IconShape.Orb, new Color(0.85f, 0.8f, 1f));
            s.range = 20f; s.radius = 2.8f; s.status = "stun"; s.statusDuration = 1.2f; s.castTime = 0.5f; s.duration = 1.0f; s.poiseDamage = 80; s.sfx = "thunder";
            list.Add(s);

            s = Skill("qi_bolt", SkillKind.Projectile, DamageType.Qi, CultivationPath.Hybrid, 10, 2.2f, 1.25f, true, "cast_forward", "qi_bolt", 1, IconShape.Orb, new Color(0.5f, 0.85f, 1f));
            s.speed = 24f; s.range = 24f; s.radius = 1.1f; s.poiseDamage = 14; s.castTime = 0.22f; s.duration = 0.5f;
            list.Add(s);

            s = Skill("swift_step", SkillKind.Dash, DamageType.Physical, CultivationPath.None, 12, 5f, 0f, false, "cast_dash", "dash_trail", 1, IconShape.Boots, new Color(0.85f, 0.95f, 1f));
            s.range = 7f; s.duration = 0.35f; s.castTime = 0.05f;
            list.Add(s);

            s = Skill("mend", SkillKind.Heal, DamageType.Physical, CultivationPath.None, 30, 20f, 0f, false, "cast_buff", "heal_glow", 1, IconShape.Herb, new Color(0.5f, 1f, 0.6f));
            s.healAmount = 0.35f; s.castTime = 0.3f; s.duration = 1.0f;
            list.Add(s);

            s = Skill("gather_qi", SkillKind.QiRestore, DamageType.Physical, CultivationPath.None, 0, 28f, 0f, false, "cast_buff", "aura_blue", 1, IconShape.Pill, new Color(0.5f, 0.75f, 1f));
            s.healAmount = 60f; s.castTime = 0.3f; s.duration = 1.2f;
            list.Add(s);
            return list.ToArray();
        }

        // ------------------------------------------------------------------ enemies
        static EnemyDef Enemy(string id, EnemyRigKind rig, float hp, float atk, float def, float poise, float walk, float run, float exp, int stones, string loot, params AttackDef[] attacks)
        {
            var d = New<EnemyDef>();
            d.name = id;
            d.id = id;
            d.nameKey = "enemy." + id + ".name";
            d.rig = rig;
            d.maxHp = hp;
            d.attack = atk;
            d.defense = def;
            d.poise = poise;
            d.walkSpeed = walk;
            d.runSpeed = run;
            d.exp = exp;
            d.spiritStones = stones;
            d.lootTable = loot;
            d.attacks = attacks;
            return d;
        }

        public static EnemyDef[] BuildEnemies()
        {
            var list = new List<EnemyDef>();
            EnemyDef e;

            e = Enemy("wolf", EnemyRigKind.Wolf, 46, 9, 2, 24, 1.8f, 6.2f, 14, 1, "loot_wolf",
                E("bite", "wolf_bite", AttackKind.Melee, 0.8f, 0.3f, 0.42f, 1.0f, 16, 2.5f, 2.0f, 90, 0f, 2.2f, 1.2f, 3f),
                E("pounce", "wolf_pounce", AttackKind.Leap, 1.05f, 0.45f, 0.7f, 1.35f, 30, 4f, 2.2f, 80, 4f, 8f, 4.5f, 1.2f));
            e.appearance.primary = new Color(0.45f, 0.45f, 0.47f); e.appearance.secondary = new Color(0.72f, 0.72f, 0.72f); e.appearance.skin = new Color(0.12f, 0.12f, 0.13f);
            e.sightRange = 20f; e.hearRange = 12f; e.preferredRange = 2f; e.circleChance = 0.55f; e.hasWeapon = false; e.scale = 0.95f;
            list.Add(e);

            e = Enemy("wolf_pup", EnemyRigKind.Wolf, 24, 5, 1, 10, 1.6f, 5.6f, 6, 0, "loot_wolf_small",
                E("bite", "wolf_bite", AttackKind.Melee, 0.75f, 0.28f, 0.4f, 1.0f, 8, 1.5f, 1.8f, 90, 0f, 2.0f, 1.3f));
            e.appearance.primary = new Color(0.55f, 0.52f, 0.5f); e.appearance.secondary = new Color(0.8f, 0.78f, 0.75f); e.appearance.skin = new Color(0.14f, 0.13f, 0.13f);
            e.scale = 0.65f; e.hasWeapon = false; e.circleChance = 0.4f;
            list.Add(e);

            e = Enemy("boar", EnemyRigKind.Boar, 95, 12, 5, 70, 1.5f, 5.0f, 28, 2, "loot_boar",
                E("gore", "wolf_bite", AttackKind.Melee, 0.9f, 0.38f, 0.5f, 1.0f, 28, 5f, 2.1f, 80, 0f, 2.4f, 1.8f),
                E("charge", "wolf_pounce", AttackKind.Charge, 1.3f, 0.5f, 1.0f, 1.6f, 55, 7f, 2.0f, 70, 5f, 14f, 6f, 1.2f));
            e.appearance.primary = new Color(0.34f, 0.26f, 0.2f); e.appearance.secondary = new Color(0.55f, 0.45f, 0.35f); e.appearance.skin = new Color(0.2f, 0.15f, 0.12f);
            e.hasWeapon = false; e.preferredRange = 2f; e.circleChance = 0.1f; e.scale = 1.1f;
            list.Add(e);

            e = Enemy("bandit", EnemyRigKind.Humanoid, 72, 12, 4, 40, 1.7f, 4.4f, 24, 3, "loot_bandit",
                E("slash", "sword_l1", AttackKind.Melee, 0.9f, 0.4f, 0.52f, 1.0f, 22, 3f, 2.5f, 110, 0f, 2.8f, 1.4f),
                E("combo", "sword_l3", AttackKind.Melee, 1.2f, 0.55f, 0.68f, 1.4f, 38, 5f, 2.8f, 80, 0f, 3.2f, 3.5f, 0.8f));
            e.weapon = WeaponFamily.Sword; e.preferredRange = 2.2f; e.circleChance = 0.4f;
            e.appearance = new CharacterAppearance { outfit = OutfitStyle.Rags, hairStyle = HairStyle.Messy, primary = new Color(0.45f, 0.25f, 0.2f), secondary = new Color(0.25f, 0.2f, 0.18f), accent = new Color(0.6f, 0.15f, 0.12f), accessories = Accessory.Headband };
            list.Add(e);

            e = Enemy("bandit_archer", EnemyRigKind.Humanoid, 52, 11, 2, 22, 1.7f, 4.2f, 22, 3, "loot_bandit",
                E("shoot", "bow_shoot", AttackKind.Projectile, 1.2f, 0.7f, 0.75f, 1.0f, 12, 2f, 18f, 30, 5f, 20f, 2.2f),
                E("stab", "sword_l1", AttackKind.Melee, 0.9f, 0.4f, 0.52f, 0.9f, 16, 3f, 2.2f, 100, 0f, 2.4f, 1.6f, 0.6f));
            e.weapon = WeaponFamily.Sword; e.preferredRange = 12f; e.circleChance = 0.2f; e.sightRange = 24f;
            e.attacks[0].speed = 26f; e.attacks[0].vfx = "arrow";
            e.appearance = new CharacterAppearance { outfit = OutfitStyle.Leather, hairStyle = HairStyle.Short, primary = new Color(0.38f, 0.3f, 0.2f), secondary = new Color(0.25f, 0.3f, 0.2f), accent = new Color(0.6f, 0.15f, 0.12f), accessories = Accessory.Mask };
            list.Add(e);

            e = Enemy("cultist", EnemyRigKind.Humanoid, 64, 11, 3, 30, 1.6f, 3.8f, 34, 4, "loot_cultist",
                E("shadow_bolt", "cast_forward", AttackKind.Projectile, 1.3f, 0.7f, 0.75f, 1.1f, 20, 3f, 18f, 30, 5f, 20f, 2.8f),
                E("shadow_volley", "cast_up", AttackKind.Barrage, 1.8f, 0.9f, 1.1f, 0.8f, 18, 2f, 16f, 90, 6f, 20f, 7f, 0.7f),
                E("curse_touch", "staff_l1", AttackKind.Melee, 1.0f, 0.45f, 0.55f, 1.0f, 24, 3f, 2.4f, 100, 0f, 2.6f, 2f, 0.5f));
            e.weapon = WeaponFamily.Staff; e.preferredRange = 10f; e.circleChance = 0.3f; e.sightRange = 22f; e.realm = 1;
            e.attacks[0].damageType = DamageType.Qi; e.attacks[0].vfx = "shadow_bolt"; e.attacks[0].speed = 17f;
            e.attacks[1].damageType = DamageType.Qi; e.attacks[1].vfx = "shadow_bolt"; e.attacks[1].count = 5; e.attacks[1].speed = 15f;
            e.appearance = new CharacterAppearance { outfit = OutfitStyle.BlackRobe, hairStyle = HairStyle.Long, primary = new Color(0.12f, 0.1f, 0.14f), secondary = new Color(0.4f, 0.1f, 0.15f), accent = new Color(0.75f, 0.2f, 0.3f), accessories = Accessory.Talismans | Accessory.Mask };
            e.glow = new Color(0.8f, 0.15f, 0.3f);
            list.Add(e);

            e = Enemy("stone_guardian", EnemyRigKind.Golem, 420, 22, 14, 170, 1.2f, 2.8f, 150, 12, "loot_guardian",
                E("swipe", "golem_swipe", AttackKind.Melee, 1.5f, 0.7f, 0.9f, 1.2f, 60, 7f, 3.4f, 130, 0f, 3.8f, 2.5f),
                E("slam", "golem_slam", AttackKind.Slam, 2.0f, 1.1f, 1.3f, 1.9f, 90, 9f, 4.0f, 360, 0f, 5f, 6f, 1f));
            e.attacks[1].radius = 4.4f; e.attacks[1].telegraph = 0.9f;
            e.elite = true; e.scale = 1.55f; e.preferredRange = 3f; e.circleChance = 0.05f; e.realm = 1; e.stunResist = 0.6f; e.weapon = WeaponFamily.Fist; e.hasWeapon = false;
            e.appearance = new CharacterAppearance { outfit = OutfitStyle.Stone, hairStyle = HairStyle.None, skin = new Color(0.46f, 0.47f, 0.45f), primary = new Color(0.4f, 0.42f, 0.4f), secondary = new Color(0.3f, 0.32f, 0.3f), accent = new Color(0.45f, 0.85f, 1f), bulk = 1.4f };
            e.glow = new Color(0.4f, 0.8f, 1f);
            list.Add(e);

            e = Enemy("alpha_wolf", EnemyRigKind.Wolf, 560, 18, 7, 120, 2.2f, 7.0f, 420, 30, "loot_alpha",
                E("bite", "wolf_bite", AttackKind.Melee, 0.8f, 0.3f, 0.42f, 1.1f, 28, 4f, 2.8f, 100, 0f, 3.0f, 1.0f, 3f),
                E("pounce", "wolf_pounce", AttackKind.Leap, 1.0f, 0.42f, 0.7f, 1.5f, 50, 6f, 2.6f, 90, 4f, 10f, 4f, 1.4f),
                E("claw_sweep", "wolf_bite", AttackKind.Slam, 1.4f, 0.6f, 0.8f, 1.3f, 55, 7f, 3.6f, 360, 0f, 4f, 7f, 1f),
                E("howl", "wolf_howl", AttackKind.Summon, 2.2f, 1.0f, 1.2f, 0f, 0f, 0f, 0f, 0, 0f, 30f, 22f, 0.8f));
            e.attacks[2].radius = 3.6f; e.attacks[2].telegraph = 0.6f; e.attacks[3].spawn = "wolf_pup"; e.attacks[3].count = 3; e.attacks[3].minPhase = 1;
            e.boss = true; e.scale = 1.7f; e.preferredRange = 2.5f; e.circleChance = 0.4f; e.realm = 1; e.hasWeapon = false; e.sightRange = 30f; e.leashRange = 70f;
            e.phaseThresholds = new[] { 0.55f };
            e.appearance.primary = new Color(0.25f, 0.22f, 0.24f); e.appearance.secondary = new Color(0.55f, 0.5f, 0.5f); e.appearance.skin = new Color(0.08f, 0.07f, 0.08f);
            e.glow = new Color(1f, 0.15f, 0.1f);
            list.Add(e);
            return list.ToArray();
        }

        // ------------------------------------------------------------------ loot
        static LootTableDef Loot(string id, int rolls, float nothing, params LootEntry[] entries)
        {
            var d = New<LootTableDef>();
            d.name = id;
            d.id = id;
            d.rolls = rolls;
            d.nothingWeight = nothing;
            d.entries = entries;
            return d;
        }

        static LootEntry Chance(string id, float chance, int min, int max) { return new LootEntry(id, 0f, chance, min, max); }
        static LootEntry Weight(string id, float weight, int min = 1, int max = 1) { return new LootEntry(id, weight, 0f, min, max); }

        public static LootTableDef[] BuildLoot()
        {
            return new[]
            {
                Loot("loot_wolf", 1, 0f, Chance("wolf_pelt", 0.55f, 1, 2), Chance("wolf_fang", 0.3f, 1, 1), Chance("beast_core_low", 0.04f, 1, 1), Chance("spirit_stone", 0.25f, 1, 2)),
                Loot("loot_wolf_small", 1, 0f, Chance("wolf_pelt", 0.3f, 1, 1), Chance("berry_wild", 0.15f, 1, 2)),
                Loot("loot_boar", 1, 0f, Chance("roast_meat", 0.7f, 1, 2), Chance("wolf_pelt", 0.4f, 1, 1), Chance("spirit_stone", 0.3f, 1, 2)),
                Loot("loot_bandit", 1, 0.3f, Chance("spirit_stone", 0.8f, 2, 5), Chance("iron_ore", 0.3f, 1, 2), Chance("healing_paste", 0.25f, 1, 1), Chance("bandit_token", 0.45f, 1, 1),
                    Weight("rusty_sword", 3f), Weight("iron_sword", 1f), Weight("bandit_saber", 1f), Weight("leather_vest", 1f), Weight("copper_ring", 1f), Weight("bamboo_hat", 1.5f)),
                Loot("loot_cultist", 1, 0.2f, Chance("spirit_stone", 0.9f, 3, 7), Chance("qi_pill_low", 0.25f, 1, 1), Chance("manual_fire_talisman", 0.05f, 1, 1),
                    Weight("oak_staff", 1.5f), Weight("sect_headband", 0.4f), Weight("jade_ring", 0.2f), Weight("manual_qi_bolt", 0.5f)),
                Loot("loot_guardian", 2, 0.3f, Chance("spirit_stone", 1f, 8, 16), Chance("iron_ore", 0.8f, 2, 4), Chance("glyph_fragment", 0.2f, 1, 1),
                    Weight("jade_sword", 0.5f), Weight("spirit_gauntlets", 0.4f), Weight("crane_staff", 0.4f), Weight("cloud_boots", 0.4f), Weight("manual_frost_ring", 0.3f), Weight("manual_sword_rain", 0.3f), Weight("manual_thunder_call", 0.3f)),
                Loot("loot_alpha", 1, 0f, Chance("wolf_fang", 1f, 3, 4), Chance("beast_core_low", 1f, 2, 3), Chance("wolf_fang_charm", 1f, 1, 1), Chance("spirit_stone", 1f, 15, 25), Chance("jade_pendant", 0.25f, 1, 1)),
                Loot("chest_common", 2, 0.4f, Chance("spirit_stone", 0.9f, 3, 8), Weight("healing_paste", 2f, 1, 2), Weight("stamina_tonic", 1.5f), Weight("herb_basic", 2f, 2, 4), Weight("leather_boots", 0.6f), Weight("hemp_robe", 0.6f), Weight("qi_pill_low", 0.8f)),
                Loot("chest_rare", 2, 0.1f, Chance("spirit_stone", 1f, 20, 40), Weight("qi_pill_low", 2f, 1, 2), Weight("iron_sword", 1f), Weight("sect_robe", 0.5f), Weight("jade_pendant", 0.6f), Weight("manual_swift_step", 0.8f), Weight("manual_mend", 0.8f), Weight("manual_gather_qi", 0.8f), Weight("manual_palm_shock", 0.6f), Weight("manual_sword_wave", 0.6f), Weight("manual_fire_talisman", 0.6f)),
                Loot("herb_node", 1, 0f, Chance("herb_basic", 1f, 1, 3), Chance("berry_wild", 0.35f, 1, 2)),
                Loot("ore_node", 1, 0f, Chance("iron_ore", 1f, 1, 3), Chance("spirit_stone", 0.3f, 1, 2))
            };
        }
    }
}
