using System.Collections.Generic;
using State;

namespace Systems
{
    /// <summary>
    /// Hideout stash operations. Stash is a flat <see cref="List{ItemState}"/>
    /// on Player, not slot-addressable like backpack. Items are appended to
    /// the end on deposit; withdraws place into a specific player slot, with
    /// optional swap when the target slot is occupied.
    ///
    /// Mirrors the System-layer pattern used by <see cref="InventorySystem"/>
    /// and <see cref="LootSystem"/> — pure static functions, mutate explicit
    /// state, no Unity refs, no allocations on the happy path.
    /// </summary>
    public static class StashSystem
    {
        /// <summary>
        /// Move item from <paramref name="playerInv"/> at <paramref name="sourceSlot"/>
        /// to the end of the <paramref name="stash"/> list. Source slot is cleared.
        /// Returns false if source slot is empty or args are null.
        /// </summary>
        public static bool TryDeposit(InventoryState playerInv, List<ItemState> stash,
            InventorySlotRef sourceSlot)
        {
            if (playerInv == null || stash == null) return false;
            var item = playerInv.GetSlot(sourceSlot);
            if (item == null) return false;

            playerInv.SetSlot(sourceSlot, null);
            stash.Add(item);
            return true;
        }

        /// <summary>
        /// Move stash[<paramref name="stashIndex"/>] into the player at
        /// <paramref name="targetSlot"/>. Validates the stash item's
        /// <c>AllowedSlots</c> against the target slot type.
        ///
        /// If the target slot is empty: simple withdraw (stash shrinks by 1).
        /// If the target slot is occupied: swap — stash item lands у targetSlot,
        /// the existing player item takes the stash slot at the same index
        /// (stash size unchanged). Mirrors <see cref="LootSystem.TryTransfer"/>
        /// swap semantics для UX consistency.
        ///
        /// Returns false if args invalid, definition missing, AllowedSlots
        /// mismatch, or stashIndex out of range.
        /// </summary>
        public static bool TryWithdraw(List<ItemState> stash, int stashIndex,
            InventoryState playerInv, InventorySlotRef targetSlot)
        {
            if (stash == null || playerInv == null) return false;
            if (stashIndex < 0 || stashIndex >= stash.Count) return false;

            var stashItem = stash[stashIndex];
            if (stashItem?.Definition == null) return false;

            var targetSlotType = targetSlot.ToItemSlotType();
            if ((stashItem.Definition.AllowedSlots & targetSlotType) == 0) return false;

            var existing = playerInv.GetSlot(targetSlot);
            if (existing != null)
            {
                // Swap: stash item replaces existing у target slot, existing
                // item takes stashItem's place у stash. Stash size unchanged.
                // Existing item's AllowedSlots не валідуємо для stash side —
                // stash приймає будь-який ItemState (no slot type filter).
                playerInv.SetSlot(targetSlot, stashItem);
                stash[stashIndex] = existing;
                return true;
            }

            playerInv.SetSlot(targetSlot, stashItem);
            stash.RemoveAt(stashIndex);
            return true;
        }
    }
}
