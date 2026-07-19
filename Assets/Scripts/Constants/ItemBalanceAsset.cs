using System;
using System.Collections.Generic;
using State;
using UnityEngine;

namespace Constants
{
    /// <summary>
    /// The single, designer-authored balance table for every item in the game: its base
    /// credit <see cref="Entry.Price"/> (what shops charge) and its <see cref="Entry.DropWeight"/>
    /// (relative chance in value-weighted loot rolls). One asset for the whole economy —
    /// lives at <c>Resources/Configs/ItemBalance.asset</c>.
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
                     "Only the ratio between items matters, not the absolute number.")]
            public float DropWeight;
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

        /// <summary>Loot drop weight for one item. Falls back to the value-derived default so an
        /// un-synced item still rolls at a sensible (scale-consistent) rate.</summary>
        public float GetDropWeight(string definitionId)
        {
            if (TryGet(definitionId, out var e) && e.DropWeight > 0f) return e.DropWeight;
            return DefaultDropWeight(ItemDefinition.Get(definitionId)?.Value ?? 10);
        }

        // Seed / fallback weight on a readable scale (~8..125) that stays proportional to the
        // classic 1/Value rarity curve, so pricier items remain rarer. Used by both the editor
        // sync (initial values) and the runtime fallback, keeping every weight on one scale.
        public static float DefaultDropWeight(int value) =>
            Mathf.Max(1f, Mathf.Round(1000f / Mathf.Max(1, value)));

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
    }
}
