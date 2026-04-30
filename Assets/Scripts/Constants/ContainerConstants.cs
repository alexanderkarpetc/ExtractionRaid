using System.Collections.Generic;

namespace Constants
{
    public readonly struct LootDrop
    {
        public readonly string DefinitionId;
        public readonly int MinCount;
        public readonly int MaxCount;

        public LootDrop(string definitionId, int minCount, int maxCount)
        {
            DefinitionId = definitionId;
            MinCount = minCount;
            MaxCount = maxCount;
        }
    }

    public readonly struct ContainerTypeConfig
    {
        public readonly string TypeId;
        public readonly string DisplayName;
        public readonly int MinDrops;
        public readonly int MaxDrops;
        public readonly LootDrop[] PossibleDrops;

        public ContainerTypeConfig(string typeId, string displayName, int minDrops, int maxDrops,
            LootDrop[] possibleDrops)
        {
            TypeId = typeId;
            DisplayName = displayName;
            MinDrops = minDrops;
            MaxDrops = maxDrops;
            PossibleDrops = possibleDrops;
        }
    }

    public enum ContainerType
    {
        MedContainer,
        AmmoBox,
        RandomLootBox,
        ModuleCache,
    }

    public static class ContainerConstants
    {
        public const int LootSlots = 8;

        // Tier 6 G2 — weapon module ids that can drop. Mirrors entries у
        // ItemDefinition.BuildRegistry (Tier 6 G1) so loot rolls produce real
        // build-able items. All Common rarity initially; Tier 4 layers rarity.
        public static readonly LootDrop[] WeaponModuleDrops =
        {
            new LootDrop("BallisticRound", 1, 1),
            new LootDrop("LaserCharge",    1, 1),
            new LootDrop("SingleAction",   1, 1),
            new LootDrop("Auto",           1, 1),
            new LootDrop("Scatter",        1, 1),
        };

        public static readonly ContainerTypeConfig MedContainer = new(
            typeId: "MedContainer",
            displayName: "Medical Supplies",
            minDrops: 2, maxDrops: 4,
            possibleDrops: new[]
            {
                new LootDrop("Medkit", 1, 1),
                new LootDrop("Bandage", 1, 1),
            }
        );

        // Cluster A (2026-05-01): Ammo_Pistol family retired from drop tables —
        // no current payload uses Pistol-caliber ammo. Pool tightened to Rifle-only.
        public static readonly ContainerTypeConfig AmmoBox = new(
            typeId: "AmmoBox",
            displayName: "Ammo Box",
            minDrops: 2, maxDrops: 4,
            possibleDrops: new[]
            {
                new LootDrop("Ammo_Rifle", 10, 40),
            }
        );

        public static readonly ContainerTypeConfig RandomLootBox = new(
            typeId: "RandomLootBox",
            displayName: "Loot Box",
            minDrops: 2, maxDrops: 4,
            // Tier 6 G2: weapon modules added alongside meds/ammo/grenades.
            // Uniform random pick ⇒ ~50% chance per drop slot is a module.
            // Per-module weighting → Tier 4 (з rarity layer).
            possibleDrops: new[]
            {
                new LootDrop("Medkit",         1, 1),
                new LootDrop("Bandage",        1, 1),
                new LootDrop("Grenade",        1, 1),
                new LootDrop("Ammo_Rifle",    10, 30),
                new LootDrop("BallisticRound", 1, 1),
                new LootDrop("LaserCharge",    1, 1),
                new LootDrop("SingleAction",   1, 1),
                new LootDrop("Auto",           1, 1),
                new LootDrop("Scatter",        1, 1),
            }
        );

        // Tier 6 G2 — dedicated cache for weapon modules. Smaller drop count
        // (1-2) but pool is module-only, so opening one always nets at least
        // one build-relevant part. Higher-value rarity than RandomLootBox.
        public static readonly ContainerTypeConfig ModuleCache = new(
            typeId: "ModuleCache",
            displayName: "Module Cache",
            minDrops: 1, maxDrops: 2,
            possibleDrops: WeaponModuleDrops
        );

        static readonly Dictionary<string, ContainerTypeConfig> Registry = new()
        {
            { MedContainer.TypeId,  MedContainer },
            { AmmoBox.TypeId,       AmmoBox },
            { RandomLootBox.TypeId, RandomLootBox },
            { ModuleCache.TypeId,   ModuleCache },
        };

        public static bool TryGetConfig(string typeId, out ContainerTypeConfig config)
        {
            return Registry.TryGetValue(typeId, out config);
        }

        public static bool TryGetConfig(ContainerType type, out ContainerTypeConfig config)
        {
            return Registry.TryGetValue(type.ToString(), out config);
        }
    }
}
