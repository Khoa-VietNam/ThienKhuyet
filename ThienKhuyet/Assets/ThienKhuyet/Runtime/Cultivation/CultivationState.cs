using System;

namespace ThienKhuyet.Cultivation
{
    /// <summary>Realm/stage table abstraction so the state machine can be tested without ScriptableObjects.</summary>
    public interface IRealmTable
    {
        int RealmCount { get; }
        int StageCount(int realm);
        float ExpForStage(int realm, int stage);
    }

    /// <summary>
    /// Cultivation progress: realm (Cảnh Giới), minor stage (Tầng) and EXP. EXP auto-advances stages;
    /// at the last stage of a realm the bar fills and waits for an explicit breakthrough.
    /// </summary>
    public sealed class CultivationState
    {
        readonly IRealmTable table;

        public int Realm { get; private set; }
        public int Stage { get; private set; } = 1;
        public float Exp { get; private set; }

        public event Action<int, int, bool> Advanced; // realm, stage, wasBreakthrough

        public CultivationState(IRealmTable table)
        {
            this.table = table;
        }

        public bool AtLastRealm => Realm >= table.RealmCount - 1;
        public bool AtLastStage => Stage >= table.StageCount(Realm);
        public float ExpRequired => table.ExpForStage(Realm, Stage);
        public bool IsFull => Exp >= ExpRequired - 0.001f;
        public float Progress01 => ExpRequired <= 0f ? 1f : Math.Min(1f, Exp / ExpRequired);

        /// <summary>True when the bar is full at the last stage and a higher realm exists.</summary>
        public bool ReadyForBreakthrough => AtLastStage && IsFull && !AtLastRealm;

        public void Set(int realm, int stage, float exp)
        {
            Realm = Math.Max(0, Math.Min(realm, table.RealmCount - 1));
            Stage = Math.Max(1, Math.Min(stage, table.StageCount(Realm)));
            Exp = Math.Max(0f, Math.Min(exp, ExpRequired));
        }

        /// <summary>Adds EXP; returns the number of minor stages gained.</summary>
        public int AddExp(float amount)
        {
            if (amount <= 0f) return 0;
            int gained = 0;
            Exp += amount;
            while (Exp >= ExpRequired - 0.001f && !AtLastStage)
            {
                Exp -= ExpRequired;
                Stage++;
                gained++;
                Advanced?.Invoke(Realm, Stage, false);
            }
            if (AtLastStage && Exp > ExpRequired) Exp = ExpRequired;
            return gained;
        }

        /// <summary>Enters the next realm. Caller verifies requirements first.</summary>
        public bool Breakthrough()
        {
            if (!ReadyForBreakthrough) return false;
            Realm++;
            Stage = 1;
            Exp = 0f;
            Advanced?.Invoke(Realm, Stage, true);
            return true;
        }

        /// <summary>Failed breakthrough: lose part of the stored EXP.</summary>
        public void ApplyFailurePenalty(float fractionLost)
        {
            Exp = Math.Max(0f, Exp * (1f - Math.Max(0f, Math.Min(1f, fractionLost))));
        }
    }
}
