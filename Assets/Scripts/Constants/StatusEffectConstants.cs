namespace Constants
{
    public static class StatusEffectConstants
    {
        // Bleeding L1 (light)
        public const float BleedL1DamagePerTick = 3f;
        public const float BleedDamagePerTick = BleedL1DamagePerTick; // backward compat alias

        // Bleeding L2 (heavy)
        public const float BleedL2DamagePerTick = 6f;

        public const float BleedTickInterval = 1f;
        public const float BandageUseTime = 3f;
    }
}
