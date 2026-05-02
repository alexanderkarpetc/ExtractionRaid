using System;
using UnityEngine;

namespace Dev
{
    /// <summary>
    /// Gunplay A.9 — Ragdoll runtime tunables. Two profiles (Headshot vs Bodyshot)
    /// drive distinct death silhouettes так, щоб top-down camera читала kill type без HUD.
    /// On <c>EntityDied</c> event, character body switches to physics-driven ragdoll з
    /// directional impulse + stagger phase. Drives <see cref="View.RagdollController"/> via
    /// <see cref="View.RagdollPresenter"/>.
    ///
    /// Death silhouettes:
    /// <list type="bullet">
    /// <item><b>Headshot — "lights out"</b>: zero stagger, hips impulse off, body falls
    /// vertically на місці. Spine immediately soft → knees buckle первими.</item>
    /// <item><b>Bodyshot — "wounded knockback"</b>: stagger 0.3–0.5s with stiff spine,
    /// strong hips push у напрямку shot → body steps back, then collapses.</item>
    /// </list>
    /// </summary>
    public class ViewCheatsRagdollSection : ScriptableObject
    {
        public bool Enabled = true;

        [Serializable]
        public class HitProfile
        {
            // ── Impulse magnitude ────────────────────────────────
            [Tooltip("damage × this = base impulse magnitude (kg·m/s).")]
            [Range(0f, 5f)] public float ImpulseScale = 0.5f;

            [Tooltip("Floor — applied even у low-damage deaths.")]
            [Range(0f, 10f)] public float MinImpulse = 3f;

            [Tooltip("Hard cap — prevents extreme damage values from launching body.")]
            [Range(0f, 30f)] public float MaxImpulse = 10f;

            [Tooltip("Multiplier на impulse applied directly to Hips (whole-body kick). " +
                     "0 = body falls на місці. 1 = full knockback у напрямку shot.")]
            [Range(0f, 1.5f)] public float HipsImpulseScale = 0.5f;

            [Tooltip("Upward bias on impulse direction. Adds lift to the fall.")]
            [Range(0f, 1f)] public float UpwardImpulseBias = 0.3f;

            // ── Stagger (alive→limp transition) ──────────────────
            [Tooltip("Stagger duration (s). Body holds stiff spine for this long → 'fights' " +
                     "gravity briefly → ramps to soft. 0 = instant full limp (no fight). " +
                     "Headshot typically 0; bodyshot typically 0.3–0.5.")]
            [Range(0f, 1f)] public float StaggerDuration = 0.4f;

            [Tooltip("Spring multiplier during stagger phase. Applied on top of base " +
                     "JointSpringForce. 0 = no stagger boost. 50–200 для visible stagger.")]
            [Range(0f, 500f)] public float StaggerSpringMultiplier = 100f;
        }

        [Header("Headshot profile (lights-out)")]
        [Tooltip("Headshot kill: instant limp, no hips push — body folds straight down.")]
        public HitProfile Headshot = new HitProfile
        {
            ImpulseScale            = 0.3f,
            MinImpulse              = 1f,
            MaxImpulse              = 5f,
            HipsImpulseScale        = 0f,    // body на місці, no knockback
            UpwardImpulseBias       = 0.1f,
            StaggerDuration         = 0f,    // no fight — instant limp
            StaggerSpringMultiplier = 0f,
        };

        [Header("Bodyshot profile (wounded knockback)")]
        [Tooltip("Bodyshot kill: stagger window with stiff spine, then collapse + knockback.")]
        public HitProfile Bodyshot = new HitProfile
        {
            ImpulseScale            = 0.5f,
            MinImpulse              = 3f,
            MaxImpulse              = 10f,
            HipsImpulseScale        = 0.5f,  // knockback у напрямку shot
            UpwardImpulseBias       = 0.3f,
            StaggerDuration         = 0.4f,  // body fights briefly
            StaggerSpringMultiplier = 100f,
        };

        // ── Damping (applied to all bone Rigidbodies at activation) ─────────
        // Counteracts wild tumble. Higher = stiffer ragdoll. Real "rag dolls" have
        // mostly self-damping cloth → moderate damping feels organic.

        [Header("Damping (на activate)")]
        [Tooltip("Linear drag — slows tumble. 0 = no air resistance, higher = settles faster.")]
        [Range(0f, 5f)] public float LinearDamping = 0.5f;

        [Tooltip("Angular drag — slows spin. Higher = limbs не whirling.")]
        [Range(0f, 10f)] public float AngularDamping = 1.5f;

        // ── Joint stiffness ─────────────────────────────────────
        // Base soft springs hold limbs against gravity (anti-sag). Stagger boosts це
        // multiplicatively for the first N seconds — see HitProfile.StaggerSpringMultiplier.

        [Header("Joint stiffness (base — after stagger)")]
        [Tooltip("Spring force on joint swing/twist limits. 0 = floppy (limbs sag fast), " +
                 "higher = stiffer (limbs hold pose longer). 5–20 typical для chibi-feel.")]
        [Range(0f, 100f)] public float JointSpringForce = 10f;

        [Tooltip("Spring damper on joint swing/twist limits. Damps oscillation.")]
        [Range(0f, 20f)] public float JointSpringDamper = 2f;

        // ── Head joint limits ───────────────────────────────────

        [Header("Head joint limits")]
        [Tooltip("Head twist limit (degrees). Prevents exorcist-spin. ~30° humanoid range.")]
        [Range(0f, 90f)] public float HeadTwistLimit = 30f;

        [Tooltip("Head swing limit (degrees). ~60° humanoid range.")]
        [Range(0f, 90f)] public float HeadSwingLimit = 60f;

        // ── Mass distribution ───────────────────────────────────
        // Heavy hips = stable center of mass для bodyshot push readability.
        // Light head + light arms = trail behind, fall природно. Applied at runtime —
        // overrides whatever the prefab utility baked у.

        [Header("Mass distribution (runtime override)")]
        [Tooltip("Hips mass — анchor for whole-body push. Heavier = stable center.")]
        [Range(1f, 12f)] public float HipsMass = 6f;

        [Tooltip("Head mass — light = falls fast on headshot, trails on bodyshot.")]
        [Range(0.5f, 4f)] public float HeadMass = 1f;

        [Tooltip("UpperArm mass — light = arms trail naturally.")]
        [Range(0.3f, 2f)] public float UpperArmMass = 0.7f;

        // ── Lifecycle ───────────────────────────────────────────

        [Header("Lifecycle")]
        [Tooltip("Active physics phase (seconds). After this — ragdoll freezes у current pose, kinematic.")]
        [Range(1f, 30f)] public float SettleAfter = 5f;

        [Tooltip("Total ragdoll lifetime (seconds). After this — body destroyed.")]
        [Range(5f, 120f)] public float Lifetime = 30f;
    }
}
