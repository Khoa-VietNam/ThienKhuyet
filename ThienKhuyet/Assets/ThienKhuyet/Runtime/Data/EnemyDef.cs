using UnityEngine;

namespace ThienKhuyet.Data
{
    [CreateAssetMenu(menuName = "Thien Khuyet/Enemy", fileName = "Enemy")]
    public sealed class EnemyDef : ScriptableObject
    {
        public string id;
        public string nameKey;
        public EnemyRigKind rig = EnemyRigKind.Humanoid;
        public CharacterAppearance appearance = new CharacterAppearance();
        public WeaponFamily weapon = WeaponFamily.Sword;
        public bool hasWeapon = true;
        public int realm;
        public float maxHp = 50f;
        public float attack = 8f;
        public float defense = 2f;
        public float poise = 30f;
        public float walkSpeed = 1.8f;
        public float runSpeed = 4.6f;
        public float sightRange = 18f;
        public float hearRange = 8f;
        public float fov = 140f;
        public float leashRange = 45f;
        public float scale = 1f;
        public float exp = 20f;
        public int spiritStones = 2;
        public string lootTable = "";
        public bool elite;
        public bool boss;
        public float preferredRange = 2f;
        public float circleChance = 0.35f;
        public float stunResist = 0f;
        public float[] phaseThresholds = new float[0];
        public AttackDef[] attacks = new AttackDef[0];
        public Color glow = Color.black;
    }
}
