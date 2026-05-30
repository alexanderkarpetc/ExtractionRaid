using State;

namespace Systems
{
    /// <summary>
    /// Hotbar weapon-slot operations driven by the HUD (Battle HUD Stage 6).
    ///
    /// Invoked as a command from <c>HotbarOverlay</c> (View) — same pattern as the overlay
    /// already calling <c>QuickSlotRules.IsAssignable</c>: a stateless system sub-function,
    /// not a per-tick system. Keeps the swap rules (ammo preservation, selection remap) in the
    /// system layer per CLAUDE.md rule 11, out of the View.
    /// </summary>
    public static class HotbarWeaponSystem
    {
        /// <summary>
        /// Swaps two weapon hotbar slots. Exchanges BOTH the inventory item references and the
        /// live runtime <see cref="WeaponEntityState"/> entities so magazines / heat / phase are
        /// preserved — if only the inventory refs were swapped, <see cref="WeaponSyncSystem"/>
        /// would see mismatched Ids next tick and rebuild both weapons, resetting AmmoInMagazine
        /// to the config's spawn value.
        ///
        /// Selection follows the equipped weapon to its new slot (so the weapon stays in hand,
        /// just changes position). A holstered selection (-1) is left untouched.
        /// </summary>
        public static void SwapWeaponSlots(RaidState state, InventoryState inventory, int a, int b)
        {
            if (state == null || inventory == null) return;
            var player = state.PlayerEntity;
            if (player == null) return;
            if (a == b) return;
            if (a < 0 || b < 0) return;
            if (a >= InventoryState.WeaponSlotCount || b >= InventoryState.WeaponSlotCount) return;
            if (a >= PlayerEntityState.HotbarSize || b >= PlayerEntityState.HotbarSize) return;

            (inventory.WeaponSlots[a], inventory.WeaponSlots[b]) =
                (inventory.WeaponSlots[b], inventory.WeaponSlots[a]);

            (player.Hotbar[a], player.Hotbar[b]) =
                (player.Hotbar[b], player.Hotbar[a]);

            player.SelectedHotbarSlot = Remap(player.SelectedHotbarSlot, a, b);
            player.PendingHotbarSlot  = Remap(player.PendingHotbarSlot, a, b);
            // EquippedWeapon references the entity object, which moved with the swap — the
            // reference stays valid and still equals Hotbar[SelectedHotbarSlot].

            // Bump so the inventory window re-renders the swapped weapon slots (RefreshAll
            // early-outs on unchanged Version). Both call sites — hotbar overlay drag AND
            // inventory window weapon↔weapon drop — get correct refresh without remembering to bump.
            inventory.Version++;
        }

        static int Remap(int slot, int a, int b)
        {
            if (slot == a) return b;
            if (slot == b) return a;
            return slot;
        }
    }
}
