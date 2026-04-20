using UnityEngine;

namespace State
{
    /// <summary>
    /// Micro-Rocket — small explosive payload with AoE impact.
    /// Typically has HeadshotDamageMultiplier = 0 (explosions don't headshot). See design.md §7.
    /// </summary>
    [CreateAssetMenu(
        fileName = "NewRocketPayload",
        menuName = "Weapon Builder/Payload/Rocket")]
    public class RocketPayloadDefinition : PayloadCoreDefinition
    {
        const int RarityTierCount = 5;

        [SerializeField] RocketSpecificStats[] _specificByTier = new RocketSpecificStats[RarityTierCount];

        /// <summary>Rocket-specific stats (ExplosionRadius) for the given rarity tier.</summary>
        public RocketSpecificStats SpecificByTier(RarityTier tier) => _specificByTier[(int)tier];

        protected override void OnValidate()
        {
            base.OnValidate();
            EnsureArrayLength(ref _specificByTier, RarityTierCount);
        }
    }
}
