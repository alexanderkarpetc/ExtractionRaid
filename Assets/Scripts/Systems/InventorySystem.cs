using Adapters;
using State;
using UnityEngine;

namespace Systems
{
    public static class InventorySystem
    {
        public const float PickUpRange = 3f;

        public static bool TryPickUp(RaidState state, EId groundItemId, IRaidEvents events)
        {
            var inventory = state.Inventory;

            GroundItemState groundItem = null;
            int groundIndex = -1;
            for (int i = 0; i < state.GroundItems.Count; i++)
            {
                if (state.GroundItems[i].Id == groundItemId)
                {
                    groundItem = state.GroundItems[i];
                    groundIndex = i;
                    break;
                }
            }
            if (groundItem == null) return false;

            var def = ItemDefinition.Get(groundItem.DefinitionId);
            int pickupCount = groundItem.StackCount;

            // Stackable item: merge into existing stacks, then overflow to free slots
            if (def != null && def.IsStackable && pickupCount > 0)
            {
                int originalCount = pickupCount;

                // Phase 1: fill existing partial stacks
                for (int i = 0; i < InventoryState.BackpackSize && pickupCount > 0; i++)
                {
                    var slot = inventory.Backpack[i];
                    if (slot == null || slot.DefinitionId != groundItem.DefinitionId) continue;
                    int space = def.MaxStackSize - slot.StackCount;
                    if (space <= 0) continue;
                    int add = pickupCount < space ? pickupCount : space;
                    slot.StackCount += add;
                    pickupCount -= add;
                }

                // Phase 2: overflow into free slots
                while (pickupCount > 0)
                {
                    int freeSlot = inventory.FindFreeBackpackSlot();
                    if (freeSlot < 0) break;
                    int add = pickupCount < def.MaxStackSize ? pickupCount : def.MaxStackSize;
                    inventory.Backpack[freeSlot] = ItemState.Create(state.AllocateEId(), groundItem.DefinitionId, add);
                    pickupCount -= add;
                }

                if (pickupCount == originalCount) return false; // nothing picked up

                state.GroundItems.RemoveAt(groundIndex);
                events.GroundItemDespawned(groundItemId);
                return true;
            }

            // Non-stackable: original behavior
            int free = inventory.FindFreeBackpackSlot();
            if (free < 0) return false;

            var item = ItemState.Create(groundItem.Id, groundItem.DefinitionId);
            inventory.Backpack[free] = item;
            state.GroundItems.RemoveAt(groundIndex);
            events.GroundItemDespawned(groundItemId);
            return true;
        }

        public static bool TryDrop(RaidState state, InventorySlotRef slot, Vector3 dropPosition, IRaidEvents events)
        {
            var inventory = state.Inventory;
            var item = inventory.GetSlot(slot);
            if (item == null) return false;

            inventory.SetSlot(slot, null);

            var groundItem = GroundItemState.Create(item.Id, item.DefinitionId, dropPosition, item.StackCount);
            state.GroundItems.Add(groundItem);
            events.GroundItemSpawned(groundItem.Id, groundItem.Position, groundItem.DefinitionId);
            return true;
        }

        public static bool TryMove(InventoryState inventory, InventorySlotRef from, InventorySlotRef to)
        {
            if (from.Equals(to)) return false;

            var sourceItem = inventory.GetSlot(from);
            if (sourceItem == null) return false;

            var def = sourceItem.Definition;
            if (def == null) return false;

            var targetSlotType = to.ToItemSlotType();
            if ((def.AllowedSlots & targetSlotType) == 0) return false;

            var targetItem = inventory.GetSlot(to);

            if (targetItem != null)
            {
                var targetDef = targetItem.Definition;
                var sourceSlotType = from.ToItemSlotType();
                if (targetDef == null || (targetDef.AllowedSlots & sourceSlotType) == 0)
                    return false;
            }

            inventory.SetSlot(from, targetItem);
            inventory.SetSlot(to, sourceItem);
            return true;
        }

        public static int FindFirstMedkitSlot(InventoryState inventory)
        {
            for (int i = 0; i < InventoryState.BackpackSize; i++)
                if (inventory.Backpack[i]?.DefinitionId == "Medkit" && inventory.Backpack[i].StackCount > 0)
                    return i;
            return -1;
        }

        public static int CountGrenades(InventoryState inventory)
        {
            int count = 0;
            for (int i = 0; i < InventoryState.BackpackSize; i++)
                if (inventory.Backpack[i]?.DefinitionId == "Grenade") count++;
            return count;
        }

        public static bool ConsumeOneGrenade(InventoryState inventory)
        {
            for (int i = 0; i < InventoryState.BackpackSize; i++)
            {
                if (inventory.Backpack[i]?.DefinitionId == "Grenade")
                {
                    inventory.Backpack[i] = null;
                    return true;
                }
            }
            return false;
        }

        public static EId FindNearestGroundItem(RaidState state, Vector3 playerPosition)
        {
            float bestDist = float.MaxValue;
            EId bestId = EId.None;

            for (int i = 0; i < state.GroundItems.Count; i++)
            {
                float dist = Vector3.Distance(playerPosition, state.GroundItems[i].Position);
                if (dist <= PickUpRange && dist < bestDist)
                {
                    bestDist = dist;
                    bestId = state.GroundItems[i].Id;
                }
            }

            return bestId;
        }
    }
}
