using System;
using UnityEngine;

namespace ThienKhuyet.Data
{
    public enum ItemType { Weapon = 0, Armor, Accessory, Consumable, Material, Quest, Manual }

    public enum Rarity { Common = 0, Fine, Rare, Epic, Legendary }

    public enum EquipSlot { None = 0, Weapon, Head, Body, Boots, Ring, Pendant }

    public enum WeaponFamily { Fist = 0, Sword, Blade, Spear, Staff }

    public enum DamageType { Physical = 0, Fire, Ice, Lightning, Qi, True }

    /// <summary>Cultivation path (Đạo Lộ) chosen after the first breakthrough.</summary>
    public enum CultivationPath { None = 0, Wu = 1, Jian = 2, Fa = 3, Hybrid = 4 }

    public enum IconShape
    {
        Sword, Blade, Spear, Fist, Staff, Hood, Robe, Boots, Ring, Pendant, Herb, Pill, Gem, Book, Pelt, Fang,
        Ore, Stone, Shard, Scroll, Berry, Paste, Orb, Key, Token, Flask
    }

    public enum SkillKind { Projectile = 0, Cone, Nova, Strike, Buff, Heal, Dash, QiRestore, Barrage, Slam }

    public enum EnemyRigKind { Wolf = 0, Humanoid, Golem, Boar, Entity }

    public enum AttackKind { Melee = 0, Projectile, Slam, Charge, Leap, Barrage, Beam, Summon }

    public enum HairStyle { None = 0, Topknot, Long, Short, Bun, Ponytail, Messy, Bald }

    public enum OutfitStyle { Robe = 0, Tunic, Leather, Armor, Modern, Rags, BlackRobe, Stone, Wanderer }

    [Flags]
    public enum Accessory
    {
        None = 0,
        Hat = 1,
        Mask = 2,
        Backpack = 4,
        Headband = 8,
        ShoulderPads = 16,
        Talismans = 32,
        Beard = 64,
        Cape = 128,
        Glasses = 256,
        Horns = 512
    }

    /// <summary>Colors and silhouette parameters of a procedural humanoid.</summary>
    [Serializable]
    public sealed class CharacterAppearance
    {
        public Color skin = new Color(0.86f, 0.69f, 0.56f);
        public Color hair = new Color(0.07f, 0.06f, 0.07f);
        public Color primary = new Color(0.86f, 0.86f, 0.82f);
        public Color secondary = new Color(0.22f, 0.36f, 0.40f);
        public Color accent = new Color(0.78f, 0.62f, 0.25f);
        public Color pants = new Color(0.20f, 0.22f, 0.25f);
        public HairStyle hairStyle = HairStyle.Topknot;
        public OutfitStyle outfit = OutfitStyle.Robe;
        public Accessory accessories = Accessory.None;
        public float height = 1f;
        public float bulk = 1f;

        public CharacterAppearance Clone()
        {
            return (CharacterAppearance)MemberwiseClone();
        }
    }

    /// <summary>One attack of a combo chain, an enemy move or a boss pattern.</summary>
    [Serializable]
    public sealed class AttackDef
    {
        public string id = "attack";
        public string clip = "sword_l1";
        public AttackKind kind = AttackKind.Melee;
        public float duration = 0.7f;
        public float hitStart = 0.25f;
        public float hitEnd = 0.4f;
        public float cancelFrom = 0.45f;
        public float damageMul = 1f;
        public float poiseDamage = 20f;
        public float knockback = 3f;
        public float range = 2.4f;
        public float arc = 110f;
        public float lunge = 0.9f;
        public float staminaCost = 10f;
        public float qiGain = 2f;
        public float moveSpeedMul = 0.25f;
        public bool heavy;
        public bool unblockable;
        public DamageType damageType = DamageType.Physical;
        public string vfx = "slash";
        public string sfx = "swing";
        // enemy-only AI parameters
        public float minRange = 0f;
        public float maxRange = 2.6f;
        public float cooldown = 1.5f;
        public float weight = 1f;
        public float speed = 14f;       // projectile speed / charge speed
        public float radius = 3f;       // AoE radius for slam / beam width
        public int count = 1;           // projectiles / summons
        public string spawn = "";       // enemy id spawned by Summon
        public float telegraph = 0f;    // visual warning seconds before hitStart (0 = use hitStart)
        public int minPhase = 0;        // boss phase gate
    }
}
