using System;
using System.Collections.Generic;
using State;
using UnityEngine;

namespace Constants
{
    /// <summary>
    /// Reusable bot equipment pools — weighted random loadout options shared across
    /// bot types. Assign one to a <see cref="BotTypeConfigAsset"/> to have each spawned
    /// bot roll its weapon / helmet / body armor from these pools instead of using the
    /// fixed values on the type config.
    ///
    /// Each entry carries a weight; picks are weighted-random per spawn (weight 0 =
    /// never picked). An armor entry with an empty id means "no item" (bare head / no
    /// vest), so a pool can include the chance of an unarmored bot.
    ///
    /// Pools are baked to plain value arrays (<see cref="WeightedWeapon"/> /
    /// <see cref="WeightedId"/>) on <see cref="BotTypeConfig"/> — no SO refs leak into
    /// runtime state (CLAUDE.md §3.6). The per-spawn roll happens in BotSpawnSystem.
    /// </summary>
    [CreateAssetMenu(fileName = "BotEquipmentConfig", menuName = "Bots/Bot Equipment Config")]
    public class BotEquipmentConfigAsset : ScriptableObject
    {
        [Serializable]
        public class WeaponEntry
        {
            [Min(0f)]
            [Tooltip("Relative pick weight. 0 = never picked.")]
            public float weight = 1f;

            [Tooltip("Payload core (Ballistic, Laser, ...). Null falls back to Ballistic Round.")]
            public PayloadCoreDefinition payload;
            [Tooltip("Delivery core (Single-Action, Auto, Scatter, ...). Null falls back to Auto.")]
            public DeliveryCoreDefinition delivery;
            [Tooltip("Optional Exotic mod. Leave null for none.")]
            public ExoticModDefinition exotic;
            public RarityTier rarity = RarityTier.Common;
            [Min(0)] public int magazineAmmo = 30;
        }

        [Serializable]
        public class HelmetEntry
        {
            [Min(0f)]
            [Tooltip("Relative pick weight. 0 = never picked.")]
            public float weight = 1f;

            [Tooltip("Pick a helmet, or (None) for a bare head.")]
            [ItemIdPicker(ItemSlotType.Helmet)]
            public string definitionId;
        }

        [Serializable]
        public class BodyArmorEntry
        {
            [Min(0f)]
            [Tooltip("Relative pick weight. 0 = never picked.")]
            public float weight = 1f;

            [Tooltip("Pick a body armor, or (None) for no vest.")]
            [ItemIdPicker(ItemSlotType.BodyArmor)]
            public string definitionId;
        }

        [Header("Weapons (weighted random pick)")]
        [SerializeField] WeaponEntry[] _weapons;

        [Header("Helmets (weighted random pick; None = bare head)")]
        [SerializeField] HelmetEntry[] _helmets;

        [Header("Body Armor (weighted random pick; None = no vest)")]
        [SerializeField] BodyArmorEntry[] _bodyArmors;

        [Header("Armor wear (durability at spawn/drop, fraction of max — models prior combat)")]
        [Tooltip("Helmet durability range as a fraction of its max (x = min, y = max). (1,1) = pristine.")]
        [SerializeField] Vector2 _helmetDurability = Vector2.one;
        [Tooltip("Body armor durability range as a fraction of its max (x = min, y = max). (1,1) = pristine.")]
        [SerializeField] Vector2 _bodyArmorDurability = Vector2.one;

        public Vector2 HelmetDurabilityRange => _helmetDurability;
        public Vector2 BodyArmorDurabilityRange => _bodyArmorDurability;

        /// <summary>Weighted weapon pool, or null when no weapon entries are authored.</summary>
        public WeightedWeapon[] BuildWeaponPool()
        {
            if (_weapons == null || _weapons.Length == 0) return null;
            var pool = new List<WeightedWeapon>(_weapons.Length);
            foreach (var e in _weapons)
            {
                if (e == null) continue;
                var config = BotTypeConfigAsset.ComposeWeapon(
                    e.payload, e.delivery, e.exotic, e.rarity, e.magazineAmmo);
                pool.Add(new WeightedWeapon(config, e.weight));
            }
            return pool.Count > 0 ? pool.ToArray() : null;
        }

        /// <summary>Weighted helmet pool, or null when no helmet entries are authored.</summary>
        public WeightedId[] BuildHelmetPool() =>
            BuildIdPool(_helmets, e => e.weight, e => e.definitionId);

        /// <summary>Weighted body-armor pool, or null when no armor entries are authored.</summary>
        public WeightedId[] BuildBodyArmorPool() =>
            BuildIdPool(_bodyArmors, e => e.weight, e => e.definitionId);

        static WeightedId[] BuildIdPool<T>(T[] entries, Func<T, float> weightOf, Func<T, string> idOf)
            where T : class
        {
            if (entries == null || entries.Length == 0) return null;
            var pool = new List<WeightedId>(entries.Length);
            foreach (var e in entries)
            {
                if (e == null) continue;
                var raw = idOf(e);
                var id = string.IsNullOrEmpty(raw) ? null : raw;
                pool.Add(new WeightedId(id, weightOf(e)));
            }
            return pool.Count > 0 ? pool.ToArray() : null;
        }
    }
}
