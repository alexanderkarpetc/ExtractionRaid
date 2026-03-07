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
            int freeSlot = inventory.FindFreeBackpackSlot();
            if (freeSlot < 0) return false;

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

            var item = ItemState.Create(groundItem.Id, groundItem.DefinitionId);
            inventory.Backpack[freeSlot] = item;

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

            var groundItem = GroundItemState.Create(item.Id, item.DefinitionId, dropPosition);
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
