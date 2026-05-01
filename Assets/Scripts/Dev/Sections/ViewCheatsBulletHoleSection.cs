using UnityEngine;

namespace Dev
{
    /// <summary>
    /// Gunplay A.8 — Bullet hole decal runtime tunables.
    /// Drives <see cref="View.BulletHoleDecalPresenter"/>: spawns persistent bullet hole
    /// decals at projectile-vs-surface impact points (skips character hits — those covered
    /// by blood pipeline already).
    ///
    /// Reuses <see cref="View.DecalProjectorPool"/> infrastructure. Surface normal arrives
    /// via extended <c>ProjectileHit</c> event payload — orients decal hugging surface.
    /// </summary>
    public class ViewCheatsBulletHoleSection : ScriptableObject
    {
        public bool Enabled = true;

        [Header("Pool / lifetime")]
        [Tooltip("Max active bullet holes world-wide. Older ones replaced when over capacity.")]
        [Range(20, 500)] public int MaxActive = 200;

        [Tooltip("Decal lifetime (seconds). Last 30% scale-shrinks toward 0.")]
        [Range(5f, 300f)] public float Lifetime = 90f;

        [Header("Spawn gating")]
        [Tooltip("Per-hit spawn chance (0..1). Lower = sparser, less visual clutter on heavy fire.")]
        [Range(0f, 1f)] public float SpawnChance = 1f;

        [Tooltip("Per-collider throttle (seconds). Prevents auto fire stacking decals у same spot.")]
        [Range(0f, 1f)] public float MinTimeBetweenSameSurface = 0.05f;

        [Header("Placement")]
        [Tooltip("Lift decal off the surface to avoid Z-fighting.")]
        [Range(0f, 0.05f)] public float SurfaceOffset = 0.01f;

        [Tooltip("Random scale range (uniform per spawn).")]
        [Range(0.3f, 2f)] public float ScaleMin = 0.5f;
        [Range(0.3f, 2f)] public float ScaleMax = 1.0f;

        [Tooltip("Random offset along surface 'up' axis (meters). Top-down shots cluster horizontally — vertical jitter breaks the trail line. Projected onto wall plane so works for ramps too.")]
        [Range(0f, 1f)] public float SurfaceUpJitter = 0.15f;

        [Tooltip("Random offset along surface 'right' axis (meters). Smaller than vertical — moving player already provides some horizontal scatter.")]
        [Range(0f, 1f)] public float SurfaceRightJitter = 0.05f;
    }
}
