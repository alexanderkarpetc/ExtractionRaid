using System;
using UnityEngine;

namespace Dev
{
    /// <summary>
    /// Per-event-type hit pulse profile. Replaces shared params + per-type modifier multipliers
    /// with full independent control per kind (normal / kill / headshot / ricochet).
    /// </summary>
    [Serializable]
    public struct HitPulseProfile
    {
        public Color Color;
        [Range(0.05f, 2f)] public float Duration;
        [Range(2f, 50f)] public float InnerStart;
        [Range(10f, 120f)] public float InnerEnd;
        [Range(4f, 50f)] public float Length;
        [Range(0.5f, 10f)] public float Thickness;
        [Range(0.02f, 0.5f)] public float BurstPhaseEnd;
        [Range(0.05f, 0.8f)] public float HoldPhaseEnd;
        [Range(0f, 1.5f)] public float RotationRad;
        [Range(0.5f, 2f)] public float ThicknessTaperStart;
        [Range(0.1f, 2f)] public float ThicknessTaperEnd;

        public static HitPulseProfile Default(Color color) => new HitPulseProfile
        {
            Color = color,
            Duration = 0.4f,
            InnerStart = 10f,
            InnerEnd = 50f,
            Length = 16f,
            Thickness = 3f,
            BurstPhaseEnd = 0.12f,
            HoldPhaseEnd = 0.30f,
            RotationRad = 0f,
            ThicknessTaperStart = 1.05f,
            ThicknessTaperEnd = 0.55f,
        };
    }

    /// <summary>
    /// v2 aim cursor — uGUI + SDF shader stack replacing IMGUI <c>AimCursorOverlay</c>.
    /// Stage 1 baseline: 1:1 visual port of v1 (4-line crosshair + dot + reload/charge rings + hit markers).
    /// Later stages extend: directional recoil kick (S2), focus blur (S3), 3-tier range color (S4),
    /// UI cursor swap (S5), bloom/low-ammo/unified arcs (S6). See <c>docs/ai/gunplay/aim-cursor-v2.md</c>.
    /// </summary>
    public class ViewCheatsCrosshairV2Section : ScriptableObject
    {
        [Tooltip("Master toggle. OFF (default) = legacy IMGUI AimCursorOverlay renders. ON = v2 SDF crosshair active, IMGUI no-ops. A/B compare during stage rollout.")]
        public bool UseV2Crosshair = false;

        [Header("Stage 1 — Crosshair geometry (px)")]
        [Range(0f, 30f)] public float Gap = 6f;
        [Range(2f, 40f)] public float LineLength = 10f;
        [Range(0.5f, 6f)] public float LineThickness = 2f;
        [Range(0f, 8f)] public float DotRadius = 2f;

        [Header("ADS — gap / bloom interpolation targets")]
        [Range(0f, 30f)] public float AdsGap = 3f;
        [Tooltip("Bloom extra gap added on fire — decays during cooldown.")]
        [Range(0f, 60f)] public float BloomExtraGap = 18f;
        [Range(0f, 30f)] public float AdsBloomExtraGap = 8f;

        [Header("Reload / charge rings")]
        [Range(10f, 80f)] public float RingRadius = 42f;
        [Range(1f, 8f)] public float RingThickness = 3f;

        [Header("Edge softness (AA)")]
        [Range(0.3f, 4f)] public float EdgeSoftness = 1f;

        [Header("Focus blur — edge softness driven by recoil pressure + ADS state (Stage 3)")]
        [Tooltip("Master toggle. OFF = _EdgeSoftness stays на static EdgeSoftness value (Stage 1 fallback, no regression).")]
        public bool FocusBlurEnabled = true;
        [Tooltip("Baseline edge softness (px) at rest, ADS settled, no recoil. Lower = sharper baseline.")]
        [Range(0.1f, 3f)] public float BlurMinPx = 0.6f;
        [Tooltip("Max edge softness (px) at full accuracy deficit. Higher = more fuzzy при максимальному стресі.")]
        [Range(1f, 8f)] public float BlurMaxPx = 3.0f;
        [Tooltip("World units of weapon.RecoilOffset.magnitude що дорівнюють 'full blur'. Lower = blur saturates faster.")]
        [Range(0.05f, 2f)] public float BlurRecoilSaturation = 0.4f;
        [Tooltip("Weight of recoil pressure у blur calculation. 1 = full contribution. Combined з ADS via max(), not sum.")]
        [Range(0f, 1f)] public float BlurRecoilWeight = 1.0f;
        [Tooltip("How much hip-fire (no ADS) adds to accuracy deficit. 0 = no diff hip/ADS, 1 = hip-fire reaches BlurMax on its own.")]
        [Range(0f, 1f)] public float BlurHipFireAmount = 0.3f;

