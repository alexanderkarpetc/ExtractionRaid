using System;
using State;
using UnityEngine;

namespace Constants
{
    [CreateAssetMenu(fileName = "ContainerTypeConfig", menuName = "Loot/Container Type Config")]
    public class ContainerTypeConfigAsset : ScriptableObject
    {
        [Serializable]
        public class WeightedDropEntry
        {
            [Tooltip("ItemDefinition.Id (e.g. \"Medkit\", \"Ammo_Rifle\", \"BallisticRound\"). " +
                     "Ignored when a Weapon Preset is assigned below.")]
            public string definitionId;

            [Min(1)] public int minCount = 1;
            [Min(1)] public int maxCount = 1;

            [Tooltip("Relative weight for the weighted roll. Higher = more likely. Must be > 0.")]
            [Min(0.0001f)] public float weight = 1f;

            [Tooltip("Optional. When set, this drop spawns the assembled weapon from the preset " +
                     "instead of a plain item. The Item id / count above are ignored.")]
            public WeaponPresetDefinition weaponPreset;
        }

        [Header("Identity")]
        [SerializeField] string _typeId = "CustomContainer";
        [SerializeField] string _displayName = "Container";

        [Header("Capacity")]
        [Tooltip("Total slots in the container (visual / capacity upper bound). Loot rolls are clamped to this.")]
        [SerializeField, Min(1)] int _slotCount = 20;

        [Header("Drop Count")]
        [Tooltip("Inclusive lower bound for number of items rolled.")]
        [SerializeField, Min(0)] int _minDrops = 2;
        [Tooltip("Inclusive upper bound for number of items rolled. Capped by slotCount.")]
        [SerializeField, Min(0)] int _maxDrops = 4;

        [Header("Drop Pool")]
        [Tooltip("Weighted pool. Each rolled slot picks one entry by weight, then a count between min/max.")]
        [SerializeField] WeightedDropEntry[] _drops = Array.Empty<WeightedDropEntry>();

        public string TypeId => _typeId;
        public string DisplayName => _displayName;
        public int SlotCount => _slotCount;

        public ContainerTypeConfig ToContainerTypeConfig()
        {
            var drops = new LootDrop[_drops != null ? _drops.Length : 0];
            for (int i = 0; i < drops.Length; i++)
            {
                var e = _drops[i];
                int min = Mathf.Max(1, e.minCount);
                int max = Mathf.Max(min, e.maxCount);
                drops[i] = new LootDrop(e.definitionId, min, max, e.weight, e.weaponPreset);
            }

            int maxDrops = Mathf.Max(_minDrops, _maxDrops);
            return new ContainerTypeConfig(
                typeId: _typeId,
                displayName: _displayName,
                slotCount: Mathf.Max(1, _slotCount),
                minDrops: _minDrops,
                maxDrops: maxDrops,
                possibleDrops: drops);
        }

        public void ApplyToRegistry()
        {
            ContainerConstants.RegisterOrOverride(ToContainerTypeConfig());
        }
    }
}
