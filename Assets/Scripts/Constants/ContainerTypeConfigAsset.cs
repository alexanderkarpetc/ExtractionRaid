using System;
using System.Collections.Generic;
using State;
using UnityEngine;

namespace Constants
{
    /// <summary>
    /// A container preset. Authors the SHAPE of the loot only:
    ///
    ///   • Guaranteed Drops — hardcoded contents that always spawn (the starting chest's
    ///     pistol, a fixed pair of bandages).
    ///   • Drop Count + Category Pool — how many extra entries to roll and what kinds they
    ///     can be.
    ///
    /// Which item comes out of a category, and how many units one drop is worth, is
    /// <see cref="ItemBalanceAsset"/>'s job — so rebalancing loot never means editing every
    /// container. Registered into <see cref="ContainerConstants"/> at spawn time.
    /// </summary>
    [CreateAssetMenu(fileName = "ContainerTypeConfig", menuName = "Loot/Container Type Config")]
    public class ContainerTypeConfigAsset : ScriptableObject
    {
        [Serializable]
        public class GuaranteedDropEntry
        {
            [ItemIdPicker]
            [Tooltip("Item that always spawns. Ignored when a Weapon Preset is assigned below.")]
            public string definitionId;

            [Tooltip("Take the stack size from ItemBalance instead of the range below.")]
            public bool countFromBalance;

            [Min(1)] public int minCount = 1;
            [Min(1)] public int maxCount = 1;

            [Tooltip("Optional. When set, this drop spawns the assembled weapon from the preset " +
                     "instead of a plain item. The item id / count above are ignored.")]
            public WeaponPresetDefinition weaponPreset;
        }

        [Serializable]
        public class PoolEntry
        {
            public enum EntryKind { Category, Item }

            [Tooltip("Category = roll any item from that bucket, weighted by ItemBalance. " +
                     "Item = always this item (stack size still from ItemBalance).")]
            public EntryKind kind = EntryKind.Category;

            public LootCategory category = LootCategory.Materials;

            [ItemIdPicker]
            public string definitionId;

            [Tooltip("Relative weight of this entry in the container's mix. Higher = shows up " +
                     "more often. This is NOT an item's rarity — that lives in ItemBalance.")]
            [Min(0f)] public float weight = 1f;
        }

        [Header("Identity")]
        [SerializeField] string _typeId = "CustomContainer";
        [SerializeField] string _displayName = "Container";

        [Header("Capacity")]
        [Tooltip("Total slots in the container (visual / capacity upper bound). Loot is clamped to this.")]
        [SerializeField, Min(1)] int _slotCount = 20;

        [Header("Guaranteed Drops")]
        [Tooltip("Always spawned, in order, before any random roll.")]
        [SerializeField] GuaranteedDropEntry[] _guaranteedDrops = Array.Empty<GuaranteedDropEntry>();

        [Header("Drop Count")]
        [Tooltip("Inclusive lower bound for how many entries are rolled from the pool below.")]
        [SerializeField, Min(0)] int _minDrops = 2;
        [Tooltip("Inclusive upper bound for how many entries are rolled. Capped by slotCount.")]
        [SerializeField, Min(0)] int _maxDrops = 4;

        [Header("Category Pool")]
        [Tooltip("Weighted buckets each roll picks from.")]
        [SerializeField] PoolEntry[] _pool = Array.Empty<PoolEntry>();

        public string TypeId => _typeId;
        public string DisplayName => _displayName;
        public int SlotCount => _slotCount;

        public ContainerTypeConfig ToContainerTypeConfig()
        {
            var guaranteed = new List<LootDrop>(_guaranteedDrops?.Length ?? 0);
            if (_guaranteedDrops != null)
                foreach (var e in _guaranteedDrops)
                {
                    if (e == null) continue;
                    if (e.weaponPreset != null && e.weaponPreset.IsValid)
                    {
                        guaranteed.Add(LootDrop.Preset(e.weaponPreset));
                        continue;
                    }
                    if (string.IsNullOrEmpty(e.definitionId)) continue;
                    if (e.countFromBalance)
                    {
                        guaranteed.Add(LootDrop.FromBalance(e.definitionId));
                        continue;
                    }
                    int min = Mathf.Max(1, e.minCount);
                    guaranteed.Add(new LootDrop(e.definitionId, min, Mathf.Max(min, e.maxCount)));
                }

            var pool = new List<LootPoolEntry>(_pool?.Length ?? 0);
            if (_pool != null)
                foreach (var e in _pool)
                {
                    if (e == null || e.weight <= 0f) continue;
                    if (e.kind == PoolEntry.EntryKind.Category)
                        pool.Add(LootPoolEntry.FromCategory(e.category, e.weight));
                    else if (!string.IsNullOrEmpty(e.definitionId))
                        pool.Add(LootPoolEntry.FromItem(e.definitionId, e.weight));
                }

            return new ContainerTypeConfig(
                typeId: _typeId,
                displayName: _displayName,
                minDrops: _minDrops,
                maxDrops: Mathf.Max(_minDrops, _maxDrops),
                randomPool: pool.ToArray(),
                guaranteedDrops: guaranteed.Count > 0 ? guaranteed.ToArray() : null,
                slotCount: Mathf.Max(1, _slotCount));
        }

        public void ApplyToRegistry()
        {
            ContainerConstants.RegisterOrOverride(ToContainerTypeConfig());
        }
    }
}
