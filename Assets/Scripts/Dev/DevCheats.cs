namespace Dev
{
    public static class DevCheats
    {
        // ── Toggles ─────────────────────────────────────────
        public static bool GodMode;
        public static bool InfiniteAmmo;
        public static bool NoRecoil;

        // ── Multipliers (1.0 = default, no change) ─────────
        public static float DamageMultiplier = 1f;
        public static float ProjectileSpeedMultiplier = 1f;
        public static float FireRateMultiplier = 1f;
        public static float MoveSpeedMultiplier = 1f;
        public static float RecoilMultiplier = 1f;

        public static void Reset()
        {
            GodMode = false;
            InfiniteAmmo = false;
            NoRecoil = false;
            DamageMultiplier = 1f;
            ProjectileSpeedMultiplier = 1f;
            FireRateMultiplier = 1f;
            MoveSpeedMultiplier = 1f;
            RecoilMultiplier = 1f;
        }
    }
}
