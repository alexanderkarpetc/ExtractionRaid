using UnityEngine;

namespace State
{
    /// <summary>
    /// Abstract base for all Payload Core definitions (ScriptableObject).
    /// Holds the common, shared data for every payload archetype — identity fields
    /// and the per-rarity <see cref="CommonPayloadStats"/> table.
    ///
    /// Payload-specific stats (e.g. ChargeTime for Laser, ExplosionRadius for Rocket)
    /// live on concrete subclasses alongside their own per-rarity tables.
    ///
    /// See docs/ai/weapon-builder/architecture.md §D2, §D3.
    /// </summary>
    public abstract class PayloadCoreDefinition : ScriptableObject
    {
        const int RarityTierCount = 5;

        [SerializeField] string _id;
        [SerializeField] string _archetype;
        [SerializeField] string _displayName;
        [SerializeField] string _ammoType;
        [SerializeField] CommonPayloadStats[] _statsByTier = new CommonPayloadStats[RarityTierCount];

        // ── Visualization (Tier 8.x* — payload as weapon root) ─────────────
        [Header("Visualization")]
        [Tooltip("Weapon BASE prefab (handle / receiver / magazine / battery — the half held " +
                 "у character's right hand). Instantiated як weapon root у CharacterBody.WeaponPivot. " +
                 "Must contain WeaponView component, DeliverySocket transform, RightHandGrip transform. " +
                 "Delivery (barrel) attaches inside DeliverySocket runtime. " +
                 "See docs/ai/weapon-builder/plan/roadmap.md Tier 8.x.")]
        [SerializeField] GameObject _basePrefab;

        public string Id          => _id;
        public string Archetype   => _archetype;
        /// <summary>Human-readable name used in weapon archetype labels (e.g. "Ballistic", "Laser").</summary>
        public string DisplayName => _displayName;
        public string AmmoType    => _ammoType;
        /// <summary>
        /// Weapon base prefab — instantiated як root коли weapon equipped. Owns the body/handle
        /// mesh, RightHandGrip IK target, DeliverySocket where barrel mounts at runtime.
        /// Null = invalid configuration; weapon assembly fails з WeaponAssemblyFailed event.
        /// </summary>
        public GameObject BasePrefab => _basePrefab;

        /// <summary>
        /// Common payload stats for the given rarity tier. Unauthored higher tiers
        /// (per-tier values are Tier 4b — not yet filled) fall back to Common, so a
        /// non-Common rarity never yields a zero-stat weapon. Rarity is visual-only
        /// until per-tier values exist.
        /// </summary>
        public CommonPayloadStats StatsByTier(RarityTier tier)
        {
            var s = _statsByTier[(int)tier];
            if (tier != RarityTier.Common && s.Equals(default(CommonPayloadStats)))
                return _statsByTier[(int)RarityTier.Common];
            return s;
        }

        protected virtual void OnValidate()
        {
            EnsureArrayLength(ref _statsByTier, RarityTierCount);
        }

        protected static void EnsureArrayLength<T>(ref T[] array, int length)
        {
            if (array == null || array.Length != length)
                System.Array.Resize(ref array, length);
        }
    }
}
