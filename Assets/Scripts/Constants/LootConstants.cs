using System;
using System.Collections.Generic;
using State;

namespace Constants
{
    /// <summary>
    /// Broad, misuse-proof loot buckets a container / bot can roll items from. Two flavours:
    ///
    ///   • Item-category buckets (<see cref="Materials"/> … <see cref="Gear"/>) — everything
    ///     registered under the matching <see cref="ItemCategory"/>.
    ///   • Curated buckets (<see cref="WeaponCores"/>, <see cref="Attachments"/>) — hand-picked
    ///     id sets carved out of <see cref="ItemCategory.WeaponMod"/>, because "build cores"
    ///     and "attachments" are separate economies that share one item category.
    ///
    /// A bucket never contains a generic weapon shell or a quest item, so a loot config can't
    /// drop one by accident. WHICH item comes out of a bucket (and how big the stack is) is
    /// decided by <see cref="ItemBalanceAsset"/> — never by the bucket itself.
    ///
    /// Enum values are serialized in loot configs — append new ones, never reorder.
    /// </summary>
    public enum LootCategory
    {
        Materials,
        Meds,
        Ammo,
        Mods,
        Throwables,
        Gear,
        WeaponCores,
        Attachments,
    }

    public static class LootConstants
    {
        /// <summary>
        /// Weapon build cores — the payload/delivery modules the Builder assembles guns from
        /// (Tier 6 G1/G2). Mirrors the module entries in <see cref="ItemDefinition"/>.
        /// </summary>
        public static readonly string[] WeaponCoreIds =
        {
            "BallisticRound",
            "LaserCharge",
            "SingleAction",
            "Auto",
            "Scatter",
        };

        /// <summary>
        /// Attachment mods (loot-gated economy). Ids match the AttachmentDefinition SOs in
        /// Resources/WeaponBuilder/Attachments. Relative rarity is NOT encoded here — it lives
        /// in <see cref="ItemBalanceAsset"/>'s DropWeight column, so the pricey optics /
        /// archetype-locked uniques stay scarce because the balance table says so.
        /// </summary>
        public static readonly string[] AttachmentIds =
        {
            "PowerComp",
            "MuzzleBrake",
            "VerticalGrip",
            "AngledGrip",
            "HeavyStock",
            "SkeletonStock",
            "RedDot",
            "SniperScope",
            "ExtendedMag",
            "QuickMag",
            "LaserFocusing",
            "ScatterChoke",
            "AutoHeatSink",
        };

        public static ItemCategory ToItemCategory(LootCategory c) => c switch
        {
            LootCategory.Materials  => ItemCategory.Material,
            LootCategory.Meds       => ItemCategory.Meds,
            LootCategory.Ammo       => ItemCategory.Ammo,
            LootCategory.Mods       => ItemCategory.WeaponMod,
            LootCategory.Throwables => ItemCategory.Throwable,
            LootCategory.Gear       => ItemCategory.Armor,
            _                       => ItemCategory.None, // curated buckets — see CandidatesFor
        };

        // Derived from the compile-time ItemDefinition registry, so it can be cached like the
        // registry itself. Drop weights are deliberately NOT cached — ItemBalance edits must
        // take effect without a recompile.
        static Dictionary<LootCategory, ItemDefinition[]> _candidates;

        /// <summary>
        /// Every item that can come out of this bucket, in registry order. Empty when the
        /// bucket resolves to nothing (e.g. a curated id that was renamed away).
        /// </summary>
        public static IReadOnlyList<ItemDefinition> CandidatesFor(LootCategory category)
        {
            _candidates ??= BuildCandidates();
            return _candidates.TryGetValue(category, out var list) ? list : Array.Empty<ItemDefinition>();
        }

        static Dictionary<LootCategory, ItemDefinition[]> BuildCandidates()
        {
            var map = new Dictionary<LootCategory, ItemDefinition[]>();

            foreach (LootCategory c in Enum.GetValues(typeof(LootCategory)))
            {
                var curated = CuratedIds(c);
                if (curated != null)
                {
                    var resolved = new List<ItemDefinition>(curated.Length);
                    foreach (var id in curated)
                    {
                        var def = ItemDefinition.Get(id);
                        if (def != null) resolved.Add(def);
                    }
                    map[c] = resolved.ToArray();
                    continue;
                }

                var cat = ToItemCategory(c);
                if (cat == ItemCategory.None) { map[c] = Array.Empty<ItemDefinition>(); continue; }

                var byCategory = new List<ItemDefinition>();
                foreach (var def in ItemDefinition.Registry.Values)
                    if (def.Category == cat) byCategory.Add(def);
                map[c] = byCategory.ToArray();
            }

            return map;
        }

        static string[] CuratedIds(LootCategory c) => c switch
        {
            LootCategory.WeaponCores  => WeaponCoreIds,
            LootCategory.Attachments  => AttachmentIds,
            _                         => null,
        };
    }
}
