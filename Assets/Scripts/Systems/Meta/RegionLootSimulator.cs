using System;
using System.Collections.Generic;
using Constants;
using State;
using UnityEngine;

namespace Systems.Meta
{
    /// <summary>
    /// Pure loot-roll helpers behind the DevCheats <c>🌍 Meta → Region raid simulator</c>.
    /// Re-rolls the same dice <see cref="LootSystem"/> uses at container / loose-loot /
    /// bot-body spawn, but emits a flat list instead of mutating a <c>RaidState</c> — so an
    /// editor tool can answer "what would I carry out if I looted this whole region?"
    /// without a live raid.
    ///
    /// Stateless, Unity value-types only (CLAUDE.md §3), no <c>App</c> / no editor deps.
    /// </summary>
    public static class RegionLootSimulator
    {
        const string SimAmmoBase = "Ammo_Rifle";

        // A looted gun's worth for the "grab the most valuable" sort. Weapons carry no
        // shop price (they're assembled from cores, not stocked), so we synthesize one:
        // a solid base + a bump per rarity tier across both cores, keeping guns near the
        // top of the pack and better guns above worse ones.
        const long WeaponBaseValue = 220;
        const long WeaponRarityStep = 120;

        public struct Rolled
        {
            public string DefinitionId;
            public int Count;
            public bool IsWeapon;
            public WeaponConfiguration Weapon;
            // Durability as a fraction of max for armor drops; <0 = leave default (full).
            public float DurabilityFrac;

            public Rolled(string id, int count)
            {
                DefinitionId = id; Count = count; IsWeapon = false; Weapon = default; DurabilityFrac = -1f;
            }

            public static Rolled MakeWeapon(WeaponConfiguration cfg)
                => new() { DefinitionId = "Weapon", Count = 1, IsWeapon = true, Weapon = cfg, DurabilityFrac = -1f };

            public static Rolled MakeArmor(string id, float durabilityFrac)
                => new() { DefinitionId = id, Count = 1, DurabilityFrac = durabilityFrac };
        }

        // ─────────────────────────────────────────── Containers ──

        public static void RollContainer(in ContainerTypeConfig cfg, List<Rolled> outItems)
        {
            var pool = cfg.PossibleDrops;
            if (pool == null || pool.Length == 0) return;

            int capacity = cfg.SlotCount > 0 ? cfg.SlotCount : ContainerTypeConfig.DefaultSlotCount;
            int dropCount = Mathf.Min(UnityEngine.Random.Range(cfg.MinDrops, cfg.MaxDrops + 1), capacity);

            float totalWeight = 0f;
            for (int i = 0; i < pool.Length; i++) totalWeight += pool[i].Weight;
            if (totalWeight <= 0f) return;

            for (int i = 0; i < dropCount; i++)
            {
                var drop = PickWeighted(pool, totalWeight);
                int count = UnityEngine.Random.Range(drop.MinCount, drop.MaxCount + 1);
                var def = ItemDefinition.Get(drop.DefinitionId);
                if (def != null) count = Mathf.Min(count, Mathf.Max(1, def.MaxStackSize));
                if (count > 0) outItems.Add(new Rolled(drop.DefinitionId, count));
            }
        }

        // ────────────────────────────────────────── Loose loot ──

        public static void RollLooseGroup(ItemGroup group, List<Rolled> outItems)
            => RollUniform(ItemGroups.GetDrops(group), outItems);

        static void RollUniform(LootDrop[] drops, List<Rolled> outItems)
        {
            if (drops == null || drops.Length == 0) return;
            var d = drops[UnityEngine.Random.Range(0, drops.Length)];
            int count = Mathf.Max(1, UnityEngine.Random.Range(d.MinCount, d.MaxCount + 1));
            if (!string.IsNullOrEmpty(d.DefinitionId)) outItems.Add(new Rolled(d.DefinitionId, count));
        }

