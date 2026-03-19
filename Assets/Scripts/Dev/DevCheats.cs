using UnityEngine;

namespace Dev
{
    /// <summary>
    /// Thin static accessor over <see cref="DevCheatsConfig"/> ScriptableObject.
    /// All 16 call-sites keep using DevCheats.X — zero refactor needed.
    /// The SO asset lives at Resources/DevCheatsConfig.
    /// </summary>
    public static class DevCheats
    {
        static DevCheatsConfig _cfg;

        public static DevCheatsConfig Config
        {
            get
            {
                if (_cfg == null)
                    _cfg = Resources.Load<DevCheatsConfig>("DevCheatsConfig");
#if UNITY_EDITOR
                // Fallback: create in-memory instance so editor never NPEs
                if (_cfg == null)
                {
                    Debug.LogWarning("[DevCheats] DevCheatsConfig asset not found in Resources. Using in-memory defaults.");
                    _cfg = ScriptableObject.CreateInstance<DevCheatsConfig>();
                }
#endif
                return _cfg;
            }
        }

        // ── Cheats ──────────────────────────────────────────
        public static bool GodMode
        {
            get => Config.GodMode;
            set => Config.GodMode = value;
        }

        public static bool InfiniteAmmo
        {
            get => Config.InfiniteAmmo;
            set => Config.InfiniteAmmo = value;
        }

        // ── Weapon ──────────────────────────────────────────
        public static float DamageMultiplier
        {
            get => Config.DamageMultiplier;
            set => Config.DamageMultiplier = value;
        }

        public static float ProjectileSpeedMultiplier
        {
            get => Config.ProjectileSpeedMultiplier;
            set => Config.ProjectileSpeedMultiplier = value;
        }

        public static float FireRateMultiplier
        {
            get => Config.FireRateMultiplier;
            set => Config.FireRateMultiplier = value;
        }

        // ── Recoil ──────────────────────────────────────────
        public static bool NoRecoil
        {
            get => Config.NoRecoil;
            set => Config.NoRecoil = value;
        }

        public static float RecoilMultiplier
        {
            get => Config.RecoilMultiplier;
            set => Config.RecoilMultiplier = value;
        }

        public static float RecoilForwardMultiplier
        {
            get => Config.RecoilForwardMultiplier;
            set => Config.RecoilForwardMultiplier = value;
        }

        public static float RecoilSideMultiplier
        {
            get => Config.RecoilSideMultiplier;
            set => Config.RecoilSideMultiplier = value;
        }

        public static float RecoilRecoveryMultiplier
        {
            get => Config.RecoilRecoveryMultiplier;
            set => Config.RecoilRecoveryMultiplier = value;
        }

        // ── Aim Split ───────────────────────────────────────
        public static bool AimSplitEnabled
        {
            get => Config.AimSplitEnabled;
            set => Config.AimSplitEnabled = value;
        }

        public static float AimFollowMultiplier
        {
            get => Config.AimFollowMultiplier;
            set => Config.AimFollowMultiplier = value;
        }

        // ── Player ──────────────────────────────────────────
        public static float MoveSpeedMultiplier
        {
            get => Config.MoveSpeedMultiplier;
            set => Config.MoveSpeedMultiplier = value;
        }

        // ── FOV ─────────────────────────────────────────────
        public static bool FOVEnabled
        {
            get => Config.FOVEnabled;
            set => Config.FOVEnabled = value;
        }

        public static float FOVNearRadius
        {
            get => Config.FOVNearRadius;
            set => Config.FOVNearRadius = value;
        }

        public static float FOVFarRadius
        {
            get => Config.FOVFarRadius;
            set => Config.FOVFarRadius = value;
        }

        public static float FOVAngle
        {
            get => Config.FOVAngle;
            set => Config.FOVAngle = value;
        }

        public static bool ForceShowAllBots
        {
            get => Config.ForceShowAllBots;
            set => Config.ForceShowAllBots = value;
        }

        public static bool FOVOcclusionEnabled
        {
            get => Config.FOVOcclusionEnabled;
            set => Config.FOVOcclusionEnabled = value;
        }

        // ── Fog of War ──────────────────────────────────────
        public static bool FogOfWarEnabled
        {
            get => Config.FogOfWarEnabled;
            set => Config.FogOfWarEnabled = value;
        }

        public static float FogBlurRadius
        {
            get => Config.FogBlurRadius;
            set => Config.FogBlurRadius = value;
        }

        public static int FogBlurIterations
        {
            get => Config.FogBlurIterations;
            set => Config.FogBlurIterations = value;
        }

        public static float FogIntensity
        {
            get => Config.FogIntensity;
            set => Config.FogIntensity = value;
        }

        public static float FogDesaturation
        {
            get => Config.FogDesaturation;
            set => Config.FogDesaturation = value;
        }

        public static Color FogColor
        {
            get => Config.FogColor;
            set => Config.FogColor = value;
        }

        public static int FoWRTScale
        {
            get => Config.FoWRTScale;
            set => Config.FoWRTScale = value;
        }

        public static float FOVRayStep
        {
            get => Config.FOVRayStep;
            set => Config.FOVRayStep = value;
        }

        public static float FogTemporalBlend
        {
            get => Config.FogTemporalBlend;
            set => Config.FogTemporalBlend = value;
        }

        // ── Status Effects ──────────────────────────────────
        public static bool ForceBleedPlayer
        {
            get => Config.ForceBleedPlayer;
            set => Config.ForceBleedPlayer = value;
        }

        /// <summary>Mark asset dirty so editor saves it. Call after batch changes.</summary>
        public static void SetDirty()
        {
#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(Config);
#endif
        }
    }
}
