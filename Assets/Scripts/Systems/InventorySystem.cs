using Adapters;
using ApplicationCore;
using State;
using UnityEngine;

namespace Systems
{
    public static class InventorySystem
    {
        public const float PickUpRange = 3f;

        public static bool TryPickUp(RaidState state, EId groundItemId, IRaidEvents events)
        {

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
            var inventory = App.Instance.Player.Inventory;

            // Stackable item: merge into existing stacks, then overflow to free slots
            if (def != null && def.IsStackable && pickupCount > 0)
            {
                int added = AddToBackpack(inventory, groundItem.DefinitionId, pickupCount, state.AllocateEId);
                if (added <= 0) return false; // nothing picked up

                state.GroundItems.RemoveAt(groundIndex);
                events.GroundItemDespawned(groundItemId);
                return true;
            }

            // Non-stackable: original behavior
            int free = inventory.FindFreeBackpackSlot();
            if (free < 0) return false;

            // Preserve WeaponConfiguration when picking up a weapon from ground.
            var item = groundItem.HasWeaponConfiguration
                ? ItemState.CreateWeapon(groundItem.Id, groundItem.DefinitionId, groundItem.WeaponConfiguration)
                : ItemState.Create(groundItem.Id, groundItem.DefinitionId);
            if (!groundItem.HasWeaponConfiguration) item.Resource = groundItem.Resource;
            inventory.Backpack[free] = item;
            state.GroundItems.RemoveAt(groundIndex);
            events.GroundItemDespawned(groundItemId);
            inventory.Version++;
            return true;
        }

        /// <summary>
        /// Adds <paramref name="count"/> of an item definition into the backpack,
        /// respecting <see cref="ItemDefinition.MaxStackSize"/>. Fills existing
        /// partial stacks of the same item first, then overflows into free slots
        /// creating as many full stacks as needed (plus a remainder stack).
        /// Returns the number actually added (may be less than requested if the
        /// backpack fills up). Bumps <see cref="InventoryState.Version"/> when
        /// anything changed.
        /// </summary>
        public static int AddToBackpack(InventoryState inventory, string defId, int count, System.Func<EId> allocateId)
        {
            if (inventory == null || count <= 0) return 0;
            var def = ItemDefinition.Get(defId);
            if (def == null) return 0;

            int maxStack = def.MaxStackSize < 1 ? 1 : def.MaxStackSize;
            int remaining = count;

            // Phase 1: top up existing partial stacks (stackable items only).
            if (def.IsStackable)
            {
                for (int i = 0; i < InventoryState.BackpackSize && remaining > 0; i++)
                {
                    var slot = inventory.Backpack[i];
                    if (slot == null || slot.DefinitionId != defId) continue;
                    int space = maxStack - slot.StackCount;
                    if (space <= 0) continue;
                    int add = remaining < space ? remaining : space;
                    slot.StackCount += add;
                    remaining -= add;
                }
            }

            // Phase 2: overflow into free slots, one full stack at a time.
            while (remaining > 0)
            {
                int freeSlot = inventory.FindFreeBackpackSlot();
                if (freeSlot < 0) break;
                int add = remaining < maxStack ? remaining : maxStack;
                inventory.Backpack[freeSlot] = ItemState.Create(allocateId(), defId, add);
                remaining -= add;
            }

            int added = count - remaining;
            if (added > 0) inventory.Version++;
            return added;
        }

        /// <summary>
        /// Pick up a ground item directly into a specific player slot
        /// (vs <see cref="TryPickUp"/> which auto-picks first free backpack slot
        /// + stacks). Used by UI drag-drop of floor items onto a chosen slot.
        ///
        /// Validates the ground item's definition AllowedSlots against the
        /// target slot type. Requires the target slot to be empty (no swap).
        /// On success: removes the ground item, creates the ItemState у target
        /// slot, fires <c>GroundItemDespawned</c>.
        /// </summary>
        public static bool TryPickUpToSlot(RaidState state, InventoryState inventory,
            EId groundItemId, InventorySlotRef targetSlot, IRaidEvents events)
        {
            if (state == null || inventory == null) return false;

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
            if (def == null) return false;

            var slotType = targetSlot.ToItemSlotType();
            if ((def.AllowedSlots & slotType) == 0) return false;
            if (inventory.GetSlot(targetSlot) != null) return false;

            var item = groundItem.HasWeaponConfiguration
                ? ItemState.CreateWeapon(groundItem.Id, groundItem.DefinitionId, groundItem.WeaponConfiguration)
                : ItemState.Create(groundItem.Id, groundItem.DefinitionId, groundItem.StackCount);
            if (!groundItem.HasWeaponConfiguration) item.Resource = groundItem.Resource;

            inventory.SetSlot(targetSlot, item);
            state.GroundItems.RemoveAt(groundIndex);
            events.GroundItemDespawned(groundItemId);
            return true;
        }

        public static bool TryDrop(RaidState state, InventoryState inventory, InventorySlotRef slot, Vector3 dropPosition, IRaidEvents events)
        {
            var item = inventory.GetSlot(slot);
            if (item == null) return false;

            inventory.SetSlot(slot, null);

            // Preserve WeaponConfiguration when dropping a weapon to the ground.
            var groundItem = item.HasWeaponConfiguration
                ? GroundItemState.CreateWeapon(item.Id, item.DefinitionId, dropPosition, item.WeaponConfiguration)
                : GroundItemState.Create(item.Id, item.DefinitionId, dropPosition, item.StackCount, item.Resource);
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
            RemapQuickSlotBindings(inventory, from, to);
            return true;
        }

        // Removes bindings whose backpack slot is now empty (mirrors QuickSlotSystem.ClearStaleBindings
        // but callable any time, not just during a raid tick).
        public static void ClearStaleQuickSlotBindings(InventoryState inventory)
        {
            for (int qi = 0; qi < inventory.QuickSlotBindings.Length; qi++)
            {
                int slot = inventory.QuickSlotBindings[qi];
                if (slot >= 0 && inventory.Backpack[slot] == null)
                    inventory.QuickSlotBindings[qi] = -1;
            }
        }

        // Updates QuickSlotBindings after a swap so bindings follow their items.
        // Handles backpack↔backpack (swap both), backpack→non-backpack (clear), non-backpack→backpack (clear).
        public static void RemapQuickSlotBindings(InventoryState inventory, InventorySlotRef from, InventorySlotRef to)
        {
            bool fromIsBackpack = from.Type == SlotType.Backpack;
            bool toIsBackpack   = to.Type   == SlotType.Backpack;
            if (!fromIsBackpack && !toIsBackpack) return;

            for (int qi = 0; qi < inventory.QuickSlotBindings.Length; qi++)
            {
                int bound = inventory.QuickSlotBindings[qi];
                if (fromIsBackpack && bound == from.Index)
                    inventory.QuickSlotBindings[qi] = toIsBackpack ? to.Index : -1;
                else if (toIsBackpack && bound == to.Index)
                    inventory.QuickSlotBindings[qi] = fromIsBackpack ? from.Index : -1;
            }
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

        public static int FindFirstBandageSlot(InventoryState inventory)
        {
            for (int i = 0; i < InventoryState.BackpackSize; i++)
                if (inventory.Backpack[i]?.DefinitionId == "Bandage")
                    return i;
            return -1;
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
