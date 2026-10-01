using UnityEngine;

namespace ThienKhuyet.Data
{
    /// <summary>Definition of an item. Created from code by ContentLibrary; designers can also author assets under Resources/Content.</summary>
    [CreateAssetMenu(menuName = "Thien Khuyet/Item", fileName = "Item")]
    public sealed class ItemDef : ScriptableObject
    {
        public string id;
        public string nameKey;
        public string descKey;
        public ItemType type;
        public Rarity rarity;
        public EquipSlot slot;
        public int maxStack = 99;
        public int value = 1;
        public IconShape icon = IconShape.Stone;
        public Color iconColor = Color.white;
        public StatMod[] mods = new StatMod[0];
        public WeaponFamily family;
        public float weaponDamage;
        public float restoreHp;
        public float restoreQi;
        public float restoreStamina;
        public string useFx = "";
        public int reqRealm;
        public string teachSkill = "";
        public Color tint = Color.white;
        public string visual = "";

        public bool Stackable => maxStack > 1;
        public bool IsEquipment => slot != EquipSlot.None;
        public bool IsUsable => type == ItemType.Consumable || type == ItemType.Manual;

        public static readonly Color[] RarityColors =
        {
            new Color(0.80f, 0.80f, 0.78f),
            new Color(0.45f, 0.85f, 0.55f),
            new Color(0.35f, 0.65f, 1.00f),
            new Color(0.72f, 0.45f, 1.00f),
            new Color(1.00f, 0.72f, 0.25f)
        };

        public Color RarityColor => RarityColors[Mathf.Clamp((int)rarity, 0, RarityColors.Length - 1)];
    }
}
