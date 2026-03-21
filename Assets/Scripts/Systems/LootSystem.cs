using Adapters;
using Constants;
using State;
using UnityEngine;

namespace Systems
{
    public enum InteractableType : byte { None, Lootable, GroundItem, Workbench }

    public struct InteractableResult
    {
        public EId Id;
        public InteractableType Type;
        public bool IsValid => Id.IsValid;
    }

    public static class LootSystem
    {
        public const float LootRange = 3f;

        public static void CreateContainer(RaidState state, in ContainerTypeConfig config, Vector3 position,
            IRaidEvents events)
        {
            var id = state.AllocateEId();
            var inventory = new InventoryState();

            int dropCount = Random.Range(config.MinDrops, config.MaxDrops + 1);
            int slot = 0;

            for (int i = 0; i < dropCount && slot < InventoryState.BackpackSize; i++)
            {
                var drop = config.PossibleDrops[Random.Range(0, config.PossibleDrops.Length)];
                var itemId = state.AllocateEId();
                int count = Random.Range(drop.MinCount, drop.MaxCount + 1);

                var def = ItemDefinition.Get(drop.DefinitionId);
                if (def != null)
                    count = Mathf.Min(count, def.MaxStackSize);

                inventory.Backpack[slot++] = ItemState.Create(itemId, drop.DefinitionId, count);
            }

            var lootable = LootableContainerState.Create(id, position, config.TypeId, inventory, isContainer: true);
            state.Lootables.Add(lootable);
            events.LootableSpawned(id, position, config.TypeId);
        }

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

        public static InteractableResult FindNearestInteractable(RaidState state, Vector3 playerPosition,
            Vector3 facingDirection)
        {
            float bestScore = float.MaxValue;
            var result = new InteractableResult();

            for (int i = 0; i < state.Lootables.Count; i++)
            {
                float dist = Vector3.Distance(playerPosition, state.Lootables[i].Position);
                if (dist > LootRange) continue;
                float score = ScoreInteractable(playerPosition, facingDirection, state.Lootables[i].Position, dist);
                if (score < bestScore)
                {
                    bestScore = score;
                    result.Id = state.Lootables[i].Id;
                    result.Type = InteractableType.Lootable;
                }
            }

            for (int i = 0; i < state.GroundItems.Count; i++)
            {
                float dist = Vector3.Distance(playerPosition, state.GroundItems[i].Position);
                if (dist > LootRange) continue;
                float score = ScoreInteractable(playerPosition, facingDirection, state.GroundItems[i].Position, dist);
                if (score < bestScore)
                {
                    bestScore = score;
                    result.Id = state.GroundItems[i].Id;
                    result.Type = InteractableType.GroundItem;
                }
            }

            for (int i = 0; i < state.Workbenches.Count; i++)
            {
                float dist = Vector3.Distance(playerPosition, state.Workbenches[i].Position);
                if (dist > LootRange) continue;
                float score = ScoreInteractable(playerPosition, facingDirection, state.Workbenches[i].Position, dist);
                if (score < bestScore)
                {
                    bestScore = score;
                    result.Id = state.Workbenches[i].Id;
                    result.Type = InteractableType.Workbench;
                }
            }

            return result;
        }

        static float ScoreInteractable(Vector3 playerPos, Vector3 facing, Vector3 targetPos, float distance)
        {
            if (distance < 0.01f) return 0f;
            var dirToTarget = targetPos - playerPos;
            dirToTarget.y = 0f;
            var flatFacing = facing;
            flatFacing.y = 0f;
            if (dirToTarget.sqrMagnitude < 0.0001f || flatFacing.sqrMagnitude < 0.0001f)
                return distance;
            float dot = Vector3.Dot(flatFacing.normalized, dirToTarget.normalized);
            return distance * (1f - 0.5f * dot);
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
