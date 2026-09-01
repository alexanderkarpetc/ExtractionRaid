using UnityEngine;

namespace State
{
    /// <summary>
    /// Laser Charge — high-tech energy payload with charge-up before firing
    /// (direct reference: Half-Life 1 laser). See docs/ai/weapon-builder/README.md.
    /// </summary>
    [CreateAssetMenu(
        fileName = "NewLaserPayload",
        menuName = "Weapon Builder/Payload/Laser")]
    public class LaserPayloadDefinition : PayloadCoreDefinition
    {
        const int RarityTierCount = 5;

        [SerializeField] LaserSpecificStats[] _specificByTier = new LaserSpecificStats[RarityTierCount];

        /// <summary>
        /// Laser-specific stats (ChargeTime) for the given rarity tier. Unauthored higher
        /// tiers fall back to Common (rarity is visual-only until per-tier values exist),
        /// so a non-Common laser keeps a valid charge time instead of 0.
        /// </summary>
        public LaserSpecificStats SpecificByTier(RarityTier tier)
        {
            var s = _specificByTier[(int)tier];
            if (tier != RarityTier.Common && s.Equals(default(LaserSpecificStats)))
                return _specificByTier[(int)RarityTier.Common];
            return s;
        }

        protected override void OnValidate()
        {
            base.OnValidate();
            EnsureArrayLength(ref _specificByTier, RarityTierCount);
        }
    }
}
