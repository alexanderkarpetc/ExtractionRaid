using System;
using System.Collections.Generic;
using ApplicationCore;
using State;
using UnityEngine;

namespace Save
{
    /// <summary>
    /// Flat, JsonUtility-friendly snapshot of a weapon's <see cref="WeaponConfiguration"/>.
    /// The live config nests <c>readonly struct</c> cores (PayloadCoreInstance etc.) whose
    /// readonly fields Unity serialization can't round-trip, so we persist the identity
    /// primitives here and rebuild the config on load via <see cref="ToConfig"/>.
    /// </summary>
    [Serializable]
    public class WeaponConfigSaveData
    {
        public string PayloadId;
        public int PayloadRarity;
        public string DeliveryId;
        public int DeliveryRarity;
        public bool HasExotic;
        public string ExoticId;
        public int AmmoInMagazine;

        public static WeaponConfigSaveData FromConfig(WeaponConfiguration c)
        {
            var data = new WeaponConfigSaveData
            {
                PayloadId = c.Payload.DefinitionId,
                PayloadRarity = (int)c.Payload.Rarity,
                DeliveryId = c.Delivery.DefinitionId,
                DeliveryRarity = (int)c.Delivery.Rarity,
                AmmoInMagazine = c.AmmoInMagazine,
            };
            var exotic = c.Exotic;
            data.HasExotic = exotic.HasValue;
            data.ExoticId = exotic.HasValue ? exotic.Value.DefinitionId : null;
            return data;
        }

        public WeaponConfiguration ToConfig()
        {
            ExoticModInstance? exotic = HasExotic && !string.IsNullOrEmpty(ExoticId)
                ? new ExoticModInstance(ExoticId)
                : (ExoticModInstance?)null;

            return new WeaponConfiguration(
                new PayloadCoreInstance(PayloadId, (RarityTier)PayloadRarity),
                new DeliveryCoreInstance(DeliveryId, (RarityTier)DeliveryRarity),
                exotic,
                AmmoInMagazine);
        }
    }

    [Serializable]
    public class ItemSaveData
    {
        public int SlotIndex;
        public string DefinitionId;
        public int StackCount;

        // Consumable resource pool (e.g. medkit charge). -1 = full/uninitialized;
        // default keeps legacy saves (missing field) at "full" rather than empty.
        public int Resource = -1;

        // Weapon-builder composition — only set for weapon items. Without this the
        // weapon loads back as a plain ItemState (HasWeaponConfiguration = false),
        // which the equip path treats as a broken/unconfigured weapon.
        public bool HasWeaponConfiguration;
        public WeaponConfigSaveData Weapon;

        public static ItemSaveData FromSlot(ItemState item, int slotIndex)
        {
            if (item == null) return null;
            var data = new ItemSaveData
            {
                SlotIndex = slotIndex,
                DefinitionId = item.DefinitionId,
                StackCount = item.StackCount,
                Resource = item.Resource
            };
            if (item.HasWeaponConfiguration)
            {
                data.HasWeaponConfiguration = true;
                data.Weapon = WeaponConfigSaveData.FromConfig(item.WeaponConfiguration);
            }
            return data;
        }

        public static ItemSaveData FromState(ItemState item)
        {
            return FromSlot(item, 0);
        }

        public ItemState ToState()
        {
            if (string.IsNullOrEmpty(DefinitionId)) return null;

            if (HasWeaponConfiguration && Weapon != null)
                return ItemState.CreateWeapon(App.Instance.AllocateEId(), DefinitionId, Weapon.ToConfig());

            return new ItemState { Id = App.Instance.AllocateEId(), DefinitionId = DefinitionId, StackCount = StackCount, Resource = Resource };
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
    public class BuildingLevelSaveData
    {
        // Kind is persisted as a string (enum name) instead of int so reordering or
        // inserting BuildingKind values doesn't corrupt existing saves.
        public string Kind;
        public int Level;
    }

    [Serializable]
    public class SaveData
    {
        public string PlayerName;
        public int Credits;
        public InventorySaveData Inventory;
        public List<ItemSaveData> Stash;
        public List<QuestProgressSaveData> Quests;
        public List<BuildingLevelSaveData> BuildingLevels;

        // Progression tree — allocated node ids (permanent, no refund) + unspent points.
        // Missing on legacy saves → null/0, which loads as an empty tree.
        public List<string> AllocatedNodes;
        public int ProgressionPoints;
    }
}
