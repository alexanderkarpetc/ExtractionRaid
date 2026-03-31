using UnityEngine;

namespace Dev
{
    /// <summary>
    /// Thin static accessor over <see cref="DevCheatsConfig"/> ScriptableObject.
    /// All call-sites keep using DevCheats.X — zero refactor needed.
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
                    _cfg = Resources.Load<DevCheatsConfig>("Configs/DevCheatsConfig");
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
            get => Config.Cheats.GodMode;
            set => Config.Cheats.GodMode = value;
        }

        public static bool InfiniteAmmo
        {
            get => Config.Cheats.InfiniteAmmo;
            set => Config.Cheats.InfiniteAmmo = value;
        }

        // ── Weapon ──────────────────────────────────────────
        public static float DamageMultiplier
        {
            get => Config.Weapon.DamageMultiplier;
            set => Config.Weapon.DamageMultiplier = value;
        }

        public static float ProjectileSpeedMultiplier
        {
            get => Config.Weapon.ProjectileSpeedMultiplier;
            set => Config.Weapon.ProjectileSpeedMultiplier = value;
        }

        public static float FireRateMultiplier
        {
            get => Config.Weapon.FireRateMultiplier;
            set => Config.Weapon.FireRateMultiplier = value;
        }

        // ── Recoil ──────────────────────────────────────────
        public static bool NoRecoil
        {
            get => Config.Recoil.NoRecoil;
            set => Config.Recoil.NoRecoil = value;
        }

        public static float RecoilMultiplier
        {
            get => Config.Recoil.RecoilMultiplier;
            set => Config.Recoil.RecoilMultiplier = value;
        }

        public static float RecoilForwardMultiplier
        {
            get => Config.Recoil.RecoilForwardMultiplier;
            set => Config.Recoil.RecoilForwardMultiplier = value;
        }

        public static float RecoilSideMultiplier
        {
            get => Config.Recoil.RecoilSideMultiplier;
            set => Config.Recoil.RecoilSideMultiplier = value;
        }

        public static float RecoilRecoveryMultiplier
        {
            get => Config.Recoil.RecoilRecoveryMultiplier;
            set => Config.Recoil.RecoilRecoveryMultiplier = value;
        }

        // ── Aim Split ───────────────────────────────────────
        public static bool AimSplitEnabled
        {
            get => Config.Aim.AimSplitEnabled;
            set => Config.Aim.AimSplitEnabled = value;
        }

        public static float AimFollowMultiplier
        {
            get => Config.Aim.AimFollowMultiplier;
            set => Config.Aim.AimFollowMultiplier = value;
        }

        // ── Player ──────────────────────────────────────────
        public static float MoveSpeedMultiplier
        {
            get => Config.Player.MoveSpeedMultiplier;
            set => Config.Player.MoveSpeedMultiplier = value;
        }

        // ── FOV ─────────────────────────────────────────────
        public static bool FOVEnabled
        {
            get => Config.FOV.FOVEnabled;
            set => Config.FOV.FOVEnabled = value;
        }

        public static float FOVNearRadius
        {
            get => Config.FOV.FOVNearRadius;
            set => Config.FOV.FOVNearRadius = value;
        }

        public static float FOVFarRadius
        {
            get => Config.FOV.FOVFarRadius;
            set => Config.FOV.FOVFarRadius = value;
        }

        public static float FOVAngle
        {
            get => Config.FOV.FOVAngle;
            set => Config.FOV.FOVAngle = value;
        }

        public static bool ForceShowAllBots
        {
            get => Config.FOV.ForceShowAllBots;
            set => Config.FOV.ForceShowAllBots = value;
        }

        public static bool FOVOcclusionEnabled
        {
            get => Config.FOV.FOVOcclusionEnabled;
            set => Config.FOV.FOVOcclusionEnabled = value;
        }

        // ── Fog of War ──────────────────────────────────────
        public static bool FogOfWarEnabled
        {
            get => Config.Fog.FogOfWarEnabled;
            set => Config.Fog.FogOfWarEnabled = value;
        }

        public static float FogBlurRadius
        {
            get => Config.Fog.FogBlurRadius;
            set => Config.Fog.FogBlurRadius = value;
        }

        public static int FogBlurIterations
        {
            get => Config.Fog.FogBlurIterations;
            set => Config.Fog.FogBlurIterations = value;
        }

        public static float FogIntensity
        {
            get => Config.Fog.FogIntensity;
            set => Config.Fog.FogIntensity = value;
        }

        public static float FogDesaturation
        {
            get => Config.Fog.FogDesaturation;
            set => Config.Fog.FogDesaturation = value;
        }

        public static Color FogColor
        {
            get => Config.Fog.FogColor;
            set => Config.Fog.FogColor = value;
        }

        public static int FoWRTScale
        {
            get => Config.Fog.FoWRTScale;
            set => Config.Fog.FoWRTScale = value;
        }

        public static float FOVRayStep
        {
            get => Config.Fog.FOVRayStep;
            set => Config.Fog.FOVRayStep = value;
        }

        public static float FogTemporalBlend
        {
            get => Config.Fog.FogTemporalBlend;
            set => Config.Fog.FogTemporalBlend = value;
        }

        // ── Crosshair ─────────────────────────────────────
        public static bool CrosshairEnabled
        {
            get => Config.Crosshair.CrosshairEnabled;
            set => Config.Crosshair.CrosshairEnabled = value;
        }

        public static float CrosshairLineLength
        {
            get => Config.Crosshair.CrosshairLineLength;
            set => Config.Crosshair.CrosshairLineLength = value;
        }

        public static float CrosshairLineThickness
        {
            get => Config.Crosshair.CrosshairLineThickness;
            set => Config.Crosshair.CrosshairLineThickness = value;
        }

        public static float CrosshairBaseGap
        {
            get => Config.Crosshair.CrosshairBaseGap;
            set => Config.Crosshair.CrosshairBaseGap = value;
        }

        public static float CrosshairCenterDotSize
        {
            get => Config.Crosshair.CrosshairCenterDotSize;
            set => Config.Crosshair.CrosshairCenterDotSize = value;
        }

        public static float CrosshairBloomExtraGap
        {
            get => Config.Crosshair.CrosshairBloomExtraGap;
            set => Config.Crosshair.CrosshairBloomExtraGap = value;
        }

        public static Color CrosshairNormalColor
        {
            get => Config.Crosshair.CrosshairNormalColor;
            set => Config.Crosshair.CrosshairNormalColor = value;
        }

        public static Color CrosshairWarningColor
        {
            get => Config.Crosshair.CrosshairWarningColor;
            set => Config.Crosshair.CrosshairWarningColor = value;
        }

        public static Color CrosshairBloomColor
        {
            get => Config.Crosshair.CrosshairBloomColor;
            set => Config.Crosshair.CrosshairBloomColor = value;
        }

        // ── Hit Markers ──────────────────────────────────
        public static float HitMarkerScale
        {
            get => Config.Crosshair.HitMarkerScale;
            set => Config.Crosshair.HitMarkerScale = value;
        }
        public static float HitDuration
        {
            get => Config.Crosshair.HitDuration;
            set => Config.Crosshair.HitDuration = value;
        }
        public static float KillDuration
        {
            get => Config.Crosshair.KillDuration;
            set => Config.Crosshair.KillDuration = value;
        }
        public static float HitLineLength
        {
            get => Config.Crosshair.HitLineLength;
            set => Config.Crosshair.HitLineLength = value;
        }
        public static float KillLineLength
        {
            get => Config.Crosshair.KillLineLength;
            set => Config.Crosshair.KillLineLength = value;
        }
        public static float HitGapStart
        {
            get => Config.Crosshair.HitGapStart;
            set => Config.Crosshair.HitGapStart = value;
        }
        public static float HitGapExpand
        {
            get => Config.Crosshair.HitGapExpand;
            set => Config.Crosshair.HitGapExpand = value;
        }
        public static float HitMarkerThickness
        {
            get => Config.Crosshair.HitMarkerThickness;
            set => Config.Crosshair.HitMarkerThickness = value;
        }
        public static Color HitColor
        {
            get => Config.Crosshair.HitColor;
            set => Config.Crosshair.HitColor = value;
        }
        public static Color KillColor
        {
            get => Config.Crosshair.KillColor;
            set => Config.Crosshair.KillColor = value;
        }
        public static float HeadshotOuterScale
        {
            get => Config.Crosshair.HeadshotOuterScale;
            set => Config.Crosshair.HeadshotOuterScale = value;
        }
        public static float HeadshotOuterExpandMul
        {
            get => Config.Crosshair.HeadshotOuterExpandMul;
            set => Config.Crosshair.HeadshotOuterExpandMul = value;
        }
        public static float HeadshotDuration
        {
            get => Config.Crosshair.HeadshotDuration;
            set => Config.Crosshair.HeadshotDuration = value;
        }
        public static Color HeadshotColor
        {
            get => Config.Crosshair.HeadshotColor;
            set => Config.Crosshair.HeadshotColor = value;
        }

        // Armor feedback
        public static Color ArmorHitColor
        {
            get => Config.Crosshair.ArmorHitColor;
            set => Config.Crosshair.ArmorHitColor = value;
        }
        public static Color RicochetColor
        {
            get => Config.Crosshair.RicochetColor;
            set => Config.Crosshair.RicochetColor = value;
        }
        public static float RicochetDuration
        {
            get => Config.Crosshair.RicochetDuration;
            set => Config.Crosshair.RicochetDuration = value;
        }

        // ── ADS ───────────────────────────────────────────
        public static float AdsTransitionTime
        {
            get => Config.ADS.AdsTransitionTime;
            set => Config.ADS.AdsTransitionTime = value;
        }

        public static float AdsMoveSpeedMultiplier
        {
            get => Config.ADS.AdsMoveSpeedMultiplier;
            set => Config.ADS.AdsMoveSpeedMultiplier = value;
        }

        public static float AdsAimFollowMultiplier
        {
            get => Config.ADS.AdsAimFollowMultiplier;
            set => Config.ADS.AdsAimFollowMultiplier = value;
        }

        public static float AdsRecoilMultiplier
        {
            get => Config.ADS.AdsRecoilMultiplier;
            set => Config.ADS.AdsRecoilMultiplier = value;
        }

        public static float AdsRecoilRecoveryMultiplier
        {
            get => Config.ADS.AdsRecoilRecoveryMultiplier;
            set => Config.ADS.AdsRecoilRecoveryMultiplier = value;
        }

        public static float AdsZoomFactor
        {
            get => Config.ADS.AdsZoomFactor;
            set => Config.ADS.AdsZoomFactor = value;
        }

        public static float AdsCursorInfluenceMultiplier
        {
            get => Config.ADS.AdsCursorInfluenceMultiplier;
            set => Config.ADS.AdsCursorInfluenceMultiplier = value;
        }

        public static float AdsBaseGap
        {
            get => Config.ADS.AdsBaseGap;
            set => Config.ADS.AdsBaseGap = value;
        }

        public static float AdsBloomExtraGap
        {
            get => Config.ADS.AdsBloomExtraGap;
            set => Config.ADS.AdsBloomExtraGap = value;
        }

        public static float AdsVignetteIntensity
        {
            get => Config.ADS.AdsVignetteIntensity;
            set => Config.ADS.AdsVignetteIntensity = value;
        }

        // ── Health Bar ─────────────────────────────────────
        public static float HBarWidth { get => Config.HealthBar.HBarWidth; set => Config.HealthBar.HBarWidth = value; }
        public static float HBarHeight { get => Config.HealthBar.HBarHeight; set => Config.HealthBar.HBarHeight = value; }
        public static float HBarOffsetY { get => Config.HealthBar.HBarOffsetY; set => Config.HealthBar.HBarOffsetY = value; }
        public static float HBarBorderSize { get => Config.HealthBar.HBarBorderSize; set => Config.HealthBar.HBarBorderSize = value; }
        public static float HBarTrailDelay { get => Config.HealthBar.HBarTrailDelay; set => Config.HealthBar.HBarTrailDelay = value; }
        public static float HBarTrailSpeed { get => Config.HealthBar.HBarTrailSpeed; set => Config.HealthBar.HBarTrailSpeed = value; }
        public static float HBarFlashDuration { get => Config.HealthBar.HBarFlashDuration; set => Config.HealthBar.HBarFlashDuration = value; }
        public static float HBarFlashExpandX { get => Config.HealthBar.HBarFlashExpandX; set => Config.HealthBar.HBarFlashExpandX = value; }
        public static float HBarFlashExpandY { get => Config.HealthBar.HBarFlashExpandY; set => Config.HealthBar.HBarFlashExpandY = value; }
        public static float HBarFlashPower { get => Config.HealthBar.HBarFlashPower; set => Config.HealthBar.HBarFlashPower = value; }
        public static float HBarShakeIntensity { get => Config.HealthBar.HBarShakeIntensity; set => Config.HealthBar.HBarShakeIntensity = value; }
        public static float HBarShakeDuration { get => Config.HealthBar.HBarShakeDuration; set => Config.HealthBar.HBarShakeDuration = value; }
        public static float HBarShakeFrequency { get => Config.HealthBar.HBarShakeFrequency; set => Config.HealthBar.HBarShakeFrequency = value; }
        public static float HBarHpPerSegment { get => Config.HealthBar.HBarHpPerSegment; set => Config.HealthBar.HBarHpPerSegment = value; }
        public static float HBarSegmentLineWidth { get => Config.HealthBar.HBarSegmentLineWidth; set => Config.HealthBar.HBarSegmentLineWidth = value; }
        public static Color HBarSegmentLineColor { get => Config.HealthBar.HBarSegmentLineColor; set => Config.HealthBar.HBarSegmentLineColor = value; }
        public static Color HBarTrailColor { get => Config.HealthBar.HBarTrailColor; set => Config.HealthBar.HBarTrailColor = value; }
        public static Color HBarFlashColor { get => Config.HealthBar.HBarFlashColor; set => Config.HealthBar.HBarFlashColor = value; }
        public static Color HBarBgColor { get => Config.HealthBar.HBarBgColor; set => Config.HealthBar.HBarBgColor = value; }

        // ── Parallax / Projectile ──────────────────────────
        public static float ProjectileSpawnHeight
        {
            get => Config.Parallax.ProjectileSpawnHeight;
            set => Config.Parallax.ProjectileSpawnHeight = value;
        }

        public static bool ParallaxCorrection
        {
            get => Config.Parallax.ParallaxCorrection;
            set => Config.Parallax.ParallaxCorrection = value;
        }

        public static float ConvergenceBlend
        {
            get => Config.Parallax.ConvergenceBlend;
            set => Config.Parallax.ConvergenceBlend = value;
        }

        public static bool ConvergenceAimUp
        {
            get => Config.Parallax.ConvergenceAimUp;
            set => Config.Parallax.ConvergenceAimUp = value;
        }

        public static float AimUpHeightRatio
        {
            get => Config.Parallax.AimUpHeightRatio;
            set => Config.Parallax.AimUpHeightRatio = value;
        }

        public static float ProjectileHitRadius
        {
            get => Config.Parallax.ProjectileHitRadius;
            set => Config.Parallax.ProjectileHitRadius = value;
        }

        // ── Damage Numbers ─────────────────────────────────
        public static bool DmgNumEnabled { get => Config.DamageNumbers.Enabled; set => Config.DamageNumbers.Enabled = value; }
        public static int DmgNumTrajectoryMode { get => Config.DamageNumbers.TrajectoryMode; set => Config.DamageNumbers.TrajectoryMode = value; }
        public static float DmgNumDuration { get => Config.DamageNumbers.Duration; set => Config.DamageNumbers.Duration = value; }
        public static float DmgNumFlySpeed { get => Config.DamageNumbers.FlySpeed; set => Config.DamageNumbers.FlySpeed = value; }
        public static float DmgNumGravityAccel { get => Config.DamageNumbers.GravityAccel; set => Config.DamageNumbers.GravityAccel = value; }
        public static float DmgNumPopDuration { get => Config.DamageNumbers.PopDuration; set => Config.DamageNumbers.PopDuration = value; }
        public static float DmgNumPopOvershoot { get => Config.DamageNumbers.PopOvershoot; set => Config.DamageNumbers.PopOvershoot = value; }
        public static float DmgNumBaseFontSize { get => Config.DamageNumbers.BaseFontSize; set => Config.DamageNumbers.BaseFontSize = value; }
        public static float DmgNumDamageScaleFactor { get => Config.DamageNumbers.DamageScaleFactor; set => Config.DamageNumbers.DamageScaleFactor = value; }
        public static float DmgNumRandomSpread { get => Config.DamageNumbers.RandomSpread; set => Config.DamageNumbers.RandomSpread = value; }
        public static Color DmgNumNormalColor { get => Config.DamageNumbers.NormalColor; set => Config.DamageNumbers.NormalColor = value; }
        public static Color DmgNumHeadshotColor { get => Config.DamageNumbers.HeadshotColor; set => Config.DamageNumbers.HeadshotColor = value; }
        public static Color DmgNumKillColor { get => Config.DamageNumbers.KillColor; set => Config.DamageNumbers.KillColor = value; }
        public static Color DmgNumArmorAbsorbColor { get => Config.DamageNumbers.ArmorAbsorbColor; set => Config.DamageNumbers.ArmorAbsorbColor = value; }

        // ── Status Effects ──────────────────────────────────
        public static bool ForceBleedPlayer
        {
            get => Config.StatusEffects.ForceBleedPlayer;
            set => Config.StatusEffects.ForceBleedPlayer = value;
        }

        // ── Armor ────────────────────────────────────────────
        public static float ArmorK
        {
            get => Config.Armor.DamageReductionK;
            set => Config.Armor.DamageReductionK = value;
        }

        public static float ArmorDurabilityThreshold
        {
            get => Config.Armor.DurabilityThreshold;
            set => Config.Armor.DurabilityThreshold = value;
        }

        public static float ArmorDurabilityPower
        {
            get => Config.Armor.DurabilityParabolicPower;
            set => Config.Armor.DurabilityParabolicPower = value;
        }

        public static float ArmorRicochetChance
        {
            get => Config.Armor.RicochetChance;
            set => Config.Armor.RicochetChance = value;
        }

        public static float ArmorDamageCap
        {
            get => Config.Armor.ArmorDamageCap;
            set => Config.Armor.ArmorDamageCap = value;
        }

        public static float ArmorPenetrationCap
        {
            get => Config.Armor.PenetrationCap;
            set => Config.Armor.PenetrationCap = value;
        }

        public static float ArmorPointsCap
        {
            get => Config.Armor.ArmorPointsCap;
            set => Config.Armor.ArmorPointsCap = value;
        }

        public static bool ForceNoArmor
        {
            get => Config.Armor.ForceNoArmor;
            set => Config.Armor.ForceNoArmor = value;
        }

        public static bool ForceMaxArmor
        {
            get => Config.Armor.ForceMaxArmor;
            set => Config.Armor.ForceMaxArmor = value;
        }

        // Armor HUD
        public static bool ArmorHUDEnabled
        {
            get => Config.Armor.ArmorHUDEnabled;
            set => Config.Armor.ArmorHUDEnabled = value;
        }
        public static float ArmorHUDMarginX
        {
            get => Config.Armor.ArmorHUDMarginX;
            set => Config.Armor.ArmorHUDMarginX = value;
        }
        public static float ArmorHUDMarginY
        {
            get => Config.Armor.ArmorHUDMarginY;
            set => Config.Armor.ArmorHUDMarginY = value;
        }
        public static float ArmorHUDBarWidth
        {
            get => Config.Armor.ArmorHUDBarWidth;
            set => Config.Armor.ArmorHUDBarWidth = value;
        }
        public static float ArmorHUDBarHeight
        {
            get => Config.Armor.ArmorHUDBarHeight;
            set => Config.Armor.ArmorHUDBarHeight = value;
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
