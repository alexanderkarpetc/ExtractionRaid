using Adapters;
using Constants;
using State;
using UnityEngine;

namespace Systems
{
    public static class PlayerSpawnSystem
    {
        public static void SpawnPlayer(RaidState state, Vector3 spawnPosition, IRaidEvents events)
        {
            if (state.PlayerEntity != null) return;

            var playerId = state.AllocateEId();
            state.PlayerEntity = PlayerEntityState.Create(playerId, spawnPosition);
            state.HealthMap[playerId] = HealthState.Create(BotConstants.PlayerMaxHp);

            if (IsInventoryEmpty(state.Inventory))
                GiveStartingLoadout(state);

            for (int i = 0; i < PlayerEntityState.HotbarSize; i++)
            {
                var invItem = state.Inventory.WeaponSlots[i];
                if (invItem == null) continue;
                var weapon = WeaponEntityState.CreateFromDefinitionId(invItem.Id, invItem.DefinitionId);
                if (weapon == null) continue;
                weapon.Phase = WeaponPhase.Ready;
                weapon.PhaseStartTime = 0f;
                state.PlayerEntity.Hotbar[i] = weapon;
            }

            for (int i = 0; i < PlayerEntityState.HotbarSize; i++)
            {
                if (state.PlayerEntity.Hotbar[i] != null)
                {
                    state.PlayerEntity.SelectedHotbarSlot = i;
                    state.PlayerEntity.EquippedWeapon = state.PlayerEntity.Hotbar[i];
                    break;
                }
            }

            events.PlayerSpawned(playerId);
        }

        static bool IsInventoryEmpty(InventoryState inv)
        {
            for (int i = 0; i < InventoryState.WeaponSlotCount; i++)
                if (inv.WeaponSlots[i] != null) return false;
            if (inv.HelmetSlot != null || inv.BodyArmorSlot != null) return false;
            for (int i = 0; i < InventoryState.BackpackSize; i++)
                if (inv.Backpack[i] != null) return false;
            return true;
        }

        static void GiveStartingLoadout(RaidState state)
        {
            var weaponId = state.AllocateEId();
            state.Inventory.WeaponSlots[0] = ItemState.Create(weaponId, "Rifle");

            var weapon2Id = state.AllocateEId();
            state.Inventory.WeaponSlots[1] = ItemState.Create(weapon2Id, "Shotgun");

            state.Inventory.Backpack[0] = ItemState.Create(state.AllocateEId(), "Ammo_Rifle", 60);
            state.Inventory.Backpack[1] = ItemState.Create(state.AllocateEId(), "Ammo_Shotgun", 15);
            state.Inventory.Backpack[2] = ItemState.Create(state.AllocateEId(), "Ammo_Pistol", 36);

            for (int i = 0; i < GrenadeConstants.StartingCount; i++)
                state.Inventory.Backpack[3 + i] = ItemState.Create(state.AllocateEId(), "Grenade");

            state.Inventory.Backpack[6] = ItemState.Create(state.AllocateEId(), "Medkit",
                (int)MedConstants.TotalHealAmount);
            state.Inventory.Backpack[7] = ItemState.Create(state.AllocateEId(), "Bandage");
            state.Inventory.Backpack[8] = ItemState.Create(state.AllocateEId(), "Bandage");
            state.Inventory.Backpack[9] = ItemState.Create(state.AllocateEId(), "Pistol");
        }
    }
}
