using System;
using System.Collections.Generic;
using ThienKhuyet.Core;
using UnityEngine;

namespace ThienKhuyet.Data
{
    [Serializable]
    public struct LootEntry
    {
        public string itemId;
        /// <summary>Weight used in weighted rolls (when chance is 0).</summary>
        public float weight;
        /// <summary>Independent drop probability (0 = weighted entry).</summary>
        public float chance;
        public int min;
        public int max;

        public LootEntry(string itemId, float weight, float chance, int min, int max)
        {
            this.itemId = itemId;
            this.weight = weight;
            this.chance = chance;
            this.min = min;
            this.max = max;
        }
    }

    [CreateAssetMenu(menuName = "Thien Khuyet/Loot Table", fileName = "LootTable")]
    public sealed class LootTableDef : ScriptableObject
    {
        public string id;
        /// <summary>How many weighted rolls are made.</summary>
        public int rolls = 1;
        /// <summary>Weight of "nothing" in weighted rolls.</summary>
        public float nothingWeight = 0f;
        public LootEntry[] entries = new LootEntry[0];

        /// <summary>Rolls drops into 'result'. Luck (0..1+) increases independent chances.</summary>
        public void Roll(ref Rng rng, float luck, List<ItemCost> result)
        {
            LootRoller.Roll(entries, rolls, nothingWeight, ref rng, luck, result);
        }
    }

    public static class LootRoller
    {
        public static void Roll(LootEntry[] entries, int rolls, float nothingWeight, ref Rng rng, float luck, List<ItemCost> result)
        {
            float totalWeight = nothingWeight;
            for (int i = 0; i < entries.Length; i++)
            {
                if (entries[i].chance > 0f)
                {
                    if (rng.Value() < Math.Min(1f, entries[i].chance * (1f + luck)))
                        result.Add(new ItemCost(entries[i].itemId, rng.Int(Math.Max(1, entries[i].min), Math.Max(entries[i].min, entries[i].max))));
                }
                else totalWeight += Math.Max(0f, entries[i].weight);
            }
            if (totalWeight <= 0f) return;
            for (int r = 0; r < rolls; r++)
            {
                float pick = rng.Value() * totalWeight;
                pick -= nothingWeight;
                if (pick < 0f) continue;
                for (int i = 0; i < entries.Length; i++)
                {
                    if (entries[i].chance > 0f) continue;
                    pick -= Math.Max(0f, entries[i].weight);
                    if (pick <= 0f)
                    {
                        result.Add(new ItemCost(entries[i].itemId, rng.Int(Math.Max(1, entries[i].min), Math.Max(entries[i].min, entries[i].max))));
                        break;
                    }
                }
            }
        }
    }
}
