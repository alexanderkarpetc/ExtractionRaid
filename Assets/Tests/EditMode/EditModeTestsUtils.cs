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

            state.PlayerEntity.Hotbar[0] = weapon;
            state.PlayerEntity.SelectedHotbarSlot = 0;
            state.PlayerEntity.EquippedWeapon = weapon;

            return state;
        }
    }
}