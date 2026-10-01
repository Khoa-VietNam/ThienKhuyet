using System;
using UnityEngine;

namespace ThienKhuyet.Data
{
    [Serializable]
    public struct ItemCost
    {
        public string itemId;
        public int count;

        public ItemCost(string itemId, int count)
        {
            this.itemId = itemId;
            this.count = count;
        }
    }

    /// <summary>
    /// A cultivation realm (Cảnh Giới) with minor stages (Tầng). Reaching the last stage and filling its EXP bar
    /// allows a breakthrough (Đột Phá) into the next realm if the requirements are met.
    /// </summary>
    [CreateAssetMenu(menuName = "Thien Khuyet/Realm", fileName = "Realm")]
    public sealed class RealmDef : ScriptableObject
    {
        public int index;
        public string nameKey;
        public string descKey;
        public int stages = 1;
        public float[] expPerStage = { 100f };
        public StatMod[] realmMods = new StatMod[0];
        public StatMod[] stageMods = new StatMod[0];
        public int attributePointsPerStage = 2;
        public int attributePointsOnBreakthrough = 5;

        // Requirements to break through INTO the next realm.
        public ItemCost[] breakthroughItems = new ItemCost[0];
        public float breakthroughQiPercent = 0.5f;
        public float breakthroughBaseChance = 1f;
        public string breakthroughFlag = "";
        public bool requiresMeditationSpot = true;
        public string breakthroughHintKey = "";

        public string[] unlocks = new string[0];
        public float absorbEfficiency = 1f;
        public float outgoingDamageMul = 1f;

        public float ExpForStage(int stage)
        {
            int i = Mathf.Clamp(stage - 1, 0, expPerStage.Length - 1);
            return expPerStage[i];
        }
    }
}
