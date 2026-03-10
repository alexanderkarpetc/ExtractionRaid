using State;
using UnityEngine;

namespace Tests.EditMode
{
    public static class EditModeTestsUtils
    {
        public static RaidState CreateStateWithPlayer(Vector3 startPos)
        {
            var state = RaidState.Create();
            var playerId = state.AllocateEId();
            state.PlayerEntity = PlayerEntityState.Create(playerId, startPos);

            var weaponId = state.AllocateEId();
            var weapon = WeaponEntityState.CreateDefault(weaponId);

            weapon.Phase = WeaponPhase.Ready;

            state.PlayerEntity.Hotbar[0] = weapon;
            state.PlayerEntity.SelectedHotbarSlot = 0;
            state.PlayerEntity.EquippedWeapon = weapon;
            state.PlayerEntity.PendingHotbarSlot = -1;

            var weapon2Id = state.AllocateEId();
            state.PlayerEntity.Hotbar[1] = WeaponEntityState.CreateSecondary(weapon2Id);

            // Starting reserve ammo for tests
            var rifleAmmoId = state.AllocateEId();
            state.Inventory.Backpack[0] = ItemState.Create(rifleAmmoId, "Ammo_Rifle", 60);
            var shotgunAmmoId = state.AllocateEId();
            state.Inventory.Backpack[1] = ItemState.Create(shotgunAmmoId, "Ammo_Shotgun", 15);

            return state;
        }
    }
}