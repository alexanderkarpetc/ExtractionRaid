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
    }

    public static class ContainerConstants
    {
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

        public static readonly ContainerTypeConfig AmmoBox = new(
            typeId: "AmmoBox",
            displayName: "Ammo Box",
            minDrops: 2, maxDrops: 4,
            possibleDrops: new[]
            {
                new LootDrop("Ammo_Rifle", 10, 40),
                new LootDrop("Ammo_Shotgun", 4, 14),
                new LootDrop("Ammo_Pistol", 8, 24),
            }
        );

        public static readonly ContainerTypeConfig RandomLootBox = new(
            typeId: "RandomLootBox",
            displayName: "Loot Box",
            minDrops: 2, maxDrops: 4,
            possibleDrops: new[]
            {
                new LootDrop("Medkit", 1, 1),
                new LootDrop("Bandage", 1, 1),
                new LootDrop("Grenade", 1, 1),
                new LootDrop("Ammo_Rifle", 10, 30),
                new LootDrop("Ammo_Shotgun", 4, 10),
                new LootDrop("Ammo_Pistol", 8, 18),
            }
        );

        static readonly Dictionary<string, ContainerTypeConfig> Registry = new()
        {
            { MedContainer.TypeId, MedContainer },
            { AmmoBox.TypeId, AmmoBox },
            { RandomLootBox.TypeId, RandomLootBox },
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