        public static void RollLooseCustom(IReadOnlyList<(string id, int min, int max)> custom, List<Rolled> outItems)
        {
            if (custom == null || custom.Count == 0) return;
            var pick = custom[UnityEngine.Random.Range(0, custom.Count)];
            if (string.IsNullOrEmpty(pick.id)) return;
            int count = Mathf.Max(1, UnityEngine.Random.Range(pick.min, pick.max + 1));
            outItems.Add(new Rolled(pick.id, count));
        }

        // ─────────────────────────────────────────── Bot bodies ──

        /// <summary>
        /// Approximates <see cref="LootSystem.CreateLootable"/>'s body drop from the static
        /// <see cref="BotTypeConfig"/>: the bot's assembled WEAPON, caliber ammo, meds,
        /// grenades, helmet + body armor, plus the loot table (ammo variants / guaranteed
        /// items / category loot) when set.
        /// </summary>
        public static void RollBot(in BotTypeConfig cfg, List<Rolled> outItems)
        {
            // The gun the bot carries — the headline drop. Pool-equipped bots have no fixed
            // config; those fall back to no gun (weapon pools resolve only at live spawn).
            if (IsValidWeapon(cfg.WeaponConfig))
                outItems.Add(Rolled.MakeWeapon(cfg.WeaponConfig));

            if (cfg.HasLootTable)
            {
                if (cfg.AmmoLoot.HasValue) RollAmmoVariants(cfg.AmmoLoot.Value, outItems);

                if (cfg.GuaranteedItems != null)
                    foreach (var rule in cfg.GuaranteedItems)
                    {
                        if (string.IsNullOrEmpty(rule.Id)) continue;
                        int c = UnityEngine.Random.Range(rule.Min, rule.Max + 1);
                        if (c > 0) outItems.Add(new Rolled(rule.Id, c));
                    }

                if (cfg.CategoryLoot != null)
                    foreach (var rule in cfg.CategoryLoot) RollCategory(rule, outItems);
            }
            else
            {
                outItems.Add(new Rolled(SimAmmoBase, 30));
                if (cfg.MedkitCount > 0) outItems.Add(new Rolled("Medkit", cfg.MedkitCount));
            }

            int grenades = cfg.GrenadeMaxCount > 0
                ? UnityEngine.Random.Range(cfg.GrenadeMinCount, cfg.GrenadeMaxCount + 1)
                : cfg.GrenadeCount;
            if (grenades > 0) outItems.Add(new Rolled("Grenade", grenades));

            if (!string.IsNullOrEmpty(cfg.HelmetDefinitionId))
                outItems.Add(Rolled.MakeArmor(cfg.HelmetDefinitionId,
                    RollWear(cfg.HelmetDurabilityMin, cfg.HelmetDurabilityMax)));
            if (!string.IsNullOrEmpty(cfg.BodyArmorDefinitionId))
                outItems.Add(Rolled.MakeArmor(cfg.BodyArmorDefinitionId,
                    RollWear(cfg.BodyArmorDurabilityMin, cfg.BodyArmorDurabilityMax)));
        }

        // Durability fraction in [min, max], clamped 0..1. Pristine (1,1) → 1.
        static float RollWear(float min, float max)
        {
            float lo = Mathf.Clamp01(Mathf.Min(min, max));
            float hi = Mathf.Clamp01(Mathf.Max(min, max));
            return UnityEngine.Random.Range(lo, hi);
        }

        static bool IsValidWeapon(in WeaponConfiguration c)
            => !string.IsNullOrEmpty(c.Payload.DefinitionId) && !string.IsNullOrEmpty(c.Delivery.DefinitionId);

