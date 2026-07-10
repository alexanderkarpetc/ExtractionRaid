using System.Collections.Generic;
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

            int backpackSlot = 0;

            // Tier 4a — drop bot's actual weapon з current ammo state. Reconstruct
            // WeaponConfiguration from bot.Weapon fields → ItemState carries that config.
            // Placed in the BACKPACK (not an equipment slot): the loot panel only renders
            // backpack slots, so equipment-slot loot would be invisible/unlootable. The
            // player drags it onto a weapon slot after looting (AllowedSlots = Weapon|Backpack).
            if (bot.Weapon != null)
            {
                var droppedConfig = new WeaponConfiguration(
                    payload:        bot.Weapon.PayloadCore,
                    delivery:       bot.Weapon.DeliveryCore,
                    exotic:         bot.Weapon.HasExotic ? bot.Weapon.ExoticMod : (ExoticModInstance?)null,
                    ammoInMagazine: bot.Weapon.AmmoInMagazine);
                var weaponItemId = state.AllocateEId();
                inventory.Backpack[backpackSlot++] = ItemState.CreateWeapon(weaponItemId, "Weapon", droppedConfig);
            }

            // Grenades the bot was carrying (leftovers after combat) — always drop, like the
            // weapon and armor. The carried count comes from the loot config (see BotSpawnSystem).
            int grenades = bot.Blackboard.GrenadesRemaining;
            for (int i = 0; i < grenades && backpackSlot < InventoryState.BackpackSize; i++)
                inventory.Backpack[backpackSlot++] = ItemState.Create(state.AllocateEId(), "Grenade");

            // Extra loot: a BotLootConfigAsset (if assigned) fully drives ammo / meds /
            // category loot; otherwise the legacy default drop applies. Weapon + armor
            // always drop from the bot's equipment regardless.
            if (config.HasLootTable)
                ApplyLootTable(state, inventory, ref backpackSlot, bot, in config);
            else
                ApplyDefaultDrop(state, inventory, ref backpackSlot, bot);

            DropArmor(state, inventory, ref backpackSlot, bot, in config);

            var lootable = LootableContainerState.Create(id, bot.Position, config.TypeId, inventory);
            state.Lootables.Add(lootable);
            events.LootableSpawned(id, bot.Position, config.TypeId);
        }

        // Legacy default drop (bots with no loot table): caliber ammo, carried
        // meds/bandages, and a small chance of an attachment mod. (Grenades drop
        // unconditionally in CreateLootable, so they're not repeated here.)
        static void ApplyDefaultDrop(RaidState state, InventoryState inventory, ref int backpackSlot,
            BotEntityState bot)
        {
            // Ammo derived з payload's AmmoType (e.g. BallisticRound → "Ammo_Rifle").
            var ammoDefId = bot.Weapon?.PayloadDefinition?.AmmoType;
            if (!string.IsNullOrEmpty(ammoDefId))
                DropStacks(state, inventory, ref backpackSlot, ammoDefId, 30);

            int medkits = bot.Blackboard.MedkitsRemaining;
            for (int i = 0; i < medkits && backpackSlot < InventoryState.BackpackSize; i++)
                inventory.Backpack[backpackSlot++] = ItemState.Create(state.AllocateEId(), "Medkit", 1);

            DropStacks(state, inventory, ref backpackSlot, "Bandage", bot.Blackboard.BandagesRemaining);

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
        }

        // Config-driven drop (bots with a BotLootConfigAsset).
        static void ApplyLootTable(RaidState state, InventoryState inventory, ref int backpackSlot,
            BotEntityState bot, in BotTypeConfig config)
        {
            if (config.AmmoLoot.HasValue)
                DropAmmoVariants(state, inventory, ref backpackSlot, bot, config.AmmoLoot.Value);

            if (config.GuaranteedItems != null)
            {
                foreach (var rule in config.GuaranteedItems)
                {
                    if (backpackSlot >= InventoryState.BackpackSize) break;
                    int count = Random.Range(rule.Min, rule.Max + 1);
                    DropStacks(state, inventory, ref backpackSlot, rule.Id, count);
                }
            }

            if (config.CategoryLoot != null)
            {
                foreach (var rule in config.CategoryLoot)
                {
                    if (backpackSlot >= InventoryState.BackpackSize) break;
                    DropCategoryLoot(state, inventory, ref backpackSlot, rule);
                }
            }
        }

        // Ammo drop for the gun's OWN caliber, weighted across Standard / AP / HP variants.
        // Variants absent from the registry (e.g. energy cells have no AP/HP) are skipped,
        // so a weight pointing at a missing variant simply doesn't contribute.
        static void DropAmmoVariants(RaidState state, InventoryState inventory, ref int backpackSlot,
            BotEntityState bot, in AmmoLootRule rule)
        {
            var baseAmmo = bot.Weapon?.PayloadDefinition?.AmmoType;
            if (string.IsNullOrEmpty(baseAmmo)) return;

            var ids = new List<string>(3);
            var weights = new List<float>(3);
            void TryAdd(string id, float w)
            {
                if (w > 0f && ItemDefinition.Get(id) != null)
                {
                    ids.Add(id);
                    weights.Add(w);
                }
            }
            TryAdd(baseAmmo, rule.StandardWeight);
            TryAdd(baseAmmo + "_AP", rule.ApWeight);
            TryAdd(baseAmmo + "_HP", rule.HpWeight);
            if (ids.Count == 0) return;

            float total = 0f;
            for (int i = 0; i < weights.Count; i++) total += weights[i];
            float r = Random.value * total;
            int chosen = ids.Count - 1;
            for (int i = 0; i < ids.Count; i++)
            {
                r -= weights[i];
                if (r <= 0f) { chosen = i; break; }
            }

            int rounds = Random.Range(rule.MinRounds, rule.MaxRounds + 1);
            DropStacks(state, inventory, ref backpackSlot, ids[chosen], rounds);
        }

        // Value-weighted pick of distinct items from a broad category (pricier = rarer).
        static void DropCategoryLoot(RaidState state, InventoryState inventory, ref int backpackSlot,
            in CategoryLootRule rule)
        {
            var cat = LootConstants.ToItemCategory(rule.Category);
            if (cat == ItemCategory.None) return;

            var candidates = new List<ItemDefinition>();
            foreach (var d in ItemDefinition.Registry.Values)
                if (d.Category == cat) candidates.Add(d);
            if (candidates.Count == 0) return;

            int picks = Mathf.Min(Random.Range(rule.MinPicks, rule.MaxPicks + 1), candidates.Count);
            for (int p = 0; p < picks && backpackSlot < InventoryState.BackpackSize; p++)
            {
                float total = 0f;
                for (int i = 0; i < candidates.Count; i++) total += LootConstants.ValueWeight(candidates[i].Value);

                float r = Random.value * total;
                int chosen = candidates.Count - 1;
                for (int i = 0; i < candidates.Count; i++)
                {
                    r -= LootConstants.ValueWeight(candidates[i].Value);
                    if (r <= 0f) { chosen = i; break; }
                }

                var def = candidates[chosen];
                candidates.RemoveAt(chosen); // distinct picks — no duplicates in one roll
                DropStacks(state, inventory, ref backpackSlot, def.Id, 1);
            }
        }

        // Armor loot — dropped into the backpack (same reason as the weapon), preserving
        // combat durability. Prefer the armor id recorded at spawn (covers armor rolled
        // from an equipment pool); fall back to the static config id.
        static void DropArmor(RaidState state, InventoryState inventory, ref int backpackSlot,
            BotEntityState bot, in BotTypeConfig config)
        {
            if (!state.ArmorMap.TryGetValue(bot.Id, out var armorSlots)) return;

            var helmetId = !string.IsNullOrEmpty(armorSlots.HelmetDefinitionId)
                ? armorSlots.HelmetDefinitionId
                : config.HelmetDefinitionId;
            if (armorSlots.Helmet != null && !armorSlots.Helmet.IsBroken
                && !string.IsNullOrEmpty(helmetId)
                && backpackSlot < InventoryState.BackpackSize)
            {
                var helmetItem = ItemState.Create(state.AllocateEId(), helmetId);
                helmetItem.CurrentDurability = armorSlots.Helmet.CurrentDurability;
                helmetItem.MaxDurability = armorSlots.Helmet.MaxDurability;
                inventory.Backpack[backpackSlot++] = helmetItem;
            }

            var bodyArmorId = !string.IsNullOrEmpty(armorSlots.BodyArmorDefinitionId)
                ? armorSlots.BodyArmorDefinitionId
                : config.BodyArmorDefinitionId;
            if (armorSlots.BodyArmor != null && !armorSlots.BodyArmor.IsBroken
                && !string.IsNullOrEmpty(bodyArmorId)
                && backpackSlot < InventoryState.BackpackSize)
            {
                var armorItem = ItemState.Create(state.AllocateEId(), bodyArmorId);
                armorItem.CurrentDurability = armorSlots.BodyArmor.CurrentDurability;
                armorItem.MaxDurability = armorSlots.BodyArmor.MaxDurability;
                inventory.Backpack[backpackSlot++] = armorItem;
            }
        }

        // Drops `count` of an item into the backpack, splitting across stacks by MaxStackSize.
        // Weapon-family ids go through WeaponItemFactory so they carry a valid configuration.
        static void DropStacks(RaidState state, InventoryState inventory, ref int backpackSlot,
            string itemId, int count)
        {
            if (string.IsNullOrEmpty(itemId) || count <= 0) return;
            var def = ItemDefinition.Get(itemId);
            if (def == null) return;

            bool isWeapon = WeaponItemFactory.IsKnownWeaponDefinition(itemId);
            int stackMax = Mathf.Max(1, def.MaxStackSize);
            while (count > 0 && backpackSlot < InventoryState.BackpackSize)
            {
                int add = Mathf.Min(count, stackMax);
                inventory.Backpack[backpackSlot++] = isWeapon
                    ? WeaponItemFactory.SpawnItem(state.AllocateEId(), itemId)
                    : ItemState.Create(state.AllocateEId(), itemId, add);
                count -= add;
            }
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
