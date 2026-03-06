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
            state.PlayerEntity.EquippedWeapon = WeaponEntityState.CreateDefault(weaponId);

            return state;
        }
    }
}