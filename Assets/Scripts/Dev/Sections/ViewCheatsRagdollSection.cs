using UnityEngine;

namespace Dev
{
    /// <summary>
    /// Gunplay A.9 — Ragdoll runtime tunables.
    /// On <c>EntityDied</c> event, character body switches to physics-driven ragdoll з
    /// directional impulse based on shot. Drives <see cref="View.RagdollController"/> via
    /// <see cref="View.RagdollPresenter"/>.
    /// </summary>
    public class ViewCheatsRagdollSection : ScriptableObject
    {
        public bool Enabled = true;

        // ── Impulse magnitude pipeline ──────────────────────────
        // Final impulse = clamp(damage × ImpulseScale, MinImpulse, MaxImpulse)
        // applied to nearest bone (chain transmits through joints до Hips для whole-body
        // displacement — no need for direct hip push).

        [Header("Impulse — magnitude")]
        [Tooltip("Multiplier: damage × this = base impulse magnitude (kg·m/s).")]
        [Range(0f, 5f)] public float ImpulseScale = 0.3f;

        [Tooltip("Floor — applied even у low-damage / status deaths so corpse still topples.")]
        [Range(0f, 10f)] public float MinImpulse = 1.5f;

        [Tooltip("Hard cap — prevents extreme damage values from sending body flying. Tune per-feel.")]
        [Range(0f, 30f)] public float MaxImpulse = 5f;

        [Tooltip("Multiplier на impulse applied directly to Hips (whole-body kick). " +
                 "Combined з local-bone impulse — local makes limb flop, Hips makes body move у напрямку shot. " +
                 "Set 0 щоб лише local impulse (joint chain transmits) — gentle. Set 1 — full whole-body push.")]
        [Range(0f, 1.5f)] public float HipsImpulseScale = 0.5f;

        [Header("Impulse — direction")]
        [Tooltip("Upward bias added to horizontal impulse direction. Adds satisfying lift to the fall.")]
        [Range(0f, 1f)] public float UpwardImpulseBias = 0.2f;

        // ── Damping (applied to all bone Rigidbodies at activation) ─────────
        // Counteracts wild tumble. Higher = stiffer ragdoll. Real "rag dolls" have
        // mostly self-damping cloth → moderate damping feels organic.

        [Header("Damping (на activate)")]
        [Tooltip("Linear drag — slows tumble. 0 = no air resistance, higher = settles faster.")]
        [Range(0f, 5f)] public float LinearDamping = 0.5f;

        [Tooltip("Angular drag — slows spin. Higher = limbs не whirling.")]
        [Range(0f, 10f)] public float AngularDamping = 1.5f;

        // ── Joint stiffness ─────────────────────────────────────
        // CharacterJoint defaults are spring-less — limbs flap freely and gravity wins,
        // arms/legs sag toward ground у seconds. Adding a soft spring до swing/twist
        // limits creates resistance that holds the body shape against gravity, while
        // still allowing a believable flop on impact.

        [Header("Joint stiffness")]
        [Tooltip("Spring force on joint swing/twist limits. 0 = floppy (limbs sag fast), " +
                 "higher = stiffer (limbs hold pose longer). 5–20 typical для chibi-feel.")]
        [Range(0f, 100f)] public float JointSpringForce = 10f;

        [Tooltip("Spring damper on joint swing/twist limits. Damps oscillation. " +
                 "Higher = settles faster, less bouncy.")]
        [Range(0f, 20f)] public float JointSpringDamper = 2f;

        // ── Head joint limits ───────────────────────────────────
        // Default CharacterJoint limits (±40° swing, ±20° twist) let the head spin wildly
        // when an impulse hits — looks like an exorcism. Real necks are stiff. Tighten
        // head joint specifically; інші joints stay floppy so limbs still flop.

        [Header("Head joint limits")]
        [Tooltip("Head twist limit (degrees). Low values prevent head spinning. ~30° humanoid range.")]
        [Range(0f, 90f)] public float HeadTwistLimit = 30f;

        [Tooltip("Head swing limit (degrees). Front/back + side bend. ~60° humanoid range.")]
        [Range(0f, 90f)] public float HeadSwingLimit = 60f;

        // ── Lifecycle ───────────────────────────────────────────

        [Header("Lifecycle")]
        [Tooltip("Active physics phase (seconds). After this — ragdoll freezes у current pose, kinematic.")]
        [Range(1f, 30f)] public float SettleAfter = 5f;

        [Tooltip("Total ragdoll lifetime (seconds). After this — body destroyed.")]
        [Range(5f, 120f)] public float Lifetime = 30f;
    }
}
