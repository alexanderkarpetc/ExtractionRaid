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

        // ── Visualization (Tier 8 Wave B) ─────────────────────────────────
        [Header("Visualization")]
        [Tooltip("Optional payload mesh attached as a child of the weapon's PayloadMount socket. " +
                 "Null = no attachment (e.g. Tier 1 archetypes before Wave C). " +
                 "See docs/ai/weapon-builder/plan/roadmap.md Tier 8 Wave B.")]
        [SerializeField] GameObject _attachmentPrefab;

        public string Id          => _id;
        public string Archetype   => _archetype;
        /// <summary>Human-readable name used in weapon archetype labels (e.g. "Ballistic", "Laser").</summary>
        public string DisplayName => _displayName;
        public string AmmoType    => _ammoType;
        /// <summary>
        /// Visual prefab spawned as a child of the weapon's PayloadMount socket on equip.
        /// Null = no attachment for this payload (graceful skip in <c>CharacterBody.SwapWeaponModel</c>).
        /// </summary>
        public GameObject AttachmentPrefab => _attachmentPrefab;

        /// <summary>Common payload stats for the given rarity tier.</summary>
        public CommonPayloadStats StatsByTier(RarityTier tier) => _statsByTier[(int)tier];

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
