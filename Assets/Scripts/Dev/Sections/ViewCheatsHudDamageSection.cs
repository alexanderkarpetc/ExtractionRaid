using UnityEngine;

namespace Dev
{
    /// <summary>
    /// HUD damage feedback tunables — directional vignette pulse on hit + low-HP edge glow.
    /// Combined into a single full-screen SDF shader with two layers (angular-gated per-hit
    /// sectors + all-edges low-HP heartbeat). See <c>HudDamagePresenter.cs</c> for runtime,
    /// <c>HudDamageDirectional.shader</c> for compositing.
    /// </summary>
    public class ViewCheatsHudDamageSection : ScriptableObject
    {
        [Tooltip("Master toggle. OFF = no overlay rendered (canvas hidden).")]
        public bool Enabled = true;

        [Header("Directional vignette — sector pulse on hit")]
        [Tooltip("Glow color (HDR-friendly). Alpha = max possible opacity for the layer.")]
        public Color BaseColor = new Color(1f, 0.15f, 0.15f, 0.85f);

        [Tooltip("Half-width of the sector arc (degrees). 40 = 80° total, ~quarter screen edge.")]
        [Range(20f, 180f)] public float SectorHalfWidthDeg = 40f;

        [Tooltip("Inner radius (normalized 0..1) below which no glow renders. 0.42 = glow only on outer half.")]
        [Range(0f, 0.9f)] public float InnerRadius = 0.42f;

        [Tooltip("Softness of sector + radial edges (normalized fraction of half-screen). Higher = blurrier feathering.")]
        [Range(0.01f, 0.5f)] public float EdgeSoftness = 0.12f;

        [Tooltip("Time (seconds) for a single hit pulse to fade out.")]
        [Range(0.1f, 1.5f)] public float PulseDuration = 0.5f;

        [Header("Per-hit-kind intensity (matches crosshair hit pulse profiles)")]
        [Tooltip("Pulse intensity (0..1) for a normal body hit (no kill / headshot / ricochet).")]
        [Range(0f, 1f)] public float NormalHitIntensity = 0.7f;

        [Tooltip("Pulse intensity for a kill — bot died from this hit.")]
        [Range(0f, 1f)] public float KillHitIntensity = 1.0f;

        [Tooltip("Pulse intensity for a headshot.")]
        [Range(0f, 1f)] public float HeadshotHitIntensity = 0.95f;

        [Tooltip("Pulse intensity for a ricochet — bullet deflected off armor.")]
        [Range(0f, 1f)] public float RicochetHitIntensity = 0.35f;

        [Header("Low-HP edge glow — all edges, slow heartbeat")]
        [Tooltip("Master toggle for the low-HP layer.")]
        public bool LowHpGlowEnabled = true;

        [Tooltip("HP ratio below which the glow appears. 0.35 = below 35% HP.")]
        [Range(0.05f, 0.6f)] public float LowHpThresholdRatio = 0.35f;

        [Tooltip("Heartbeat pulse frequency (Hz). 0.8 = ~74 BPM.")]
        [Range(0.3f, 3f)] public float LowHpPulseFreqHz = 0.8f;

        [Tooltip("Glow intensity at threshold (just below it).")]
        [Range(0f, 1f)] public float LowHpMinIntensity = 0.25f;

        [Tooltip("Glow intensity at near-zero HP (peak warning).")]
        [Range(0f, 1f)] public float LowHpMaxIntensity = 0.7f;
    }
}
