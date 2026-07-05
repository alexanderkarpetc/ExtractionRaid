using System.Collections.Generic;

namespace Constants
{
    public readonly struct LootDrop
    {
        public readonly string DefinitionId;
        public readonly int MinCount;
        public readonly int MaxCount;
        public readonly float Weight;

        public LootDrop(string definitionId, int minCount, int maxCount)
            : this(definitionId, minCount, maxCount, 1f) { }

        public LootDrop(string definitionId, int minCount, int maxCount, float weight)
        {
            DefinitionId = definitionId;
            MinCount = minCount;
            MaxCount = maxCount;
            Weight = weight > 0f ? weight : 1f;
        }
    }

    public readonly struct ContainerTypeConfig
    {
        public const int DefaultSlotCount = 20;

        public readonly string TypeId;
        public readonly string DisplayName;
        public readonly int SlotCount;
        public readonly int MinDrops;
        public readonly int MaxDrops;
        public readonly LootDrop[] PossibleDrops;

        public ContainerTypeConfig(string typeId, string displayName, int minDrops, int maxDrops,
            LootDrop[] possibleDrops)
            : this(typeId, displayName, DefaultSlotCount, minDrops, maxDrops, possibleDrops) { }

        public ContainerTypeConfig(string typeId, string displayName, int slotCount,
            int minDrops, int maxDrops, LootDrop[] possibleDrops)
        {
            TypeId = typeId;
            DisplayName = displayName;
            SlotCount = slotCount > 0 ? slotCount : DefaultSlotCount;
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

        // Attachment mods (loot-gated economy). Ids match the AttachmentDefinition SOs +
        // ItemDefinition entries. Universal mods drop at full weight; the 3 unique
        // (archetype-restricted) mods are rarer (×0.4). `scale` tunes the whole pool's share
        // when concatenated into a mixed container. See docs/ai/weapon-builder/attachments.
        public static LootDrop[] AttachmentModDrops(float scale = 1f) => new[]
        {
            new LootDrop("PowerComp",     1, 1, 1f * scale),
            new LootDrop("MuzzleBrake",   1, 1, 1f * scale),
            new LootDrop("VerticalGrip",  1, 1, 1f * scale),
            new LootDrop("AngledGrip",    1, 1, 1f * scale),
            new LootDrop("HeavyStock",    1, 1, 1f * scale),
            new LootDrop("SkeletonStock", 1, 1, 1f * scale),
            new LootDrop("RedDot",        1, 1, 1f * scale),
            new LootDrop("ExtendedMag",   1, 1, 1f * scale),
            new LootDrop("QuickMag",      1, 1, 1f * scale),
            new LootDrop("LaserFocusing", 1, 1, 0.4f * scale),
            new LootDrop("ScatterChoke",  1, 1, 0.4f * scale),
            new LootDrop("AutoHeatSink",  1, 1, 0.4f * scale),
        };

        static LootDrop[] Concat(LootDrop[] a, LootDrop[] b)
        {
            var r = new LootDrop[a.Length + b.Length];
            a.CopyTo(r, 0);
            b.CopyTo(r, a.Length);
            return r;
        }

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
            // Meds/ammo/grenades + weapon modules + attachment mods (half-weight so mods stay a
            // modest slice of the general box; the ModuleCache is the mod-dense source).
            possibleDrops: Concat(new[]
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
            }, AttachmentModDrops(0.5f))
        );

        // Dedicated build-parts cache: cores + attachment mods (full mod weight). Pool is
        // build-only, so opening one always nets a build-relevant part — the mod-dense source
        // vs the general RandomLootBox.
        public static readonly ContainerTypeConfig ModuleCache = new(
            typeId: "ModuleCache",
            displayName: "Module Cache",
            minDrops: 1, maxDrops: 3,
            possibleDrops: Concat(WeaponModuleDrops, AttachmentModDrops(1f))
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
