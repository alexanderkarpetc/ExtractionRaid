using Adapters;
using Constants;
using State;
using UnityEngine;

namespace Systems
{
    public static class PlayerSpawnSystem
    {
        public static void SpawnPlayer(RaidState state, Vector3 spawnPosition, IRaidEvents events, string levelId = null)
        {
            if (state.PlayerEntity != null) return;

            var playerId = state.AllocateEId();
            state.PlayerEntity = PlayerEntityState.Create(playerId, spawnPosition);
            state.HealthMap[playerId] = HealthState.Create(BotConstants.PlayerMaxHp);

            var inventory = App.App.Instance.Player.Inventory;
            if (levelId == "shooting_range" || IsInventoryEmpty(App.App.Instance.Player.Inventory))
            {
                ClearInventory(inventory);
                GiveStartingLoadout(state, inventory);
            }

            for (int i = 0; i < PlayerEntityState.HotbarSize; i++)
            {
                var invItem = inventory.WeaponSlots[i];
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

            EquipmentSystem.SyncArmorFromInventory(state, playerId, inventory);

            events.PlayerSpawned(playerId);
        }

        static void ClearInventory(InventoryState inv)
        {
            for (int i = 0; i < InventoryState.WeaponSlotCount; i++)
                inv.WeaponSlots[i] = null;
            inv.HelmetSlot = null;
            inv.BodyArmorSlot = null;
            for (int i = 0; i < InventoryState.BackpackSize; i++)
                inv.Backpack[i] = null;
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

        static void GiveStartingLoadout(RaidState state, InventoryState inventory)
        {
            var weaponId = state.AllocateEId();
            inventory.WeaponSlots[0] = ItemState.Create(weaponId, "Rifle");

            var weapon2Id = state.AllocateEId();
            inventory.WeaponSlots[1] = ItemState.Create(weapon2Id, "Shotgun");

            inventory.Backpack[0] = ItemState.Create(state.AllocateEId(), "Ammo_Rifle", 60);
            inventory.Backpack[1] = ItemState.Create(state.AllocateEId(), "Ammo_Shotgun", 15);
            inventory.Backpack[2] = ItemState.Create(state.AllocateEId(), "Ammo_Pistol", 36);

            for (int i = 0; i < GrenadeConstants.StartingCount; i++)
                inventory.Backpack[3 + i] = ItemState.Create(state.AllocateEId(), "Grenade");

            inventory.Backpack[6] = ItemState.Create(state.AllocateEId(), "Medkit",
                (int)MedConstants.TotalHealAmount);
            inventory.Backpack[7] = ItemState.Create(state.AllocateEId(), "Bandage");
            inventory.Backpack[8] = ItemState.Create(state.AllocateEId(), "Bandage");
            inventory.Backpack[9] = ItemState.Create(state.AllocateEId(), "Pistol");

            inventory.Backpack[10] = ItemState.Create(state.AllocateEId(), "Helmet_Basic");
            inventory.Backpack[11] = ItemState.Create(state.AllocateEId(), "Armor_Basic");
        }
    }
}
