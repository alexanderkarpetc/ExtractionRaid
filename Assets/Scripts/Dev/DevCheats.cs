using UnityEngine;

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

        // ── Status Effects ────────────────────────────────
        public static bool ForceBleedPlayer;

        // ── FOV ────────────────────────────────────────────
        public static bool FOVEnabled = true;
        public static float FOVNearRadius = 5f;
        public static float FOVFarRadius = 30f;
        public static float FOVAngle = 120f;
        public static bool ForceShowAllBots;
        public static bool FOVOcclusionEnabled = true;

        // ── Fog of War ────────────────────────────────────
        public static bool FogOfWarEnabled = true;
        public static float FogBlurRadius = 1.74f;
        public static int FogBlurIterations = 3;
        public static float FogIntensity = 0.4f;
        public static float FogDesaturation = 0.7f;
        public static Color FogColor = new(0.02f, 0.02f, 0.05f, 1f);
        public static int FoWRTScale = 256;
        public static float FOVRayStep = 2f;
        public static float FogTemporalBlend = 0.2f;

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
            FOVEnabled = true;
            FOVNearRadius = 5f;
            FOVFarRadius = 30f;
            FOVAngle = 120f;
            ForceShowAllBots = false;
            ForceBleedPlayer = false;
            FOVOcclusionEnabled = true;
            FogOfWarEnabled = true;
            FogBlurRadius = 1.74f;
            FogBlurIterations = 3;
            FogIntensity = 0.4f;
            FogDesaturation = 0.7f;
            FogColor = new Color(0.02f, 0.02f, 0.05f, 1f);
            FoWRTScale = 256;
            FOVRayStep = 2f;
            FogTemporalBlend = 0.2f;
        }
    }
}
