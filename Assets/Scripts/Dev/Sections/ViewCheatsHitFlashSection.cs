using UnityEngine;

namespace Dev
{
    /// <summary>
    /// Character hit flash + bullet decal tunables. Drives VibeCharacterShader's
    /// rim flash + decal array via MaterialPropertyBlock on BotView.
    ///
    /// Migrated DevCheats → ViewCheats 2026-05-05 — pure visual feedback з no
    /// gameplay impact, fits ViewCheats scope better.
    /// </summary>
    public class ViewCheatsHitFlashSection : ScriptableObject
    {
        public bool Enabled = true;

        [Tooltip("Rim flash duration (seconds, unscaled). Decay is ease-out — perceived shorter than literal.")]
        [Range(0f, 1.5f)] public float Duration = 0.5f;

        [Tooltip("Rim flash peak intensity. 1 = match flash color brightness, 2-3 = HDR glow with bloom.")]
        [Range(0f, 5f)] public float Intensity = 2f;

        [Tooltip("Legacy emission boost — kept for SO back-compat after VibeCharacterShader migration. Not used by rim flash.")]
        [Range(0f, 20f)] public float EmissionBoost = 0f;

        [Tooltip("Tint for normal body shot.")]
        public Color NormalColor = Color.white;

        [Tooltip("Tint for headshot.")]
        public Color HeadshotColor = new(1f, 0.85f, 0.2f, 1f); // gold

        [Tooltip("Tint for ricochet (no damage went through).")]
        public Color RicochetColor = new(0.4f, 0.7f, 1f, 1f); // light blue

        [Tooltip("Tint for kill — overrides normal/headshot colors when isKill flag set.")]
        public Color KillColor = new(1f, 0.25f, 0.25f, 1f); // red

        [Header("Rim Shape (advanced)")]
        [Tooltip("Falloff sharpness — higher = thinner edge band.")]
        [Range(0.5f, 8f)] public float RimPower = 2.5f;

        [Tooltip("Width of the rim band — 1 = full surface, 0.3 = thin silhouette only.")]
        [Range(0.05f, 1f)] public float RimWidth = 0.6f;

        [Header("Bullet Decals")]
        [Tooltip("Color of the persistent blood decal at impact point.")]
        public Color DecalColor = new(0.85f, 0.05f, 0.05f, 1f);

        [Tooltip("Decal radius (world units, meters). 0.35 ≈ palm-size on chibi character.")]
        [Range(0.05f, 1f)] public float DecalRadius = 0.35f;

        [Tooltip("Edge softness — 0 = hard circle, 1 = full feather. Smaller = sharper splat.")]
        [Range(0.05f, 1f)] public float DecalSoftness = 0.55f;

        [Tooltip("Decal fade-out duration (seconds, unscaled). Decay is linear from 1 → 0.")]
        [Range(0.5f, 30f)] public float DecalLifetime = 8f;

        [Tooltip("Constant Y offset applied to hit position before placing decal " +
                 "(world units). Negative pushes decals down — useful when projectile " +
                 "hits land at face level but the body shape lives lower.")]
        [Range(-1f, 1f)] public float DecalYOffset = -0.25f;

        [Tooltip("Random ±Y jitter added per decal so consecutive hits don't stack at " +
                 "exactly the same height.")]
        [Range(0f, 0.5f)] public float DecalYJitter = 0.1f;
    }
}
