using Session;
using State;

namespace Systems
{
    public static class WeaponSyncSystem
    {
        public static void Tick(RaidState state, in RaidContext context)
        {
            var player = state.PlayerEntity;
            if (player == null) return;

            var inventory = state.Inventory;
            int slotCount = PlayerEntityState.HotbarSize;

            for (int i = 0; i < slotCount; i++)
            {
                var invItem = inventory.WeaponSlots[i];
                var hotbarWeapon = player.Hotbar[i];

                if (invItem == null && hotbarWeapon != null)
                {
                    player.Hotbar[i] = null;

                    if (player.SelectedHotbarSlot == i)
                    {
                        player.SelectedHotbarSlot = -1;
                        player.EquippedWeapon = null;
                    }

                    if (player.PendingHotbarSlot == i)
                        player.PendingHotbarSlot = -1;

                    continue;
                }

                if (invItem != null && hotbarWeapon == null)
                {
                    var weapon = WeaponEntityState.CreateFromDefinitionId(
                        invItem.Id, invItem.DefinitionId);
                    if (weapon != null)
                        player.Hotbar[i] = weapon;

                    continue;
                }

                if (invItem != null && hotbarWeapon != null
                    && hotbarWeapon.Id != invItem.Id)
                {
                    var weapon = WeaponEntityState.CreateFromDefinitionId(
                        invItem.Id, invItem.DefinitionId);
                    if (weapon != null)
                        player.Hotbar[i] = weapon;

                    if (player.SelectedHotbarSlot == i)
                        player.EquippedWeapon = player.Hotbar[i];
                }
            }
        }
    }
}
