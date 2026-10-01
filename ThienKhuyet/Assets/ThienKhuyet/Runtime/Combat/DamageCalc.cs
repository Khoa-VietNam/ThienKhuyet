using System;
using ThienKhuyet.Data;
using UnityEngine;

namespace ThienKhuyet.Combat
{
    public enum Team { Neutral = 0, Player = 1, Enemy = 2 }

    /// <summary>Everything the receiver needs to resolve one hit.</summary>
    [Serializable]
    public struct DamageInfo
    {
        public float power;            // already includes attack stat and motion multiplier
        public DamageType type;
        public Team team;              // attacker team
        public GameObject source;
        public Vector3 point;
        public Vector3 direction;      // direction the hit travels (attacker -> victim)
        public float poiseDamage;
        public float knockback;
        public bool unblockable;
        public bool heavy;
        public float critChance;
        public float critDamage;       // extra multiplier on crit (0.5 = +50%)
        public int attackerRealm;
        public string tag;             // e.g. "light", "skill:sword_wave"
        public bool silent;            // no hit-stop / shake (DOT, environment)
    }

    public struct DamageResult
    {
        public float dealt;
        public bool crit;
        public bool blocked;
        public bool parried;
        public bool dodged;
        public bool killed;
        public bool staggered;
        public bool ignored;
    }

    /// <summary>Pure damage formulas (unit-tested).</summary>
    public static class DamageCalc
    {
        /// <summary>Defense uses a diminishing-returns curve: 100 defense halves damage.</summary>
        public static float Mitigate(float raw, float defense)
        {
            if (defense <= 0f) return raw * (1f + Mathf.Min(0.5f, -defense * 0.01f));
            return raw * (100f / (100f + defense));
        }

        /// <summary>Cultivation realm gap: +/-20% damage per realm, clamped. Cultivation really matters in combat.</summary>
        public static float RealmFactor(int attackerRealm, int defenderRealm)
        {
            int gap = attackerRealm - defenderRealm;
            return Mathf.Clamp(1f + 0.2f * gap, 0.55f, 1.9f);
        }

        public static float TypeFactor(DamageType type, float resist)
        {
            if (type == DamageType.True) return 1f;
            return Mathf.Clamp(1f - resist, 0.1f, 2f);
        }

        public static float CritMultiplier(bool crit, float critDamage)
        {
            return crit ? 1.5f + Mathf.Max(0f, critDamage) : 1f;
        }

        public static float Final(float power, float defense, int attackerRealm, int defenderRealm, DamageType type, float resist, bool crit, float critDamage, float guardReduction)
        {
            float d = Mitigate(power, defense);
            if (type == DamageType.True) d = power;
            d *= RealmFactor(attackerRealm, defenderRealm);
            d *= TypeFactor(type, resist);
            d *= CritMultiplier(crit, critDamage);
            d *= 1f - Mathf.Clamp01(guardReduction);
            return Mathf.Max(0f, d);
        }
    }
}
