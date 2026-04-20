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
        [SerializeField] string _ammoType;
        [SerializeField] CommonPayloadStats[] _statsByTier = new CommonPayloadStats[RarityTierCount];

        public string Id        => _id;
        public string Archetype => _archetype;
        public string AmmoType  => _ammoType;

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
