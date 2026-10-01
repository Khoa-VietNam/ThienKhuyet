using System;
using System.Collections.Generic;

namespace ThienKhuyet.Items
{
    [Serializable]
    public struct ItemStack
    {
        public string itemId;
        public int count;

        public ItemStack(string itemId, int count)
        {
            this.itemId = itemId;
            this.count = count;
        }

        public bool IsEmpty => string.IsNullOrEmpty(itemId) || count <= 0;
    }

    /// <summary>Slot-based inventory with stacking. Pure C#; stack limits come from a lookup delegate.</summary>
    public sealed class Inventory
    {
        readonly List<ItemStack> slots = new List<ItemStack>();
        readonly Func<string, int> stackLimit;

        public int Capacity { get; private set; }
        public event Action Changed;

        public Inventory(int capacity, Func<string, int> stackLimit)
        {
            Capacity = capacity;
            this.stackLimit = stackLimit ?? (_ => 99);
            for (int i = 0; i < capacity; i++) slots.Add(default);
        }

        public int SlotCount => slots.Count;

        public ItemStack GetSlot(int index)
        {
            return slots[index];
        }

        public int Count(string itemId)
        {
            int n = 0;
            for (int i = 0; i < slots.Count; i++)
                if (slots[i].itemId == itemId) n += slots[i].count;
            return n;
        }

        public bool Has(string itemId, int count = 1)
        {
            return Count(itemId) >= count;
        }

        public int UsedSlots
        {
            get
            {
                int n = 0;
                for (int i = 0; i < slots.Count; i++) if (!slots[i].IsEmpty) n++;
                return n;
            }
        }

        public bool CanAdd(string itemId, int count)
        {
            int limit = Math.Max(1, stackLimit(itemId));
            int room = 0;
            for (int i = 0; i < slots.Count && room < count; i++)
            {
                if (slots[i].IsEmpty) room += limit;
                else if (slots[i].itemId == itemId) room += limit - slots[i].count;
            }
            return room >= count;
        }

        /// <summary>Adds items; returns how many could NOT be added (inventory full).</summary>
        public int Add(string itemId, int count)
        {
            if (string.IsNullOrEmpty(itemId) || count <= 0) return 0;
            int limit = Math.Max(1, stackLimit(itemId));
            int remaining = count;
            for (int i = 0; i < slots.Count && remaining > 0; i++)
            {
                if (slots[i].itemId != itemId || slots[i].count >= limit) continue;
                int add = Math.Min(limit - slots[i].count, remaining);
                slots[i] = new ItemStack(itemId, slots[i].count + add);
                remaining -= add;
            }
            for (int i = 0; i < slots.Count && remaining > 0; i++)
            {
                if (!slots[i].IsEmpty) continue;
                int add = Math.Min(limit, remaining);
                slots[i] = new ItemStack(itemId, add);
                remaining -= add;
            }
            if (remaining != count) Changed?.Invoke();
            return remaining;
        }

        public bool Remove(string itemId, int count)
        {
            if (count <= 0) return true;
            if (Count(itemId) < count) return false;
            int remaining = count;
            for (int i = slots.Count - 1; i >= 0 && remaining > 0; i--)
            {
                if (slots[i].itemId != itemId) continue;
                int take = Math.Min(slots[i].count, remaining);
                int left = slots[i].count - take;
                slots[i] = left <= 0 ? default : new ItemStack(itemId, left);
                remaining -= take;
            }
            Changed?.Invoke();
            return true;
        }

        public bool RemoveAt(int index, int count)
        {
            ItemStack s = slots[index];
            if (s.IsEmpty || s.count < count) return false;
            int left = s.count - count;
            slots[index] = left <= 0 ? default : new ItemStack(s.itemId, left);
            Changed?.Invoke();
            return true;
        }

        public void Swap(int a, int b)
        {
            ItemStack t = slots[a];
            slots[a] = slots[b];
            slots[b] = t;
            Changed?.Invoke();
        }

        /// <summary>Merges stacks and moves everything to the front, ordered by item id.</summary>
        public void Sort(Func<string, string> sortKey)
        {
            var all = new List<ItemStack>();
            var totals = new Dictionary<string, int>();
            for (int i = 0; i < slots.Count; i++)
            {
                if (slots[i].IsEmpty) continue;
                totals.TryGetValue(slots[i].itemId, out int c);
                totals[slots[i].itemId] = c + slots[i].count;
            }
            var ids = new List<string>(totals.Keys);
            ids.Sort((x, y) => string.CompareOrdinal(sortKey != null ? sortKey(x) : x, sortKey != null ? sortKey(y) : y));
            for (int i = 0; i < slots.Count; i++) slots[i] = default;
            for (int i = 0; i < ids.Count; i++) Add(ids[i], totals[ids[i]]);
            Changed?.Invoke();
        }

        public void Clear()
        {
            for (int i = 0; i < slots.Count; i++) slots[i] = default;
            Changed?.Invoke();
        }

        public ItemStack[] ToArray()
        {
            var list = new List<ItemStack>();
            for (int i = 0; i < slots.Count; i++) if (!slots[i].IsEmpty) list.Add(slots[i]);
            return list.ToArray();
        }

        public void FromArray(ItemStack[] stacks)
        {
            for (int i = 0; i < slots.Count; i++) slots[i] = default;
            if (stacks != null)
                for (int i = 0; i < stacks.Length; i++) Add(stacks[i].itemId, stacks[i].count);
            Changed?.Invoke();
        }
    }
}
