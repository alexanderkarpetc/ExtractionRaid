namespace Constants
{
    public static class ArmorConstants
    {
        // Penetration curve: DamageMultiplier = K / (K + diff)
        public const float DamageReductionK = 30f;
        public const float PenetrationCap = 100f;
        public const float ArmorPointsCap = 100f;

        // Durability degradation parabola
        public const float DurabilityThreshold = 0.7f;
        public const float DurabilityParabolicPower = 2f;

        // Helmet ricochet
        public const float RicochetChance = 0.4f;

        // ArmorDmg cap (flat points per hit)
        public const float ArmorDamageCap = 30f;

        // Weight → movement speed penalty (battle-design-status.md §11).
        // Weight = (ArmorPoints + MaxDurability) summed across both equipped slots.
        // Speed multiplier = max(WeightSpeedFloor, 1 - totalWeight × WeightSpeedFactor).
        // Tuning at 2026-05-05:
        //   Basic kit (Helmet 30/100 + Armor 40/120 = 290 weight) → 14.5% slowdown
        //   Mid-tier kit (~400 weight) → 20% slowdown
        //   Elite kit (~550 weight) → 27.5% slowdown
        //   Floor reached at 1000 weight (theoretical, blocks god-gear edge cases)
        public const float WeightSpeedFactor = 0.0005f;
        public const float WeightSpeedFloor = 0.5f;
    }
}
