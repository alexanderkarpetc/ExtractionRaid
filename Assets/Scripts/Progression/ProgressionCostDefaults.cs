using System.Collections.Generic;
using State;

namespace Progression
{
    /// <summary>
    /// The material-cost curve for the default tree: what each node asks for on top of its
    /// skill point. Ported from the design concept
    /// (<c>Assets/Concepts/progression_tree_concept.html</c>).
    ///
    /// Shape of the curve — cost climbs outward, and a node of a given ring costs more the
    /// bigger it is:
    ///
    ///   ring 1-2 minor  → 1 common material, small stack
    ///   ring 3   minor  → common + uncommon
    ///   ring 4-5 minor  → common + uncommon + one rare component
    ///   notable         → skips the cheap scrap: uncommon + rare, and from ring 4 an
    ///                     assembled weapon (Delivery + Payload at a minimum rarity)
    ///   keystone        → the heaviest line of all: bulk uncommon + several rare + an
    ///                     Epic/Legendary weapon
    ///
    /// Material ids come from <see cref="ItemDefinition"/>'s crafting-material tiers, drawn from
    /// a per-discipline palette so Warden burns armour plate while Phantom burns optics. Quantities
    /// are picked deterministically from the node id, so re-seeding the asset is stable and a
    /// designer's inspector tweaks are the only source of drift.
    ///
    /// This is the *seed*: the numbers that ship live on the ProgressionTree asset next to the
    /// rest of the tree balance. Re-apply with <c>Raid → Progression → Reseed Node Costs</c>.
    /// </summary>
    public static class ProgressionCostDefaults
    {
        // ── material palettes (ids from ItemDefinition.Registry) ───
        // Common scrap — every discipline burns through it.
        static readonly string[] Scrap =
            { "Metal_Parts", "Adhesive", "Duct_Tape", "Plastic", "Springs", "Cloth" };

        // Uncommon mechanical/electrical modules, flavoured per discipline.
        static readonly Dictionary<string, string[]> Modules = new()
        {
            ["warden"]     = new[] { "Hydraulic_Seals", "Structural_Foam", "Synthetic_Fiber", "Aluminum" },
            ["phantom"]    = new[] { "Sensor_Module", "Camera_Optics", "Motion_Sensor", "Insulated_Wiring" },
            ["predator"]   = new[] { "Gear_Cluster", "Rotary_Motor", "Pneumatic_Valve", "Gunpowder" },
            ["prospector"] = new[] { "Cooling_Fan", "Pipes", "Mechanical_Parts", "Electronics" },
        };

        // Rare components that gate the deep nodes, also flavoured per discipline.
        static readonly Dictionary<string, string[]> Rares = new()
        {
            ["warden"]     = new[] { "Military_Components", "Magnetic_Alloy", "Resonance_Plate" },
            ["phantom"]    = new[] { "Smart_Targeting_Unit", "Pulse_Emitter", "Synthetic_Quartz" },
            ["predator"]   = new[] { "Gyro_Stabilizer", "Adaptive_Circuit", "Nano_Filament" },
            ["prospector"] = new[] { "Energy_Core", "Crystal_Matrix", "Quantum_Relay" },
        };

        static readonly string[] Deliveries = { "SingleAction", "Auto", "Scatter" };
        static readonly string[] Payloads   = { "BallisticRound", "LaserCharge" };

        /// <summary>
        /// Ceiling on the rarity a weapon cost line may demand.
        /// <b>Nothing in the game rolls above-Common cores yet</b> — the builder hardcodes
        /// <see cref="RarityTier.Common"/> and loot never upgrades it — so leaving this at
        /// Legendary ships ring-4+ notables and every keystone permanently unpayable.
        /// Drop it to <see cref="RarityTier.Common"/> to make the whole tree completable today,
        /// and raise it back once core rarity actually drops.
        /// </summary>
        public static RarityTier MaxWeaponRarity = RarityTier.Legendary;

        static RarityTier Cap(RarityTier r) => r > MaxWeaponRarity ? MaxWeaponRarity : r;

        static readonly string[] ScrapFallback = { "Metal_Parts" };

        /// <summary>Fills <see cref="ProgressionNodeDef.Cost"/> for every node, overwriting what's there.</summary>
        public static void Apply(List<ProgressionDisciplineDef> disciplines)
        {
            if (disciplines == null) return;
            foreach (var disc in disciplines)
                foreach (var branch in disc.Branches)
                    foreach (var node in branch.Nodes)
                        node.Cost = Build(disc.Id, node);
        }

        /// <summary>The cost curve itself: ring + size → what this node asks for.</summary>
        public static List<ProgressionCostEntry> Build(string disciplineId, ProgressionNodeDef node)
        {
            var cost = new List<ProgressionCostEntry>(3);
            uint h = Hash(node.Id);
            int ring = UnityEngine.Mathf.Clamp(node.Ring, 1, 5);
            var scrap   = Scrap;
            var modules = Modules.TryGetValue(disciplineId, out var m) ? m : ScrapFallback;
            var rares   = Rares.TryGetValue(disciplineId, out var r) ? r : ScrapFallback;

            switch (node.Size)
            {
                case NodeSize.Minor:
                    // Scrap stack grows ~4 per ring; ring 3 adds a module, ring 4+ a rare.
                    cost.Add(ProgressionCostEntry.Item_(Pick(scrap, h), 2 + (ring - 1) * 4 + (int)(h % 3)));
                    if (ring >= 3)
                        cost.Add(ProgressionCostEntry.Item_(Pick(modules, h >> 4), ring - 1 + (int)((h >> 7) % 2)));
                    if (ring >= 4)
                        cost.Add(ProgressionCostEntry.Item_(Pick(rares, h >> 11), ring - 3 + (int)((h >> 13) % 2)));
                    break;

                case NodeSize.Notable:
                    // No cheap scrap — notables start at the module tier and jump again at ring 5.
                    cost.Add(ProgressionCostEntry.Item_(Pick(modules, h), 3 + ring * 2 + (int)(h % 3)));
                    cost.Add(ProgressionCostEntry.Item_(Pick(rares, h >> 5), ring >= 5 ? 3 : 2));
                    if (ring >= 4)
                        cost.Add(ProgressionCostEntry.Weapon_(Pick(Deliveries, h >> 9), Pick(Payloads, h >> 12),
                            Cap(ring >= 5 ? RarityTier.Epic : RarityTier.Rare)));
                    break;

                default:   // Keystone — the end of a branch, and the most expensive thing in it.
                    cost.Add(ProgressionCostEntry.Item_(Pick(modules, h), 10 + (int)(h % 6)));
                    cost.Add(ProgressionCostEntry.Item_(Pick(rares, h >> 5), 4 + (int)((h >> 8) % 3)));
                    cost.Add(ProgressionCostEntry.Weapon_(Pick(Deliveries, h >> 12), Pick(Payloads, h >> 15),
                        Cap(h % 4 == 0 ? RarityTier.Legendary : RarityTier.Epic)));
                    break;
            }
            return cost;
        }

        // FNV-1a over the node id — stable across runs and platforms (unlike string.GetHashCode).
        static uint Hash(string s)
        {
            uint h = 2166136261;
            if (string.IsNullOrEmpty(s)) return h;
            foreach (var ch in s) { h ^= (uint)ch; h *= 16777619u; }
            return h;
        }

        static string Pick(string[] pool, uint seed) => pool[seed % (uint)pool.Length];
    }
}