        static void RollAmmoVariants(in AmmoLootRule rule, List<Rolled> outItems)
        {
            var ids = new List<string>(3);
            var weights = new List<float>(3);
            void TryAdd(string id, float w)
            {
                if (w > 0f && ItemDefinition.Get(id) != null) { ids.Add(id); weights.Add(w); }
            }
            TryAdd(SimAmmoBase, rule.StandardWeight);
            TryAdd(SimAmmoBase + "_AP", rule.ApWeight);
            TryAdd(SimAmmoBase + "_HP", rule.HpWeight);
            if (ids.Count == 0) return;

            float total = 0f;
            for (int i = 0; i < weights.Count; i++) total += weights[i];
            float r = UnityEngine.Random.value * total;
            int chosen = ids.Count - 1;
            for (int i = 0; i < ids.Count; i++) { r -= weights[i]; if (r <= 0f) { chosen = i; break; } }

            int rounds = UnityEngine.Random.Range(rule.MinRounds, rule.MaxRounds + 1);
            if (rounds > 0) outItems.Add(new Rolled(ids[chosen], rounds));
        }

        static void RollCategory(in CategoryLootRule rule, List<Rolled> outItems)
        {
            var cat = LootConstants.ToItemCategory(rule.Category);
            if (cat == ItemCategory.None) return;

            var candidates = new List<ItemDefinition>();
            foreach (var d in ItemDefinition.Registry.Values)
                if (d.Category == cat) candidates.Add(d);
            if (candidates.Count == 0) return;

            int picks = Mathf.Min(UnityEngine.Random.Range(rule.MinPicks, rule.MaxPicks + 1), candidates.Count);
            for (int p = 0; p < picks; p++)
            {
                float total = 0f;
                for (int i = 0; i < candidates.Count; i++) total += ItemBalanceAsset.DropWeightOf(candidates[i].Id);
                float r = UnityEngine.Random.value * total;
                int chosen = candidates.Count - 1;
                for (int i = 0; i < candidates.Count; i++)
                {
                    r -= ItemBalanceAsset.DropWeightOf(candidates[i].Id);
                    if (r <= 0f) { chosen = i; break; }
                }
                outItems.Add(new Rolled(candidates[chosen].Id, 1));
                candidates.RemoveAt(chosen);
            }
        }

        static LootDrop PickWeighted(LootDrop[] pool, float totalWeight)
        {
            float r = UnityEngine.Random.value * totalWeight, acc = 0f;
            for (int i = 0; i < pool.Length; i++)
            {
                acc += pool[i].Weight;
                if (r <= acc) return pool[i];
            }
            return pool[pool.Length - 1];
        }

        // ───────────────────────────────── Carry (most valuable) ──

        public struct FillResult
        {
            public int SlotsUsed;
            public int SlotsCapacity;
            public int UnitsBanked;
            public long ValueBanked;
            public int DistinctBanked;
            public int WeaponsBanked;
            public List<Rolled> Skipped; // items that didn't make the cut (backpack full)
        }

