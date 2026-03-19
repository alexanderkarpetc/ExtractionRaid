using Session;
using State;

namespace Systems
{
    public static class QuickSlotSystem
    {
        public static void Tick(RaidState state, in RaidContext context)
        {
            var player = state.PlayerEntity;
            if (player == null) return;

            var input = context.Input;
            if (input == null) return;

            var inventory = state.Inventory;

            ClearStaleBindings(inventory);

            int held = input.QuickSlotHeld;
            if (player.ActiveQuickSlot >= 0 && held != player.ActiveQuickSlot)
            {
                player.QuickSlotHeld = false;
                player.ActiveQuickSlot = -1;
            }
            else if (player.ActiveQuickSlot >= 0)
            {
                player.QuickSlotHeld = true;
            }

            int pressed = input.QuickSlotPressed;
            if (pressed < 0) return;
            if (player.IsRolling || player.AreHandsBusy) return;

            int boundSlot = inventory.QuickSlotBindings[pressed];
            if (boundSlot < 0) return;

            if (inventory.Backpack[boundSlot] == null) return;

            player.ActiveQuickSlot = pressed;
            player.QuickSlotHeld = true;
        }

        static void ClearStaleBindings(InventoryState inventory)
        {
            for (int qi = 0; qi < InventoryState.QuickSlotCount; qi++)
            {
                int slot = inventory.QuickSlotBindings[qi];
                if (slot < 0) continue;
                if (inventory.Backpack[slot] == null)
                    inventory.QuickSlotBindings[qi] = -1;
            }
        }

        public static string GetActiveDefinitionId(PlayerEntityState player, InventoryState inventory)
        {
            if (player.ActiveQuickSlot < 0) return null;
            int slot = inventory.QuickSlotBindings[player.ActiveQuickSlot];
            if (slot < 0) return null;
            return inventory.Backpack[slot]?.DefinitionId;
        }

        public static int GetActiveBoundSlot(PlayerEntityState player, InventoryState inventory)
        {
            if (player.ActiveQuickSlot < 0) return -1;
            return inventory.QuickSlotBindings[player.ActiveQuickSlot];
        }
    }
}
