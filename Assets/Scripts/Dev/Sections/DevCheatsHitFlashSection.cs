using UnityEngine;

namespace Dev
{
    /// <summary>
    /// Gunplay A.2 — Character hit flash runtime tunables.
    /// Briefly tints character renderer to flash color via MaterialPropertyBlock.
    /// "This thing took damage" universal feedback layer.
    /// </summary>
    public class DevCheatsHitFlashSection : ScriptableObject
    {
        public bool Enabled = true;

        [Tooltip("Flash duration (seconds, unscaled). Decay is ease-out — perceived shorter than literal.")]
        [Range(0f, 0.4f)] public float Duration = 0.12f;

        [Tooltip("Color blend strength at peak (0 = no tint, 1 = full color override). Drives _BaseColor/_Color when shader exposes them.")]
        [Range(0f, 1f)] public float Intensity = 0.7f;

        [Tooltip("Emission HDR multiplier at peak — visibly brighter. 0 = no emission boost, 5+ glows clearly with bloom on.")]
        [Range(0f, 20f)] public float EmissionBoost = 6f;

        [Tooltip("Tint for normal body shot.")]
        public Color NormalColor = Color.white;

        [Tooltip("Tint for headshot.")]
        public Color HeadshotColor = new(1f, 0.85f, 0.2f, 1f); // gold

        [Tooltip("Tint for ricochet (no damage went through).")]
        public Color RicochetColor = new(0.4f, 0.7f, 1f, 1f); // light blue

        [Tooltip("Tint for kill — overrides normal/headshot colors when isKill flag set.")]
        public Color KillColor = new(1f, 0.25f, 0.25f, 1f); // red
    }
}
