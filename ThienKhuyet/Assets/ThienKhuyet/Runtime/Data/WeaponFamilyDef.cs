using UnityEngine;

namespace ThienKhuyet.Data
{
    /// <summary>Attack chains and guard properties shared by every weapon of a family (fists, sword, blade, spear, staff).</summary>
    [CreateAssetMenu(menuName = "Thien Khuyet/Weapon Family", fileName = "WeaponFamily")]
    public sealed class WeaponFamilyDef : ScriptableObject
    {
        public WeaponFamily family;
        public string nameKey;
        public AttackDef[] light = new AttackDef[0];
        public AttackDef heavy = new AttackDef();
        public float blockReduction = 0.7f;
        public float parryWindow = 0.22f;
        public float reach = 2.4f;
        public string guardClip = "guard_sword";
        public string idleClip = "idle";
    }
}
