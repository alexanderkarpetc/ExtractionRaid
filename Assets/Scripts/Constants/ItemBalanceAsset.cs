using System;
using System.Collections.Generic;
using State;
using UnityEngine;

namespace Constants
{
    /// <summary>
    /// The single, designer-authored balance table for every item in the game: its base
    /// credit <see cref="Entry.Price"/> (what shops charge), its <see cref="Entry.DropWeight"/>
    /// (relative chance in loot rolls) and its <see cref="Entry.MinDropCount"/> /
    /// <see cref="Entry.MaxDropCount"/> (how many units one drop is worth). One asset for the
    /// whole economy — lives at <c>Resources/Configs/ItemBalance.asset</c>.
    ///
    /// Loot configs (containers, bot loot tables, loose loot) only say WHAT KIND of thing can
    /// appear and HOW MANY entries to roll; this table decides WHICH item comes out of a
    /// bucket and HOW BIG the stack is. Hardcoded/guaranteed drops on a container are the one
    /// deliberate exception — those name an item outright.
    ///
    /// Rows are kept in sync with <see cref="ItemDefinition"/> via the "Sync from
    /// ItemDefinition" button on the asset's inspector, so a newly-added item can't silently
    /// miss a price or drop weight. Per-vendor quantity + sell rules still live on
    /// <see cref="ShopDefinitionAsset"/>; only the price moved here.
    /// </summary>
    [CreateAssetMenu(fileName = "ItemBalance", menuName = "Items/Item Balance")]
    public class ItemBalanceAsset : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
            public string DefinitionId;

            [Tooltip("Base credits the item is worth. Shops charge this as the buy price; " +
                     "sell price = Price * shop SellRatio.")]
            public int Price;

            [Tooltip("Relative loot drop weight — higher = more common in category loot rolls. " +
                     "Only the ratio between items matters, not the absolute number. " +
                     "0 = never drops from a random roll (still buyable / craftable / " +
                     "reachable via a container's guaranteed drops).")]
            public float DropWeight;

            [Tooltip("Smallest stack this item drops as when a loot roll picks it. " +
                     "0 = derive from the item's MaxStackSize.")]
            public int MinDropCount;

            [Tooltip("Largest stack this item drops as when a loot roll picks it (split across " +
                     "slots when it exceeds MaxStackSize). 0 = derive from MaxStackSize.")]
            public int MaxDropCount;
        }

        [SerializeField] Entry[] _entries = Array.Empty<Entry>();

        public Entry[] Entries => _entries;

        Dictionary<string, Entry> _map;

        void BuildMap()
        {
            _map = new Dictionary<string, Entry>(_entries.Length, StringComparer.Ordinal);
            foreach (var e in _entries)
                if (!string.IsNullOrEmpty(e.DefinitionId))
                    _map[e.DefinitionId] = e;
        }

        void OnValidate() => _map = null;

        public bool TryGet(string definitionId, out Entry entry)
        {
            if (_map == null) BuildMap();
            return _map.TryGetValue(definitionId, out entry);
        }

        /// <summary>Buy price for one unit. Falls back to the item's intrinsic Value when the
        /// balance table has no row for it (e.g. before a sync).</summary>
        public int GetPrice(string definitionId)
        {
            if (TryGet(definitionId, out var e)) return e.Price;
            return ItemDefinition.Get(definitionId)?.Value ?? 0;
        }

        /// <summary>Loot drop weight for one item. A row's weight is authoritative — including
        /// an explicit 0, which takes the item out of every random roll. Only items with NO row
        /// fall back to the value-derived default, so an un-synced item still rolls at a
        /// sensible (scale-consistent) rate instead of silently vanishing.</summary>
        public float GetDropWeight(string definitionId)
        {
            if (TryGet(definitionId, out var e)) return Mathf.Max(0f, e.DropWeight);
            return DefaultDropWeight(ItemDefinition.Get(definitionId)?.Value ?? 10);
        }

        /// <summary>How many units one drop of this item is worth. Falls back to a stack-size
        /// derived range for rows that haven't been authored yet.</summary>
        public void GetDropCountRange(string definitionId, out int min, out int max)
        {
            if (TryGet(definitionId, out var e) && e.MaxDropCount > 0)
            {
                min = Mathf.Max(1, e.MinDropCount);
                max = Mathf.Max(min, e.MaxDropCount);
                return;
            }
            DefaultDropCountRange(definitionId, out min, out max);
        }

        // Seed / fallback weight on a readable scale (~8..125) that stays proportional to the
        // classic 1/Value rarity curve, so pricier items remain rarer. Used by both the editor
        // sync (initial values) and the runtime fallback, keeping every weight on one scale.
        public static float DefaultDropWeight(int value) =>
            Mathf.Max(1f, Mathf.Round(1000f / Mathf.Max(1, value)));

        /// <summary>Seed / fallback drop-count range: singles for non-stackables, a modest
        /// slice of a full stack for stackables. Deliberately conservative — the balance table
        /// is where a generous item (ammo, bandages) gets its real range.</summary>
        public static void DefaultDropCountRange(string definitionId, out int min, out int max)
        {
            int stack = Mathf.Max(1, ItemDefinition.Get(definitionId)?.MaxStackSize ?? 1);
            min = 1;
            max = stack <= 1 ? 1 : Mathf.Clamp(Mathf.RoundToInt(stack * 0.25f), 1, stack);
        }

        // ── Runtime singleton ────────────────────────────────────────────────
        public const string ResourcePath = "Configs/ItemBalance";

        static ItemBalanceAsset _instance;

        /// <summary>Shared instance loaded from Resources. Null in contexts without the asset
        /// (e.g. some EditMode tests) — callers fall back accordingly.</summary>
        public static ItemBalanceAsset Instance =>
            _instance != null ? _instance : (_instance = Resources.Load<ItemBalanceAsset>(ResourcePath));

        /// <summary>Convenience buy price that tolerates a missing asset.</summary>
        public static int PriceOf(string definitionId)
        {
            var inst = Instance;
            if (inst != null) return inst.GetPrice(definitionId);
            return ItemDefinition.Get(definitionId)?.Value ?? 0;
        }

        /// <summary>Convenience drop weight that tolerates a missing asset.</summary>
        public static float DropWeightOf(string definitionId)
        {
            var inst = Instance;
            if (inst != null) return inst.GetDropWeight(definitionId);
            return DefaultDropWeight(ItemDefinition.Get(definitionId)?.Value ?? 10);
        }

        /// <summary>Convenience drop-count range that tolerates a missing asset.</summary>
        public static void DropCountRangeOf(string definitionId, out int min, out int max)
        {
            var inst = Instance;
            if (inst != null) { inst.GetDropCountRange(definitionId, out min, out max); return; }
            DefaultDropCountRange(definitionId, out min, out max);
        }
    }
}
