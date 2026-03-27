using System;
using System.Collections.Generic;
using State;
using UnityEngine;

namespace Save
{
    [Serializable]
    public class ItemSaveData
    {
        public string DefinitionId;
        public int StackCount;

        public static ItemSaveData FromState(ItemState item)
        {
            if (item == null) return null;
            return new ItemSaveData { DefinitionId = item.DefinitionId, StackCount = item.StackCount };
        }

        public ItemState ToState()
        {
            return new ItemState { Id = EId.None, DefinitionId = DefinitionId, StackCount = StackCount };
        }
    }

    [Serializable]
    public class InventorySaveData
    {
        public ItemSaveData[] WeaponSlots;
        public ItemSaveData Helmet;
        public ItemSaveData BodyArmor;
        public ItemSaveData[] Backpack;
        public int[] QuickSlotBindings;

        public static InventorySaveData FromState(InventoryState inv)
        {
            var data = new InventorySaveData
            {
                WeaponSlots = new ItemSaveData[InventoryState.WeaponSlotCount],
                Backpack = new ItemSaveData[InventoryState.BackpackSize],
                QuickSlotBindings = new int[InventoryState.QuickSlotCount],
                Helmet = ItemSaveData.FromState(inv.HelmetSlot),
                BodyArmor = ItemSaveData.FromState(inv.BodyArmorSlot),
            };

            for (int i = 0; i < InventoryState.WeaponSlotCount; i++)
                data.WeaponSlots[i] = ItemSaveData.FromState(inv.WeaponSlots[i]);
            for (int i = 0; i < InventoryState.BackpackSize; i++)
                data.Backpack[i] = ItemSaveData.FromState(inv.Backpack[i]);
            Array.Copy(inv.QuickSlotBindings, data.QuickSlotBindings, InventoryState.QuickSlotCount);

            return data;
        }

        public void ApplyTo(InventoryState inv)
        {
            inv.HelmetSlot = Helmet?.ToState();
            inv.BodyArmorSlot = BodyArmor?.ToState();

            for (int i = 0; i < InventoryState.WeaponSlotCount; i++)
                inv.WeaponSlots[i] = WeaponSlots != null && i < WeaponSlots.Length
                    ? WeaponSlots[i]?.ToState() : null;

            for (int i = 0; i < InventoryState.BackpackSize; i++)
                inv.Backpack[i] = Backpack != null && i < Backpack.Length
                    ? Backpack[i]?.ToState() : null;

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
        public InventorySaveData Stash;
        public List<QuestProgressSaveData> Quests;
    }
}
