using Session;
using State;

namespace Systems
{
    public static class WeaponEquipSystem
    {
        public static void Tick(RaidState state, in RaidContext context)
        {
            var player = state.PlayerEntity;
            if (player == null) return;

            var input = context.Input;
            if (input == null) return;

            var slotPressed = input.HotbarSlotPressed;
            if (slotPressed < 0) return;

            if (slotPressed == player.SelectedHotbarSlot)
            {
                player.SelectedHotbarSlot = -1;
            }
            else
            {
                player.SelectedHotbarSlot = slotPressed;
            }

            player.EquippedWeapon = player.SelectedHotbarSlot >= 0
                ? player.Hotbar[player.SelectedHotbarSlot]
                : null;
        }
    }
}
