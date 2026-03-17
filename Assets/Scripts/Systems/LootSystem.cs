using Adapters;
using Constants;
using State;
using UnityEngine;

namespace Systems
{
    public static class LootSystem
    {
        public const float LootRange = 3f;

        public static void CreateLootable(RaidState state, BotEntityState bot, in BotTypeConfig config,
            IRaidEvents events)
        {
            var id = state.AllocateEId();
            var inventory = new InventoryState();

            var weaponDefId = MapWeaponPrefabToDefinition(config.WeaponPrefabId);
            if (weaponDefId != null)
            {
                var weaponItemId = state.AllocateEId();
                inventory.WeaponSlots[0] = ItemState.Create(weaponItemId, weaponDefId);
            }

            int backpackSlot = 0;

            var ammoDefId = MapWeaponPrefabToAmmo(config.WeaponPrefabId);
            if (ammoDefId != null)
            {
                var ammoId = state.AllocateEId();
                var def = ItemDefinition.Get(ammoDefId);
                int ammoCount = def != null ? Mathf.Min(30, def.MaxStackSize) : 30;
                inventory.Backpack[backpackSlot++] = ItemState.Create(ammoId, ammoDefId, ammoCount);
            }

            int medkits = bot.Blackboard.MedkitsRemaining;
            for (int i = 0; i < medkits && backpackSlot < InventoryState.BackpackSize; i++)
            {
                var medId = state.AllocateEId();
                inventory.Backpack[backpackSlot++] = ItemState.Create(medId, "Medkit", 1);
            }

            int grenades = bot.Blackboard.GrenadesRemaining;
            for (int i = 0; i < grenades && backpackSlot < InventoryState.BackpackSize; i++)
            {
                var grenadeId = state.AllocateEId();
                inventory.Backpack[backpackSlot++] = ItemState.Create(grenadeId, "Grenade");
            }

            var lootable = LootableContainerState.Create(id, bot.Position, config.TypeId, inventory);
            state.Lootables.Add(lootable);
            events.LootableSpawned(id, bot.Position, config.TypeId);
        }

        public static EId FindNearestLootable(RaidState state, Vector3 playerPosition)
        {
            float bestDist = float.MaxValue;
            EId bestId = EId.None;

            for (int i = 0; i < state.Lootables.Count; i++)
            {
                float dist = Vector3.Distance(playerPosition, state.Lootables[i].Position);
                if (dist <= LootRange && dist < bestDist)
                {
                    bestDist = dist;
                    bestId = state.Lootables[i].Id;
                }
            }

            return bestId;
        }

        public static LootableContainerState GetLootable(RaidState state, EId id)
        {
            for (int i = 0; i < state.Lootables.Count; i++)
                if (state.Lootables[i].Id == id)
                    return state.Lootables[i];
            return null;
        }

        public static bool TryTransfer(InventoryState from, InventorySlotRef fromSlot,
            InventoryState to, InventorySlotRef toSlot)
        {
            if (from == to && fromSlot.Equals(toSlot)) return false;

            var sourceItem = from.GetSlot(fromSlot);
            if (sourceItem == null) return false;

            var def = sourceItem.Definition;
            if (def == null) return false;

            var targetSlotType = toSlot.ToItemSlotType();
            if ((def.AllowedSlots & targetSlotType) == 0) return false;

            var targetItem = to.GetSlot(toSlot);

            if (targetItem != null)
            {
                var targetDef = targetItem.Definition;
                var sourceSlotType = fromSlot.ToItemSlotType();
                if (targetDef == null || (targetDef.AllowedSlots & sourceSlotType) == 0)
                    return false;
            }

            from.SetSlot(fromSlot, targetItem);
            to.SetSlot(toSlot, sourceItem);
            return true;
        }

        static string MapWeaponPrefabToDefinition(string weaponPrefabId)
        {
            return weaponPrefabId switch
            {
                "Weapon_Rifle" => "Rifle",
                "Weapon_Shotgun" => "Shotgun",
                "Weapon_Pistol" => "Pistol",
                _ => null,
            };
        }

        static string MapWeaponPrefabToAmmo(string weaponPrefabId)
        {
            return weaponPrefabId switch
            {
                "Weapon_Rifle" => "Ammo_Rifle",
                "Weapon_Shotgun" => "Ammo_Shotgun",
                "Weapon_Pistol" => "Ammo_Pistol",
                _ => null,
            };
        }
    }
}
