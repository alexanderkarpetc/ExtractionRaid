using UnityEngine;

namespace State
{
    /// <summary>
    /// Adhesive Foam — control/utility payload. Not a burst-damage weapon;
    /// instead applies slow / sticking / movement denial to targets. See docs/ai/weapon-builder/README.md.
    /// </summary>
    [CreateAssetMenu(
        fileName = "NewFoamPayload",
        menuName = "Weapon Builder/Payload/Foam")]
    public class FoamPayloadDefinition : PayloadCoreDefinition
    {
        const int RarityTierCount = 5;

        [SerializeField] FoamSpecificStats[] _specificByTier = new FoamSpecificStats[RarityTierCount];

        /// <summary>Foam-specific stats (SlowDuration, StickDuration) for the given rarity tier.</summary>
        public FoamSpecificStats SpecificByTier(RarityTier tier) => _specificByTier[(int)tier];

        protected override void OnValidate()
        {
            base.OnValidate();
            EnsureArrayLength(ref _specificByTier, RarityTierCount);
        }
    }
}
