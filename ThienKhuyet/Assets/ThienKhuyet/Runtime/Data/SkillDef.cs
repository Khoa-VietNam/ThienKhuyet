using UnityEngine;

namespace ThienKhuyet.Data
{
    /// <summary>Active technique (Công Pháp). Costs Qi, has a cooldown and a realm requirement.</summary>
    [CreateAssetMenu(menuName = "Thien Khuyet/Skill", fileName = "Skill")]
    public sealed class SkillDef : ScriptableObject
    {
        public string id;
        public string nameKey;
        public string descKey;
        public SkillKind kind;
        public DamageType element = DamageType.Qi;
        public CultivationPath path = CultivationPath.None;
        public float qiCost = 20f;
        public float cooldown = 6f;
        public float damageMul = 1.5f;
        public bool useSpellPower;
        public float range = 12f;
        public float radius = 3f;
        public float speed = 22f;
        public float castTime = 0.35f;
        public float duration = 0.8f;
        public string clip = "cast_forward";
        public string vfx = "qi_bolt";
        public string sfx = "cast";
        public int reqRealm = 1;
        public int count = 1;
        public float poiseDamage = 25f;
        public float knockback = 4f;
        public float healAmount;
        public float buffDuration;
        public StatMod[] buffMods = new StatMod[0];
        public string status = "";
        public float statusDuration;
        public float statusPower;
        public IconShape icon = IconShape.Orb;
        public Color color = new Color(0.4f, 0.8f, 1f);
    }
}
