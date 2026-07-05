using Adapters;
using Constants;
using State;
using UnityEngine;

namespace Systems
{
    public enum InteractableType : byte { None, Lootable, GroundItem, Workbench, DeployPoint, Npc }

    public struct InteractableResult
    {
        public EId Id;
        public InteractableType Type;
        public bool IsValid => Id.IsValid;
    }

    public static class LootSystem
    {
        public const float LootRange = 3f;

        // Chance a killed bot also drops one weapon attachment mod (loot-gated economy).
        // Placeholder — promote to DevCheats if runtime tuning is wanted.
        const float BotModDropChance = 0.25f;

        public static void CreateContainer(RaidState state, in ContainerTypeConfig config, Vector3 position,
            IRaidEvents events)
        {
            var id = state.AllocateEId();
            var inventory = new InventoryState();

            int capacity = Mathf.Min(
                config.SlotCount > 0 ? config.SlotCount : InventoryState.BackpackSize,
                inventory.Backpack.Length);

            int rolledCount = Random.Range(config.MinDrops, config.MaxDrops + 1);
            int dropCount = Mathf.Min(rolledCount, capacity);

            var pool = config.PossibleDrops;
            float totalWeight = 0f;
            if (pool != null)
                for (int i = 0; i < pool.Length; i++)
                    totalWeight += pool[i].Weight;

            int slot = 0;
            for (int i = 0; i < dropCount && slot < capacity; i++)
            {
                if (pool == null || pool.Length == 0 || totalWeight <= 0f) break;

                var drop = PickWeighted(pool, totalWeight);
                var itemId = state.AllocateEId();
                int count = Random.Range(drop.MinCount, drop.MaxCount + 1);

                var def = ItemDefinition.Get(drop.DefinitionId);
                if (def != null)
                    count = Mathf.Min(count, def.MaxStackSize);

                inventory.Backpack[slot++] = WeaponItemFactory.IsKnownWeaponDefinition(drop.DefinitionId)
                    ? WeaponItemFactory.SpawnItem(itemId, drop.DefinitionId)
                    : ItemState.Create(itemId, drop.DefinitionId, count);
            }

            var lootable = LootableContainerState.Create(id, position, config.TypeId, inventory, isContainer: true);
            state.Lootables.Add(lootable);
            events.LootableSpawned(id, position, config.TypeId);
        }

        static LootDrop PickWeighted(LootDrop[] pool, float totalWeight)
        {
            float r = Random.value * totalWeight;
            float acc = 0f;
            for (int i = 0; i < pool.Length; i++)
            {
                acc += pool[i].Weight;
                if (r <= acc) return pool[i];
            }
            return pool[pool.Length - 1];
        }

        public static void CreateLootable(RaidState state, BotEntityState bot, in BotTypeConfig config,
            IRaidEvents events)
        {
            var id = state.AllocateEId();
            var inventory = new InventoryState();

            // Tier 4a — drop bot's actual weapon з current ammo state. Reconstruct
            // WeaponConfiguration from bot.Weapon fields → ItemState carries that config.
            // Player can pick up + equip → goes through same WeaponSyncSystem.BuildWeaponForItem.
            if (bot.Weapon != null)
            {
                var droppedConfig = new WeaponConfiguration(
                    payload:        bot.Weapon.PayloadCore,
                    delivery:       bot.Weapon.DeliveryCore,
                    exotic:         bot.Weapon.HasExotic ? bot.Weapon.ExoticMod : (ExoticModInstance?)null,
                    ammoInMagazine: bot.Weapon.AmmoInMagazine);
                var weaponItemId = state.AllocateEId();
                inventory.WeaponSlots[0] = ItemState.CreateWeapon(weaponItemId, "Weapon", droppedConfig);
            }

            int backpackSlot = 0;

            // Ammo derived з payload's AmmoType (e.g. BallisticRound → "Ammo_Rifle").
            var ammoDefId = bot.Weapon?.PayloadDefinition?.AmmoType;
            if (!string.IsNullOrEmpty(ammoDefId))
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

            // Bandages — packed into stacks (MaxStackSize) rather than one slot each.
            int bandages = bot.Blackboard.BandagesRemaining;
            int bandageStack = ItemDefinition.Get("Bandage")?.MaxStackSize ?? 1;
            while (bandages > 0 && backpackSlot < InventoryState.BackpackSize)
            {
                int add = bandages < bandageStack ? bandages : bandageStack;
                inventory.Backpack[backpackSlot++] = ItemState.Create(state.AllocateEId(), "Bandage", add);
                bandages -= add;
            }

            // Armor loot — preserve durability from combat
            if (state.ArmorMap.TryGetValue(bot.Id, out var armorSlots))
            {
                if (armorSlots.Helmet != null && !armorSlots.Helmet.IsBroken
                    && config.HelmetDefinitionId != null)
                {
                    var helmetItem = ItemState.Create(state.AllocateEId(), config.HelmetDefinitionId);
                    helmetItem.CurrentDurability = armorSlots.Helmet.CurrentDurability;
                    helmetItem.MaxDurability = armorSlots.Helmet.MaxDurability;
                    inventory.HelmetSlot = helmetItem;
                }
                if (armorSlots.BodyArmor != null && !armorSlots.BodyArmor.IsBroken
                    && config.BodyArmorDefinitionId != null)
                {
                    var armorItem = ItemState.Create(state.AllocateEId(), config.BodyArmorDefinitionId);
                    armorItem.CurrentDurability = armorSlots.BodyArmor.CurrentDurability;
                    armorItem.MaxDurability = armorSlots.BodyArmor.MaxDurability;
                    inventory.BodyArmorSlot = armorItem;
                }
            }

            // Chance to also drop a weapon attachment mod (universal common, unique rare — see
            // ContainerConstants.AttachmentModDrops). Weighted pick from the shared mod pool.
            if (backpackSlot < InventoryState.BackpackSize && Random.value < BotModDropChance)
            {
                var modPool = ContainerConstants.AttachmentModDrops();
                float total = 0f;
                for (int i = 0; i < modPool.Length; i++) total += modPool[i].Weight;
                if (total > 0f)
                {
                    var drop = PickWeighted(modPool, total);
                    inventory.Backpack[backpackSlot++] = ItemState.Create(state.AllocateEId(), drop.DefinitionId, 1);
                }
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

            for (int i = 0; i < state.DeployPoints.Count; i++)
            {
                float dist = Vector3.Distance(playerPosition, state.DeployPoints[i].Position);
                if (dist > LootRange) continue;
                float score = ScoreInteractable(playerPosition, facingDirection, state.DeployPoints[i].Position, dist);
                if (score < bestScore)
                {
                    bestScore = score;
                    result.Id = state.DeployPoints[i].Id;
                    result.Type = InteractableType.DeployPoint;
                }
            }

            for (int i = 0; i < state.Npcs.Count; i++)
            {
                float dist = Vector3.Distance(playerPosition, state.Npcs[i].Position);
                if (dist > LootRange) continue;
                float score = ScoreInteractable(playerPosition, facingDirection, state.Npcs[i].Position, dist);
                if (score < bestScore)
                {
                    bestScore = score;
                    result.Id = state.Npcs[i].Id;
                    result.Type = InteractableType.Npc;
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

    }
}
