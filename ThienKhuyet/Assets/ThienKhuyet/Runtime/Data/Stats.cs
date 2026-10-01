using System;
using System.Collections.Generic;
using UnityEngine;

namespace ThienKhuyet.Data
{
    public enum StatId
    {
        MaxHp = 0,
        MaxStamina,
        MaxQi,
        Attack,
        Defense,
        SpellPower,
        CritChance,
        CritDamage,
        MoveSpeed,
        HpRegen,
        StaminaRegen,
        QiRegen,
        Poise,
        CooldownReduction,
        ExpGain,
        LootLuck,
        Count
    }

    public enum AttributeId
    {
        Vitality = 0,   // Thể Chất
        Strength,       // Sức Mạnh
        Spirit,         // Linh Lực
        Agility,        // Thân Pháp
        Insight,        // Ngộ Tính
        Count
    }

    [Serializable]
    public struct StatMod
    {
        public StatId stat;
        public float flat;
        public float pct;

        public StatMod(StatId stat, float flat, float pct = 0f)
        {
            this.stat = stat;
            this.flat = flat;
            this.pct = pct;
        }
    }

    /// <summary>
    /// Final stat = (base + sum(flat)) * (1 + sum(pct)). Modifiers are grouped by source id so equipment, realm,
    /// buffs and passives can be added/removed independently. Pure C# (unit-tested).
    /// </summary>
    public sealed class StatBlock
    {
        readonly float[] baseValues = new float[(int)StatId.Count];
        readonly float[] flat = new float[(int)StatId.Count];
        readonly float[] pct = new float[(int)StatId.Count];
        readonly Dictionary<string, StatMod[]> sources = new Dictionary<string, StatMod[]>();
        bool dirty = true;

        public event Action Changed;

        public void SetBase(StatId id, float value)
        {
            baseValues[(int)id] = value;
            Touch();
        }

        public float GetBase(StatId id)
        {
            return baseValues[(int)id];
        }

        public void SetSource(string sourceId, IList<StatMod> mods)
        {
            if (mods == null || mods.Count == 0)
            {
                RemoveSource(sourceId);
                return;
            }
            var arr = new StatMod[mods.Count];
            for (int i = 0; i < arr.Length; i++) arr[i] = mods[i];
            sources[sourceId] = arr;
            Touch();
        }

        public bool RemoveSource(string sourceId)
        {
            bool r = sources.Remove(sourceId);
            if (r) Touch();
            return r;
        }

        public bool HasSource(string sourceId)
        {
            return sources.ContainsKey(sourceId);
        }

        public void ClearSources()
        {
            if (sources.Count == 0) return;
            sources.Clear();
            Touch();
        }

        public float Get(StatId id)
        {
            if (dirty) Recompute();
            int i = (int)id;
            return (baseValues[i] + flat[i]) * (1f + pct[i]);
        }

        public float Flat(StatId id)
        {
            if (dirty) Recompute();
            return flat[(int)id];
        }

        void Touch()
        {
            dirty = true;
            Changed?.Invoke();
        }

        void Recompute()
        {
            Array.Clear(flat, 0, flat.Length);
            Array.Clear(pct, 0, pct.Length);
            foreach (var kv in sources)
            {
                StatMod[] arr = kv.Value;
                for (int i = 0; i < arr.Length; i++)
                {
                    flat[(int)arr[i].stat] += arr[i].flat;
                    pct[(int)arr[i].stat] += arr[i].pct;
                }
            }
            dirty = false;
        }
    }

    /// <summary>Player attributes and the stat bonuses each point gives.</summary>
    [Serializable]
    public sealed class AttributeSet
    {
        public int vitality = 5;
        public int strength = 5;
        public int spirit = 5;
        public int agility = 5;
        public int insight = 5;
        public int unspent;

        public int Get(AttributeId id)
        {
            switch (id)
            {
                case AttributeId.Vitality: return vitality;
                case AttributeId.Strength: return strength;
                case AttributeId.Spirit: return spirit;
                case AttributeId.Agility: return agility;
                default: return insight;
            }
        }

        public void Add(AttributeId id, int delta)
        {
            switch (id)
            {
                case AttributeId.Vitality: vitality += delta; break;
                case AttributeId.Strength: strength += delta; break;
                case AttributeId.Spirit: spirit += delta; break;
                case AttributeId.Agility: agility += delta; break;
                default: insight += delta; break;
            }
        }

        public bool Spend(AttributeId id)
        {
            if (unspent <= 0) return false;
            unspent--;
            Add(id, 1);
            return true;
        }

        /// <summary>Stat modifiers granted by the attribute points above the starting value of 5.</summary>
        public StatMod[] ToMods()
        {
            return new[]
            {
                new StatMod(StatId.MaxHp, vitality * 9f),
                new StatMod(StatId.Defense, vitality * 0.9f + strength * 0.2f),
                new StatMod(StatId.Attack, strength * 1.6f + agility * 0.3f),
                new StatMod(StatId.Poise, strength * 0.8f + vitality * 0.5f),
                new StatMod(StatId.MaxQi, spirit * 7f),
                new StatMod(StatId.SpellPower, spirit * 1.7f + insight * 0.3f),
                new StatMod(StatId.QiRegen, spirit * 0.05f + insight * 0.03f),
                new StatMod(StatId.MaxStamina, agility * 3f),
                new StatMod(StatId.CritChance, agility * 0.004f),
                new StatMod(StatId.MoveSpeed, 0f, agility * 0.004f),
                new StatMod(StatId.StaminaRegen, agility * 0.3f),
                new StatMod(StatId.ExpGain, 0f, insight * 0.015f),
                new StatMod(StatId.CooldownReduction, insight * 0.006f),
            };
        }

        public static string BuildName(AttributeSet a, bool wieldsMagic, bool wieldsSword, bool unarmed)
        {
            float body = a.vitality + a.strength;
            float spell = a.spirit * 1.5f;
            if (unarmed && body > spell * 0.9f) return "wu";
            if (wieldsMagic && spell >= body * 0.8f) return "fa";
            if (wieldsSword && a.agility + a.strength >= spell * 0.9f) return "jian";
            return "hybrid";
        }
    }
}
