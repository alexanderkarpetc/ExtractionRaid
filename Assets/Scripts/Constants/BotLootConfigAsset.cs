using System;
using System.Collections.Generic;
using State;
using UnityEngine;

namespace Constants
{
    // ── Runtime loot rules (plain value types baked onto BotTypeConfig) ──────────
    // No SO refs leak into runtime state (CLAUDE.md §3.6); BotSpawnSystem/LootSystem
    // roll these on death.

    /// <summary>
    /// Ammo drop rule. The caliber is never authored here — it's resolved at drop time from
    /// the equipped gun's payload AmmoType, so a rifle bot can only ever drop rifle ammo and
    /// a laser bot energy cells. Only the round count is a design choice.
    /// AP/HP variant weights lived here until the ammo audit (2026-07-27) retired those
    /// calibers; they come back with the ammo-selection feature, not before.
    /// </summary>
    public readonly struct AmmoLootRule
    {
        public readonly int MinRounds;
        public readonly int MaxRounds;

        public AmmoLootRule(int minRounds, int maxRounds)
        {
            MinRounds = Mathf.Max(0, minRounds);
            MaxRounds = Mathf.Max(MinRounds, maxRounds);
        }
    }

    /// <summary>A specific item to drop, in a random count range (e.g. 1-2 Medkits).</summary>
    public readonly struct ItemCountRule
    {
        public readonly string Id;
        public readonly int    Min;
        public readonly int    Max;

        public ItemCountRule(string id, int min, int max)
        {
            Id  = id;
            Min = Mathf.Max(0, min);
            Max = Mathf.Max(Min, max);
        }
    }

    /// <summary>Pick a random number of items (value-weighted) from a broad category.</summary>
    public readonly struct CategoryLootRule
    {
        public readonly LootCategory Category;
        public readonly int          MinPicks;
        public readonly int          MaxPicks;

        public CategoryLootRule(LootCategory category, int minPicks, int maxPicks)
        {
            Category = category;
            MinPicks = Mathf.Max(0, minPicks);
            MaxPicks = Mathf.Max(MinPicks, maxPicks);
        }
    }

    /// <summary>
    /// Reusable bot loot table — assign to a <see cref="BotTypeConfigAsset"/> to control
    /// what a bot drops beyond its equipped weapon and armor (which always drop). Three
    /// independent sections:
    ///
    ///   • Ammo    — variant mix (Standard / AP / HP) relative to the gun's caliber + count.
    ///   • Items   — specific items in count ranges (your "1-2 medkits", bandages, keys…).
    ///   • Category — pick N items from a broad category, weighted so pricier = rarer.
    ///
    /// Baked to plain value arrays for runtime (see BotTypeConfig). When a BotTypeConfig
    /// has no loot asset, the legacy default drop applies (caliber ammo + carried
    /// meds/bandages), so existing bots are unchanged.
    /// </summary>
    [CreateAssetMenu(fileName = "BotLootConfig", menuName = "Bots/Bot Loot Config")]
    public class BotLootConfigAsset : ScriptableObject
    {
        [Serializable]
        public class ItemEntry
        {
            [Tooltip("Item to drop.")]
            [ItemIdPicker]
            public string definitionId;
            [Min(0)] public int minCount = 1;
            [Min(0)] public int maxCount = 1;
        }

        [Serializable]
        public class CategoryEntry
        {
            public LootCategory category = LootCategory.Materials;
            [Min(0)]
            [Tooltip("Minimum number of distinct items picked from this category.")]
            public int minPicks = 1;
            [Min(0)]
            [Tooltip("Maximum number of distinct items picked from this category.")]
            public int maxPicks = 2;
        }

        [Header("Grenades (carried — thrown in combat, leftovers drop on death)")]
        [Min(0)] public int grenadeMinCount = 0;
        [Min(0)] public int grenadeMaxCount = 0;

        [Header("Ammo (rounds of the gun's own caliber)")]
        [Tooltip("Rounds dropped on death. Set both to 0 to drop no ammo.")]
        [Min(0)] public int ammoMinRounds = 30;
        [Min(0)] public int ammoMaxRounds = 30;

        [Header("Guaranteed items (specific items, random count each)")]
        [SerializeField] ItemEntry[] _items;

        [Header("Category loot (value-weighted random picks per category)")]
        [SerializeField] CategoryEntry[] _categories;

        public AmmoLootRule BuildAmmoRule() => new(ammoMinRounds, ammoMaxRounds);

        public ItemCountRule[] BuildItemRules()
        {
            if (_items == null || _items.Length == 0) return null;
            var list = new List<ItemCountRule>(_items.Length);
            foreach (var e in _items)
            {
                if (e == null || string.IsNullOrEmpty(e.definitionId)) continue;
                list.Add(new ItemCountRule(e.definitionId, e.minCount, e.maxCount));
            }
            return list.Count > 0 ? list.ToArray() : null;
        }

        public CategoryLootRule[] BuildCategoryRules()
        {
            if (_categories == null || _categories.Length == 0) return null;
            var list = new List<CategoryLootRule>(_categories.Length);
            foreach (var e in _categories)
            {
                if (e == null || e.maxPicks <= 0) continue;
                list.Add(new CategoryLootRule(e.category, e.minPicks, e.maxPicks));
            }
            return list.Count > 0 ? list.ToArray() : null;
        }
    }
}
