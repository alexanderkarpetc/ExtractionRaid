using UnityEngine;

namespace Dev
{
    public static class DevCheats
    {
        // ── Cheats ──────────────────────────────────────────
        public static bool GodMode;
        public static bool InfiniteAmmo;

        // ── Weapon ──────────────────────────────────────────
        public static float DamageMultiplier;
        public static float ProjectileSpeedMultiplier;
        public static float FireRateMultiplier;

        // ── Recoil ──────────────────────────────────────────
        public static bool NoRecoil;
        public static float RecoilMultiplier;
        public static float RecoilForwardMultiplier;
        public static float RecoilSideMultiplier;
        public static float RecoilRecoveryMultiplier;

        // ── Aim Split ───────────────────────────────────────
        public static bool AimSplitEnabled;
        public static float AimFollowMultiplier;

        // ── Player ──────────────────────────────────────────
        public static float MoveSpeedMultiplier;

        // ── FOV ─────────────────────────────────────────────
        public static bool FOVEnabled;
        public static float FOVNearRadius;
        public static float FOVFarRadius;
        public static float FOVAngle;
        public static bool ForceShowAllBots;
        public static bool FOVOcclusionEnabled;

        // ── Fog of War ──────────────────────────────────────
        public static bool FogOfWarEnabled;
        public static float FogBlurRadius;
        public static int FogBlurIterations;
        public static float FogIntensity;
        public static float FogDesaturation;
        public static Color FogColor;
        public static int FoWRTScale;
        public static float FOVRayStep;
        public static float FogTemporalBlend;

        // ── Status Effects ──────────────────────────────────
        public static bool ForceBleedPlayer;

        static DevCheats() => Reset();

        public static void Reset()
        {
            // Cheats
            GodMode = false;
            InfiniteAmmo = false;

            // Weapon
            DamageMultiplier = 1f;
            ProjectileSpeedMultiplier = 1f;
            FireRateMultiplier = 1f;

            // Recoil
            NoRecoil = false;
            RecoilMultiplier = .5f;
            RecoilForwardMultiplier = 1f;
            RecoilSideMultiplier = 1f;
            RecoilRecoveryMultiplier = 1f;

            // Aim Split
            AimSplitEnabled = false;
            AimFollowMultiplier = 1f;

            // Player
            MoveSpeedMultiplier = 1f;

            // FOV
            FOVEnabled = true;
            FOVNearRadius = 5f;
            FOVFarRadius = 30f;
            FOVAngle = 120f;
            ForceShowAllBots = false;
            FOVOcclusionEnabled = true;

            // Fog of War
            FogOfWarEnabled = true;
            FogBlurRadius = 1.74f;
            FogBlurIterations = 3;
            FogIntensity = 0.4f;
            FogDesaturation = 0.7f;
            FogColor = new Color(0.02f, 0.02f, 0.05f, 1f);
            FoWRTScale = 256;
            FOVRayStep = 2f;
            FogTemporalBlend = 0.2f;

            // Status Effects
            ForceBleedPlayer = false;
        }
    }
}
