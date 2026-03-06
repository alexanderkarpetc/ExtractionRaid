using Adapters;
using State;
using UnityEngine;

namespace Systems
{
    public static class PlayerSpawnSystem
    {
        static readonly Vector3 DefaultSpawnPosition = Vector3.zero;

        public static void SpawnPlayer(RaidState state, IRaidEvents events)
        {
            if (state.PlayerEntity != null) return;

            var playerId = state.AllocateEId();
            state.PlayerEntity = PlayerEntityState.Create(playerId, DefaultSpawnPosition);

            var weaponId = state.AllocateEId();
            var weapon = WeaponEntityState.CreateDefault(weaponId);

            state.PlayerEntity.Hotbar[0] = weapon;
            state.PlayerEntity.SelectedHotbarSlot = 0;
            state.PlayerEntity.EquippedWeapon = weapon;

            var weapon2Id = state.AllocateEId();
            state.PlayerEntity.Hotbar[1] = WeaponEntityState.CreateSecondary(weapon2Id);

            events.PlayerSpawned(playerId);
        }
    }
}
