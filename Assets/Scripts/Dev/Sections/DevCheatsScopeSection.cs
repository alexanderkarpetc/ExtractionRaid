using UnityEngine;

namespace Dev
{
    /// <summary>
    /// Sniper-scope tuning — all the scope/aiming feel knobs in one place (see the friendly
    /// grouped editor in DevCheatsScopeSectionEditor). Consumed via DevCheats.Scope* accessors by:
    /// PlayerVisionSystem (engage distance), AimingSystem (aim spring), RaidCameraController
    /// (camera lean/zoom) and FogOfWarController (reticle circle + vignette).
    /// </summary>
    public class DevCheatsScopeSection : ScriptableObject
    {
        // ── Engage: how the scope blends in by aim distance from the player ──
        public float NearDistance = 4f;    // cursor closer than this → plain dot (no scope)
        public float FarDistance = 13f;    // cursor past this → full sniper scope

        // ── Aim spring: the scoped aim is a damped spring chasing the cursor (weight) ──
        public float SpringStiffnessLow = 120f;   // reach speed at worst ergo (soft / slow)
        public float SpringStiffnessHigh = 900f;  // reach speed at best ergo (stiff / snappy)
        public float SpringDampingLow = 0.45f;    // damping ζ at worst ergo (<1 = overshoot + bounce)
        public float SpringDampingHigh = 1f;      // damping ζ at best ergo (1 = critical, no bounce)
        public float ErgoImpact = 1f;             // ergo curve exponent (1 = linear, >1 = only high ergo feels tight)

        // ── Camera while scoped ──
        public float CursorInfluenceMul = 1.6f;   // extra lean toward the cursor (1 = none)
        public float ZoomMul = 0.92f;             // extra zoom-in (1 = none, lower = closer)

        // ── Reticle circle + vignette ──
        public float CircleRadius = 0.2f;         // reveal circle radius as a fraction of screen height
        public float CircleDark = 0.9f;           // how dark everything outside the circle gets (0..1)
        public float RingThickness = 0.006f;      // scope rim ring thickness (UV)
        public float RingBright = 0.3f;           // ring + crosshair highlight strength
    }
}
