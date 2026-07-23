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
    /// bot-body spawn, but emits a flat (definitionId, count) list instead of mutating a
    /// <c>RaidState</c> — so an editor tool can answer "what would I carry out if I looted
    /// this whole region?" without a live raid.
    ///
    /// Stateless, Unity value-types only (CLAUDE.md §3), no <c>App</c> / no editor deps →
    /// unit-testable. The one deliberate simplification vs the real death drop: a bot's
    /// assembled weapon is NOT rolled (it needs a live <c>BotEntityState</c> weapon); this
    /// is a meta-loot simulator, not a combat replay.
    /// </summary>
    public static class RegionLootSimulator
    {
        // All current payloads fire the ballistic round → rifle ammo (mirrors
        // LootSystem's default drop). Simulated bodies drop this caliber.
        const string SimAmmoBase = "Ammo_Rifle";

        public struct Rolled
        {
            public string DefinitionId;
            public int Count;
            public Rolled(string id, int count) { DefinitionId = id; Count = count; }
        }

        // ─────────────────────────────────────────── Containers ──

        /// <summary>Mirrors <see cref="LootSystem.CreateContainer"/>: N weighted drops,
        /// each count-ranged and stack-capped.</summary>
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

        /// <summary>Uniform pick over the group (matches <c>LooseLootSpawnPoint.RollItem</c>).</summary>
        public static void RollLooseGroup(ItemGroup group, List<Rolled> outItems)
            => RollUniform(ItemGroups.GetDrops(group), outItems);

        static void RollUniform(LootDrop[] drops, List<Rolled> outItems)
        {
            if (drops == null || drops.Length == 0) return;
            var d = drops[UnityEngine.Random.Range(0, drops.Length)];
            int count = Mathf.Max(1, UnityEngine.Random.Range(d.MinCount, d.MaxCount + 1));
            if (!string.IsNullOrEmpty(d.DefinitionId)) outItems.Add(new Rolled(d.DefinitionId, count));
        }

        /// <summary>Uniform pick over a custom (definitionId, min, max) pool.</summary>
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
        /// <see cref="BotTypeConfig"/>: caliber ammo, meds, grenades, helmet + body armor,
        /// plus the loot table (ammo variants / guaranteed items / category loot) when set.
        /// The assembled weapon is intentionally omitted (see class summary).
        /// </summary>
        public static void RollBot(in BotTypeConfig cfg, List<Rolled> outItems)
        {
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
                // Legacy default drop: caliber ammo + carried meds.
                outItems.Add(new Rolled(SimAmmoBase, 30));
                if (cfg.MedkitCount > 0) outItems.Add(new Rolled("Medkit", cfg.MedkitCount));
            }

            int grenades = cfg.GrenadeMaxCount > 0
                ? UnityEngine.Random.Range(cfg.GrenadeMinCount, cfg.GrenadeMaxCount + 1)
                : cfg.GrenadeCount;
            if (grenades > 0) outItems.Add(new Rolled("Grenade", grenades));

            if (!string.IsNullOrEmpty(cfg.HelmetDefinitionId)) outItems.Add(new Rolled(cfg.HelmetDefinitionId, 1));
            if (!string.IsNullOrEmpty(cfg.BodyArmorDefinitionId)) outItems.Add(new Rolled(cfg.BodyArmorDefinitionId, 1));
        }

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
                candidates.RemoveAt(chosen); // distinct picks
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
            public int SlotsUsed;      // occupied backpack slots after the fill
            public int SlotsCapacity;  // total backpack slots
            public int UnitsBanked;    // item units that fit
            public long ValueBanked;   // Σ (unit value × units) that fit
            public int DistinctBanked; // distinct definitions banked
            public List<Rolled> Skipped; // units that didn't fit (pack full)
        }

        /// <summary>
        /// Consolidates rolled loot by definition, sorts by unit <see cref="ItemDefinition.Value"/>
        /// (desc), and pours it into free backpack slots via
        /// <see cref="Systems.InventorySystem.AddToBackpack"/> until the pack is full —
        /// "grab the most valuable you can carry, more slots ⇒ more loot". Anything that
        /// didn't fit lands in <see cref="FillResult.Skipped"/>.
        /// </summary>
        public static FillResult FillBackpackByValue(InventoryState inv, List<Rolled> rolled, Func<EId> alloc)
        {
            var result = new FillResult { Skipped = new List<Rolled>() };
            if (inv == null || rolled == null) return result;

            var byId = new Dictionary<string, int>();
            foreach (var r in rolled)
            {
                if (string.IsNullOrEmpty(r.DefinitionId) || r.Count <= 0) continue;
                byId.TryGetValue(r.DefinitionId, out var c);
                byId[r.DefinitionId] = c + r.Count;
            }

            var ordered = new List<KeyValuePair<string, int>>(byId);
            ordered.Sort((a, b) => UnitValue(b.Key).CompareTo(UnitValue(a.Key)));

            foreach (var kv in ordered)
            {
                int added = Systems.InventorySystem.AddToBackpack(inv, kv.Key, kv.Value, alloc);
                if (added > 0)
                {
                    result.UnitsBanked += added;
                    result.ValueBanked += (long)UnitValue(kv.Key) * added;
                    result.DistinctBanked++;
                }
                if (added < kv.Value) result.Skipped.Add(new Rolled(kv.Key, kv.Value - added));
            }

            result.SlotsCapacity = InventoryState.BackpackSize;
            int used = 0;
            for (int i = 0; i < inv.Backpack.Length; i++) if (inv.Backpack[i] != null) used++;
            result.SlotsUsed = used;
            return result;
        }

        static int UnitValue(string id)
        {
            var d = ItemDefinition.Get(id);
            return d != null ? d.Value : 0;
        }
    }
}
