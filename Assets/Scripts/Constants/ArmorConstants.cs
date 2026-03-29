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
    }
}