        [Header("Outline")]
        [Tooltip("Outline ring color drawn behind face. Default black з 85% opacity for readability over busy backgrounds.")]
        public Color OutlineColor = new Color(0f, 0f, 0f, 0.85f);
        [Tooltip("Outline ring width (px). 0 = no outline.")]
        [Range(0f, 6f)] public float OutlineWidth = 1.5f;

        [Header("ADS — top arm cutoff")]
        [Tooltip("Binary threshold on adsAmount (lerp toward IsADS). Below = top arm shown (4 arms), above = hidden (3 arms). Cleaner than smooth fade for Stage 1. 1 = always show, 0 = always hide.")]
        [Range(0f, 1f)] public float AdsTopArmFadeStart = 0.5f;

        [Header("Colors")]
        public Color NormalColor   = Color.white;
        public Color BloomColor    = new Color(1f, 0.9f, 0.4f, 1f);
        public Color WarningColor  = new Color(1f, 0.4f, 0.3f, 1f); // dry-fire / out of ammo

        [Header("Charge — flame fill inside crosshair gap (white→yellow→red gradient)")]
        [Tooltip("Inner segment color — close to dot, 'just-lit' tip of flame.")]
        public Color ChargeColorCold = Color.white;
        [Tooltip("Middle segment color — warming up.")]
        public Color ChargeColorMid  = new Color(1f, 0.85f, 0.2f, 1f);
        [Tooltip("Outer segment color — overheating, near arm edge.")]
        public Color ChargeColorHot  = new Color(1f, 0.3f, 0.1f, 1f);
        [Tooltip("Bar thickness as ratio of LineThickness. 0.7 = slightly thinner than main arms.")]
        [Range(0.2f, 2f)] public float ChargeBarThicknessRatio = 0.7f;

        [Header("Charge — overheat tremble (near max charge)")]
        [Tooltip("ChargeFill above this triggers cursor tremble. 0.85 = activates у last 15% of charge.")]
        [Range(0.5f, 1f)] public float ChargeOverheatThreshold = 0.85f;
        [Tooltip("Maximum cursor tremble offset (px) at ChargeFill = 1.0. Scales linearly з overheat fraction.")]
        [Range(0f, 8f)] public float ChargeOverheatTremblePx = 2.5f;
        [Tooltip("Tremble noise frequency (Hz). Higher = more jittery / vibrating.")]
        [Range(5f, 80f)] public float ChargeOverheatTrembleFreq = 35f;

        [Header("Rolling / fading")]
        [Range(0f, 1f)] public float RollingAlpha = 0.3f;

        [Header("Laser cursor — segmented ring (replaces 4-arm when active weapon payload = Laser)")]
        [Tooltip("How many segments around the ring. 12 = clock-style baseline. Higher = smoother fill, more compute.")]
        [Range(4, 24)] public int LaserSegmentCount = 12;
        [Tooltip("Ring inner radius (px from center).")]
        [Range(4f, 60f)] public float LaserRingInnerRadius = 14f;
        [Tooltip("Ring outer radius (px from center). Must be > inner for a visible band.")]
        [Range(6f, 80f)] public float LaserRingOuterRadius = 22f;
        [Tooltip("Angular gap between adjacent segments (degrees). Visual separation; 0 = solid donut.")]
        [Range(0f, 30f)] public float LaserSegmentGapDeg = 4f;
        [Tooltip("Alpha multiplier for inactive segments (chargeRatio not yet reached them). 0 = invisible, 1 = same as active.")]
        [Range(0f, 1f)] public float LaserInactiveAlpha = 0.22f;
        [Tooltip("Radial pulse expansion (px) on fire — ring inhales outward then springs back over weapon.FireInterval. Inner radius shrinks by this, outer grows by it. 0 = no pulse animation on shot.")]
        [Range(0f, 20f)] public float LaserFirePulseRadiusPx = 5f;

        [Header("Hit pulse — per-event-type profiles (4 diagonal stubs on cursor, spread + fade)")]
        [Tooltip("Normal body hit (no kill, no headshot, no ricochet).")]
        public HitPulseProfile NormalProfile = HitPulseProfile.Default(Color.white);

        [Tooltip("Kill confirm — usually larger / longer / red.")]
        public HitPulseProfile KillProfile = HitPulseProfile.Default(new Color(1f, 0.3f, 0.3f, 1f));

        [Tooltip("Headshot — gold tint. Note: priority Ricochet > Kill > Headshot > Normal; headshot+kill uses Kill profile.")]
        public HitPulseProfile HeadshotProfile = HitPulseProfile.Default(new Color(1f, 0.85f, 0.2f, 1f));

        [Tooltip("Ricochet — armor deflected, no damage. Usually short flash / blue.")]
        public HitPulseProfile RicochetProfile = HitPulseProfile.Default(new Color(0.4f, 0.7f, 1f, 1f));
    }
}
