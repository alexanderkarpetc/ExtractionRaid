using System;
using System.Collections.Generic;
using State;
using UnityEngine;

namespace Save
{
    [Serializable]
    public class ItemSaveData
    {
        public int SlotIndex;
        public string DefinitionId;
        public int StackCount;

        public static ItemSaveData FromSlot(ItemState item, int slotIndex)
        {
            if (item == null) return null;
            return new ItemSaveData
            {
                SlotIndex = slotIndex,
                DefinitionId = item.DefinitionId,
                StackCount = item.StackCount
            };
        }

        public static ItemSaveData FromState(ItemState item)
        {
            return FromSlot(item, 0);
        }

        public ItemState ToState()
        {
            if (string.IsNullOrEmpty(DefinitionId)) return null;
            return new ItemState { Id = EId.None, DefinitionId = DefinitionId, StackCount = StackCount };
        }
    }

    [Serializable]
    public class InventorySaveData
    {
        public List<ItemSaveData> WeaponSlots;
        public ItemSaveData Helmet;
        public ItemSaveData BodyArmor;
        public List<ItemSaveData> Backpack;
        public int[] QuickSlotBindings;

        public static InventorySaveData FromState(InventoryState inv)
        {
            var data = new InventorySaveData
            {
                WeaponSlots = new List<ItemSaveData>(),
                Backpack = new List<ItemSaveData>(),
                QuickSlotBindings = new int[InventoryState.QuickSlotCount],
                Helmet = ItemSaveData.FromState(inv.HelmetSlot),
                BodyArmor = ItemSaveData.FromState(inv.BodyArmorSlot),
            };

            for (int i = 0; i < InventoryState.WeaponSlotCount; i++)
            {
                var saved = ItemSaveData.FromSlot(inv.WeaponSlots[i], i);
                if (saved != null) data.WeaponSlots.Add(saved);
            }
            for (int i = 0; i < InventoryState.BackpackSize; i++)
            {
                var saved = ItemSaveData.FromSlot(inv.Backpack[i], i);
                if (saved != null) data.Backpack.Add(saved);
            }
            Array.Copy(inv.QuickSlotBindings, data.QuickSlotBindings, InventoryState.QuickSlotCount);

            return data;
        }

        public void ApplyTo(InventoryState inv)
        {
            inv.HelmetSlot = Helmet?.ToState();
            inv.BodyArmorSlot = BodyArmor?.ToState();

            for (int i = 0; i < InventoryState.WeaponSlotCount; i++)
                inv.WeaponSlots[i] = null;
            if (WeaponSlots != null)
                foreach (var ws in WeaponSlots)
                    if (ws.SlotIndex >= 0 && ws.SlotIndex < InventoryState.WeaponSlotCount)
                        inv.WeaponSlots[ws.SlotIndex] = ws.ToState();

            for (int i = 0; i < InventoryState.BackpackSize; i++)
                inv.Backpack[i] = null;
            if (Backpack != null)
                foreach (var bs in Backpack)
                    if (bs.SlotIndex >= 0 && bs.SlotIndex < InventoryState.BackpackSize)
                        inv.Backpack[bs.SlotIndex] = bs.ToState();

            if (QuickSlotBindings != null)
                Array.Copy(QuickSlotBindings, inv.QuickSlotBindings,
                    Mathf.Min(QuickSlotBindings.Length, InventoryState.QuickSlotCount));
        }
    }

    [Serializable]
    public class TaskProgressSaveData
    {
        public int TaskIndex;
        public int CurrentCount;
    }

    [Serializable]
    public class QuestProgressSaveData
    {
        public string QuestId;
        public int Status;
        public List<TaskProgressSaveData> Tasks;

        public static QuestProgressSaveData FromState(QuestProgress p)
        {
            var data = new QuestProgressSaveData
            {
                QuestId = p.QuestId,
                Status = (int)p.Status,
                Tasks = new List<TaskProgressSaveData>(p.Tasks.Count)
            };
            foreach (var t in p.Tasks)
                data.Tasks.Add(new TaskProgressSaveData { TaskIndex = t.TaskIndex, CurrentCount = t.CurrentCount });
            return data;
        }

        public QuestProgress ToState()
        {
            var p = new QuestProgress
            {
                QuestId = QuestId,
                Status = (QuestStatus)Status,
                Tasks = new List<TaskProgress>(Tasks?.Count ?? 0)
            };
            if (Tasks != null)
                foreach (var t in Tasks)
                    p.Tasks.Add(new TaskProgress { TaskIndex = t.TaskIndex, CurrentCount = t.CurrentCount });
            return p;
        }
    }

    [Serializable]
    public class SaveData
    {
        public string PlayerName;
        public InventorySaveData Inventory;
        public List<ItemSaveData> Stash;
        public List<QuestProgressSaveData> Quests;
    }
}