        /// <summary>
        /// Merges the rolled loot with whatever is ALREADY in the backpack, then keeps the
        /// most valuable items that fit — "grab the better stuff, drop the garbage". Existing
        /// weapons / armor keep their config + durability; stackables are re-consolidated.
        /// Anything that didn't make the cut lands in <see cref="FillResult.Skipped"/>.
        /// </summary>
        public static FillResult FillBackpackByValue(InventoryState inv, List<Rolled> rolled, Func<EId> alloc)
        {
            var result = new FillResult { Skipped = new List<Rolled>() };
            if (inv == null) return result;
            rolled ??= new List<Rolled>();

            // Non-stackables keep their concrete ItemState (durability / weapon config /
            // medkit charge). Stackables merge into per-def counts, then split into stacks.
            var singles = new List<ItemState>();
            var stackCounts = new Dictionary<string, int>();

            void AddStackable(string id, int count)
            {
                if (count <= 0) return;
                stackCounts.TryGetValue(id, out var c);
                stackCounts[id] = c + count;
            }

            // Existing backpack contents.
            for (int i = 0; i < inv.Backpack.Length; i++)
            {
                var it = inv.Backpack[i];
                if (it == null) continue;
                var def = it.Definition;
                if (def != null && def.IsStackable) AddStackable(it.DefinitionId, Mathf.Max(1, it.StackCount));
                else singles.Add(it);
            }

            // Newly rolled loot.
            foreach (var r in rolled)
            {
                if (r.IsWeapon)
                {
                    singles.Add(ItemState.CreateWeapon(alloc(), "Weapon", r.Weapon));
                    continue;
                }
                if (string.IsNullOrEmpty(r.DefinitionId) || r.Count <= 0) continue;
                var def = ItemDefinition.Get(r.DefinitionId);
                if (def == null) continue;
                if (def.IsStackable)
                {
                    AddStackable(r.DefinitionId, r.Count);
                }
                else
                {
                    for (int k = 0; k < r.Count; k++)
                    {
                        var item = ItemState.Create(alloc(), r.DefinitionId);
                        ApplyWear(item, def, r.DurabilityFrac);
                        singles.Add(item);
                    }
                }
            }

            // Materialize stacks into slot-sized ItemStates.
            var candidates = new List<ItemState>(singles);
            foreach (var kv in stackCounts)
            {
                var def = ItemDefinition.Get(kv.Key);
                int maxStack = def != null ? Mathf.Max(1, def.MaxStackSize) : 1;
                int remaining = kv.Value;
                while (remaining > 0)
                {
                    int n = Mathf.Min(remaining, maxStack);
                    candidates.Add(ItemState.Create(alloc(), kv.Key, n));
                    remaining -= n;
                }
            }

            // Most valuable first, then pour into the pack until full.
            candidates.Sort((a, b) => ValueOfItem(b).CompareTo(ValueOfItem(a)));

            int cap = inv.Backpack.Length;
            for (int i = 0; i < cap; i++) inv.Backpack[i] = null;

            int placed = 0;
            for (int i = 0; i < candidates.Count; i++)
            {
                var it = candidates[i];
                if (placed < cap)
                {
                    inv.Backpack[placed++] = it;
                    result.UnitsBanked += Mathf.Max(1, it.StackCount);
                    result.ValueBanked += ValueOfItem(it);
                    result.DistinctBanked++;
                    if (it.HasWeaponConfiguration) result.WeaponsBanked++;
                }
                else
                {
                    result.Skipped.Add(it.HasWeaponConfiguration
                        ? Rolled.MakeWeapon(it.WeaponConfiguration)
                        : new Rolled(it.DefinitionId, Mathf.Max(1, it.StackCount)));
                }
            }

            inv.Version++;
            result.SlotsCapacity = cap;
            result.SlotsUsed = placed;
            return result;
        }

        /// <summary>Worth of one backpack item for the "keep the most valuable" sort.</summary>
        public static long ValueOfItem(ItemState it)
        {
            if (it == null) return 0;
            if (it.HasWeaponConfiguration) return WeaponValue(it.WeaponConfiguration);
            int unit = ItemBalanceAsset.PriceOf(it.DefinitionId);
            if (unit <= 0) unit = it.Definition?.Value ?? 0;
            return (long)unit * Mathf.Max(1, it.StackCount);
        }

        static long WeaponValue(in WeaponConfiguration c)
        {
            int rarity = (int)c.Payload.Rarity + (int)c.Delivery.Rarity;
            return WeaponBaseValue + rarity * WeaponRarityStep;
        }

        // Stamps rolled combat wear onto a looted armor item (matches LootSystem.DropArmor,
        // which carries the bot's ArmorState durability). No-op for non-armor / full drops.
        static void ApplyWear(ItemState item, ItemDefinition def, float frac)
        {
            if (item == null || def == null || frac < 0f || def.MaxDurability <= 0f) return;
            item.MaxDurability = def.MaxDurability;
            item.CurrentDurability = Mathf.Clamp01(frac) * def.MaxDurability;
        }
    }
}
