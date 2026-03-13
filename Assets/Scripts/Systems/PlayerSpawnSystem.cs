using Adapters;
using Constants;
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
            var weapon = WeaponEntityState.CreateRifle(weaponId);

            weapon.Phase = WeaponPhase.Ready;
            weapon.PhaseStartTime = 0f;

            state.PlayerEntity.Hotbar[0] = weapon;
            state.PlayerEntity.SelectedHotbarSlot = 0;
            state.PlayerEntity.EquippedWeapon = weapon;
            state.PlayerEntity.PendingHotbarSlot = -1;

            var weapon2Id = state.AllocateEId();
            state.PlayerEntity.Hotbar[1] = WeaponEntityState.CreateShotgun(weapon2Id);

            var pistolId = state.AllocateEId();
            state.PlayerEntity.Hotbar[2] = WeaponEntityState.CreatePistol(pistolId);

            // Starting reserve ammo
            var rifleAmmoId = state.AllocateEId();
            state.Inventory.Backpack[0] = ItemState.Create(rifleAmmoId, "Ammo_Rifle", 60);
            var shotgunAmmoId = state.AllocateEId();
            state.Inventory.Backpack[1] = ItemState.Create(shotgunAmmoId, "Ammo_Shotgun", 15);
            var pistolAmmoId = state.AllocateEId();
            state.Inventory.Backpack[2] = ItemState.Create(pistolAmmoId, "Ammo_Pistol", 36);

            // Starting grenades — 1 per backpack slot
            for (int i = 0; i < GrenadeConstants.StartingCount; i++)
                state.Inventory.Backpack[3 + i] = ItemState.Create(state.AllocateEId(), "Grenade");

            state.HealthMap[playerId] = HealthState.Create(BotConstants.PlayerMaxHp);

            events.PlayerSpawned(playerId);
        }
    }
}
