using UnityEngine;

namespace Dev
{
    /// <summary>
    /// Gunplay A.4 — Blood decal runtime tunables.
    /// Drives <see cref="View.BloodDecalPresenter"/>: floor decals на ground beneath hit point
    /// + optional wall splatters behind penetrated targets. Particle blood spray уже covered
    /// by ProjectilePresenter via existing BodyImpact.prefab; this section adds the persistent
    /// decoration layer (decals stay 30s by default, fade out, replaced by pool when full).
    /// </summary>
    public class ViewCheatsBloodDecalSection : ScriptableObject
    {
        public bool Enabled = true;

        // ── Pool / lifetime ──────────────────────────────────────

        [Header("Pool / lifetime")]
        [Tooltip("Max active floor decals world-wide. Older ones replaced when hit; prevents decal pollution.")]
        [Range(10, 300)] public int MaxActiveFloorDecals = 100;

        [Tooltip("Max active wall splatter decals world-wide.")]
        [Range(5, 200)] public int MaxActiveWallDecals = 30;

        [Tooltip("Decal lifetime у seconds before forced cleanup. Last 30% fades alpha to 0.")]
        [Range(2f, 120f)] public float Lifetime = 30f;

        // ── Spawn gating ─────────────────────────────────────────

        [Header("Spawn gating")]
        [Tooltip("Per-target throttle (seconds) — same target won't drop another decal у цей window. Prevents auto fire pollution.")]
        [Range(0f, 2f)] public float MinTimeBetweenDecalsPerTarget = 0.3f;

        [Tooltip("Per-hit spawn probability (0..1). Adds organic gaps, не кожний hit лишає decal.")]
        [Range(0f, 1f)] public float SpawnChance = 0.7f;

        [Tooltip("Minimum (1 - absorption) для decal spawn. > 0.5 = only on penetrating hits; 0 = even high-armor hits leave drops.")]
        [Range(0f, 1f)] public float MinPenetrationFraction = 0.3f;

        // ── Floor decal ──────────────────────────────────────────

        [Header("Floor decal")]
        [Tooltip("Maximum raycast distance downward від character feet до ground.")]
        [Range(0.5f, 10f)] public float FloorRaycastMaxDistance = 5f;

        [Tooltip("Lift decal off the floor by this much (avoid Z-fighting / sub-surface clipping).")]
        [Range(0f, 0.1f)] public float FloorOffset = 0.02f;

        [Tooltip("Minimum / maximum scale multiplier for randomization. Each decal gets a uniform random value у цьому діапазоні.")]
        [Range(0.3f, 2f)] public float FloorScaleMin = 0.6f;
        [Range(0.3f, 2f)] public float FloorScaleMax = 1.2f;

        [Tooltip("Random XZ offset radius around character center (meters). 0 = exactly at center; 0.4 = within a 40cm circle.")]
        [Range(0f, 1.5f)] public float FloorRandomRadius = 0.4f;

        // ── Wall decal ───────────────────────────────────────────

        [Header("Wall splatter")]
        [Tooltip("Spawn wall splatter behind target когда projectile penetrates? Top-down angle limits visibility.")]
        public bool EnableWallSplatter = true;

        [Tooltip("Maximum raycast distance forward (along projectile) from hit point до wall behind.")]
        [Range(0.5f, 10f)] public float WallRaycastMaxDistance = 5f;

        [Tooltip("Lift wall decal off the wall surface (avoid Z-fighting).")]
        [Range(0f, 0.1f)] public float WallOffset = 0.02f;

        [Range(0.3f, 2f)] public float WallScaleMin = 0.5f;
        [Range(0.3f, 2f)] public float WallScaleMax = 1f;

        [Tooltip("Random offset along wall 'up' axis (meters). Top-down shots cluster horizontally — vertical jitter breaks trail line.")]
        [Range(0f, 1f)] public float WallUpJitter = 0.2f;

        [Tooltip("Random offset along wall 'right' axis (meters). Smaller — character movement already adds horizontal scatter.")]
        [Range(0f, 1f)] public float WallRightJitter = 0.1f;
    }
}
