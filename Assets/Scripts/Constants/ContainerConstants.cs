using System.Collections.Generic;
using State;

namespace Constants
{
    /// <summary>
    /// A named drop: one specific item (or one assembled weapon preset) in a count range.
    /// Used for HARDCODED loot — a container's guaranteed contents, a bot's guaranteed items —
    /// where the designer wants that exact thing, not a roll. Set
    /// <see cref="CountFromBalance"/> to keep the item fixed but let
    /// <see cref="ItemBalanceAsset"/> decide the stack size.
    /// </summary>
    public readonly struct LootDrop
    {
        public readonly string DefinitionId;
        public readonly int MinCount;
        public readonly int MaxCount;

        /// <summary>When true the count range above is ignored and the stack size comes from
        /// the balance table's per-item drop-count range.</summary>
        public readonly bool CountFromBalance;

        /// <summary>
        /// Optional. When set (and valid), this drop spawns the fully-assembled weapon
        /// built from the preset instead of a plain item — <see cref="DefinitionId"/> and
        /// the count range are ignored for it. Mirrors the "Give Weapon Preset" devcheat.
        /// </summary>
        public readonly WeaponPresetDefinition WeaponPreset;

        public bool IsWeaponPreset => WeaponPreset != null && WeaponPreset.IsValid;

        public LootDrop(string definitionId, int minCount, int maxCount)
            : this(definitionId, minCount, maxCount, null) { }

        public LootDrop(string definitionId, int minCount, int maxCount,
            WeaponPresetDefinition weaponPreset, bool countFromBalance = false)
        {
            DefinitionId = definitionId;
            MinCount = minCount;
            MaxCount = maxCount;
            WeaponPreset = weaponPreset;
            CountFromBalance = countFromBalance;
        }

        /// <summary>Named item, stack size straight from the balance table.</summary>
        public static LootDrop FromBalance(string definitionId) =>
            new(definitionId, 1, 1, null, countFromBalance: true);

        /// <summary>An assembled weapon (the "starting chest always holds a pistol" case).</summary>
        public static LootDrop Preset(WeaponPresetDefinition preset) =>
            new(null, 1, 1, preset);
    }

    /// <summary>
    /// One weighted entry in a container's random pool. Either a whole
    /// <see cref="LootCategory"/> bucket — the item inside it is picked by the balance table's
    /// DropWeight — or one specific item. Either way the stack size comes from the balance
    /// table. The entry weight only shapes the container's MIX ("mostly ammo, some meds"),
    /// never an individual item's rarity.
    /// </summary>
    public readonly struct LootPoolEntry
    {
        public readonly bool IsCategory;
        public readonly LootCategory Category;
        public readonly string DefinitionId;
        public readonly float Weight;

        LootPoolEntry(bool isCategory, LootCategory category, string definitionId, float weight)
        {
            IsCategory = isCategory;
            Category = category;
            DefinitionId = definitionId;
            Weight = weight;
        }

        public static LootPoolEntry FromCategory(LootCategory category, float weight = 1f) =>
            new(true, category, null, weight > 0f ? weight : 0f);

        public static LootPoolEntry FromItem(string definitionId, float weight = 1f) =>
            new(false, default, definitionId, weight > 0f ? weight : 0f);
    }

    /// <summary>
    /// What a container spawns. Three independent knobs:
    ///
    ///   • <see cref="GuaranteedDrops"/> — hardcoded contents, always present, in order.
    ///   • <see cref="MinDrops"/>/<see cref="MaxDrops"/> — how many extra entries to roll.
    ///   • <see cref="RandomPool"/> — WHAT those rolls can be (category buckets / named items).
    ///
    /// Item rarity inside a bucket and every rolled stack size come from
    /// <see cref="ItemBalanceAsset"/>, so retuning the economy is a one-asset edit.
    /// </summary>
    public readonly struct ContainerTypeConfig
    {
        public const int DefaultSlotCount = 20;

        public readonly string TypeId;
        public readonly string DisplayName;
        public readonly int SlotCount;
        public readonly int MinDrops;
        public readonly int MaxDrops;
        public readonly LootPoolEntry[] RandomPool;
        public readonly LootDrop[] GuaranteedDrops;

        public ContainerTypeConfig(string typeId, string displayName, int minDrops, int maxDrops,
            LootPoolEntry[] randomPool, LootDrop[] guaranteedDrops = null,
            int slotCount = DefaultSlotCount)
        {
            TypeId = typeId;
            DisplayName = displayName;
            SlotCount = slotCount > 0 ? slotCount : DefaultSlotCount;
            MinDrops = minDrops;
            MaxDrops = maxDrops;
            RandomPool = randomPool;
            GuaranteedDrops = guaranteedDrops;
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

        public static readonly ContainerTypeConfig MedContainer = new(
            typeId: "MedContainer",
            displayName: "Medical Supplies",
            minDrops: 2, maxDrops: 4,
            randomPool: new[] { LootPoolEntry.FromCategory(LootCategory.Meds) }
        );

        public static readonly ContainerTypeConfig AmmoBox = new(
            typeId: "AmmoBox",
            displayName: "Ammo Box",
            minDrops: 2, maxDrops: 4,
            randomPool: new[] { LootPoolEntry.FromCategory(LootCategory.Ammo) }
        );

        // General-purpose box: consumables + ammo lead, build parts are a modest slice (the
        // ModuleCache is the parts-dense source). Which medkit / which caliber / which mod
        // comes out — and how many — is ItemBalance's call.
        public static readonly ContainerTypeConfig RandomLootBox = new(
            typeId: "RandomLootBox",
            displayName: "Loot Box",
            minDrops: 2, maxDrops: 4,
            randomPool: new[]
            {
                LootPoolEntry.FromCategory(LootCategory.Meds,        2f),
                LootPoolEntry.FromCategory(LootCategory.Ammo,        3f),
                LootPoolEntry.FromCategory(LootCategory.Throwables,  1f),
                LootPoolEntry.FromCategory(LootCategory.Materials,   2f),
                LootPoolEntry.FromCategory(LootCategory.WeaponCores, 1f),
                LootPoolEntry.FromCategory(LootCategory.Attachments, 1f),
            }
        );

        // Dedicated build-parts cache: cores + attachments only, so opening one always nets a
        // build-relevant part.
        public static readonly ContainerTypeConfig ModuleCache = new(
            typeId: "ModuleCache",
            displayName: "Module Cache",
            minDrops: 1, maxDrops: 3,
            randomPool: new[]
            {
                LootPoolEntry.FromCategory(LootCategory.WeaponCores, 1f),
                LootPoolEntry.FromCategory(LootCategory.Attachments, 1f),
            }
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

        public static void RegisterOrOverride(ContainerTypeConfig config)
        {
            if (string.IsNullOrEmpty(config.TypeId)) return;
            Registry[config.TypeId] = config;
        }
    }
}
