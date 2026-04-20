using UnityEngine;

namespace State
{
    /// <summary>
    /// Laser Charge — high-tech energy payload with charge-up before firing
    /// (direct reference: Half-Life 1 laser). See design.md §7.
    /// </summary>
    [CreateAssetMenu(
        fileName = "NewLaserPayload",
        menuName = "Weapon Builder/Payload/Laser")]
    public class LaserPayloadDefinition : PayloadCoreDefinition
    {
        const int RarityTierCount = 5;

        [SerializeField] LaserSpecificStats[] _specificByTier = new LaserSpecificStats[RarityTierCount];

        /// <summary>Laser-specific stats (ChargeTime) for the given rarity tier.</summary>
        public LaserSpecificStats SpecificByTier(RarityTier tier) => _specificByTier[(int)tier];

        protected override void OnValidate()
        {
            base.OnValidate();
            EnsureArrayLength(ref _specificByTier, RarityTierCount);
        }
    }
}
