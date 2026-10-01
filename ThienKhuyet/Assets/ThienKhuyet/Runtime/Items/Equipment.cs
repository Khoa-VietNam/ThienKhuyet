using System;
using System.Collections.Generic;
using ThienKhuyet.Data;

namespace ThienKhuyet.Items
{
    /// <summary>Equipped item ids per slot. Pure C#.</summary>
    public sealed class Equipment
    {
        readonly Dictionary<EquipSlot, string> slots = new Dictionary<EquipSlot, string>();

        public static readonly EquipSlot[] AllSlots =
        {
            EquipSlot.Weapon, EquipSlot.Head, EquipSlot.Body, EquipSlot.Boots, EquipSlot.Ring, EquipSlot.Pendant
        };

        public event Action Changed;

        public string Get(EquipSlot slot)
        {
            return slots.TryGetValue(slot, out string id) ? id : null;
        }

        /// <summary>Sets the slot and returns the previously equipped item id (or null).</summary>
        public string Set(EquipSlot slot, string itemId)
        {
            string old = Get(slot);
            if (string.IsNullOrEmpty(itemId)) slots.Remove(slot);
            else slots[slot] = itemId;
            Changed?.Invoke();
            return old;
        }

        public void Clear()
        {
            slots.Clear();
            Changed?.Invoke();
        }

        public string[] ToArray()
        {
            var arr = new string[AllSlots.Length];
            for (int i = 0; i < arr.Length; i++) arr[i] = Get(AllSlots[i]) ?? string.Empty;
            return arr;
        }

        public void FromArray(string[] ids)
        {
            slots.Clear();
            if (ids != null)
                for (int i = 0; i < ids.Length && i < AllSlots.Length; i++)
                    if (!string.IsNullOrEmpty(ids[i])) slots[AllSlots[i]] = ids[i];
            Changed?.Invoke();
        }
    }
}
