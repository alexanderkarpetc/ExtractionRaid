using UnityEngine;

namespace Dev
{
    /// <summary>
    /// Gunplay A.6 — Casing ejection runtime tunables.
    /// Drives <see cref="View.CasingEjectorPresenter"/> — спавн small brass shell прefab
    /// per <c>WeaponFired</c> event з physics velocity + spin. Adds visceral "machine
    /// working" layer без gameplay logic dependency.
    /// </summary>
    public class ViewCheatsCasingsSection : ScriptableObject
    {
        public bool Enabled = true;

        [Header("Pool")]
        [Tooltip("Max active casings world-wide. Older ones replaced. Hard cap проти physics overhead during sustained fire.")]
        [Range(5, 200)] public int MaxActive = 40;

        [Tooltip("Total lifetime у seconds. Last 30% does scale shrink fade-out.")]
        [Range(2f, 30f)] public float Lifetime = 6f;

        [Header("Spawn position")]
        [Tooltip("Eject port offset from fire origin (muzzle position) у local weapon space. " +
                 "+X = to the right (typical eject port), +Y = up, -Z = back toward player.")]
        public Vector3 EjectPortOffset = new(0.05f, 0.02f, -0.10f);

        [Header("Ejection velocity")]
        [Tooltip("Lateral eject velocity along weapon's right axis (m/s). Real shells fly to right side of gun.")]
        [Range(0f, 5f)] public float LateralVelocity = 1.6f;

        [Tooltip("Upward eject velocity (m/s).")]
        [Range(0f, 5f)] public float UpwardVelocity = 1.2f;

        [Tooltip("Backward velocity (m/s) — shell flies slightly back as it ejects.")]
        [Range(0f, 3f)] public float BackwardVelocity = 0.4f;

        [Tooltip("Random additive jitter on each velocity component (m/s).")]
        [Range(0f, 2f)] public float VelocityJitter = 0.5f;

        [Header("Spin")]
        [Tooltip("Random angular velocity magnitude per axis (rad/s). Casings tumble в air.")]
        [Range(0f, 50f)] public float SpinMagnitude = 18f;

        [Header("Physics base values (overrides Casing.prefab Rigidbody fields at spawn)")]
        [Tooltip("Mass (kg) at spawn. Heavier = harder to push when player walks on it.")]
        [Range(0.005f, 1f)] public float Mass = 0.05f;

        [Tooltip("Base linear damping (air drag) при initial fly. Higher = settles faster, less bouncy.")]
        [Range(0f, 5f)] public float LinearDamping = 0.7f;

        [Tooltip("Base angular damping. Higher = stops spinning/rolling faster.")]
        [Range(0f, 5f)] public float AngularDamping = 1.5f;

        [Header("Settle (auto-freeze after delay)")]
        [Tooltip("Seconds after spawn before settle ramp begins. Initial physics phase = pure juice.")]
        [Range(0.3f, 5f)] public float SettleDelay = 1.5f;

        [Tooltip("Settle ramp duration. During цей window damping linearly grows from base to max — natural deceleration.")]
        [Range(0.1f, 3f)] public float SettleTimeout = 1f;

        [Tooltip("Peak linear damping at settle ramp end. Velocity decays exponentially toward 0.")]
        [Range(1f, 100f)] public float MaxLinearDamping = 30f;

        [Tooltip("Peak angular damping at settle ramp end. Spin/roll dies out completely.")]
        [Range(1f, 100f)] public float MaxAngularDamping = 30f;

        [Tooltip("After ramp ends → kinematic freeze. Casing parked in world, player walks through.")]
        public bool DisableColliderOnSettle = true;
    }
}

