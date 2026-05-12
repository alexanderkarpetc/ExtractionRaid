using UnityEngine;

namespace Dev
{
    /// <summary>
    /// Magazine drop runtime tunables. Drives <see cref="View.MagazineDropPresenter"/> —
    /// fizzy physics drop of a placeholder magazine GO per Ballistic reload event.
    /// Lasers do not drop magazines (energy cell vent / different visual coming later).
    /// </summary>
    public class ViewCheatsMagazineSection : ScriptableObject
    {
        public bool Enabled = true;

        [Header("Pool")]
        [Tooltip("Max active magazines world-wide. Older ones replaced. Cap проти physics overhead.")]
        [Range(2, 30)] public int MaxActive = 8;

        [Tooltip("Total lifetime у seconds. Last 30% — scale shrink fade-out.")]
        [Range(2f, 30f)] public float Lifetime = 6f;

        [Header("Spawn timing + position")]
        [Tooltip("Delay (seconds) after WeaponReloadStarted before magazine drops. Real reload " +
                 "anim ejects mag mid-animation, not at start. Default ~30% of an avg reload.")]
        [Range(0f, 2f)] public float DropDelay = 0.25f;

        [Tooltip("Magazine origin offset from player position у local-facing space. " +
                 "+X = to the right, +Y = up (typically just below weapon hand), +Z = forward.")]
        public Vector3 SpawnOffset = new(0.15f, 0.8f, 0.25f);

        [Header("Drop velocity (mostly gravity-driven — keep low)")]
        [Tooltip("Downward push at spawn (m/s). 0 = pure gravity drop.")]
        [Range(0f, 3f)] public float DownwardVelocity = 0f;

        [Tooltip("Forward velocity (m/s) — tiny toss to clear feet. Keep small.")]
        [Range(0f, 2f)] public float ForwardVelocity = 0.15f;

        [Tooltip("Random additive jitter on each velocity component (m/s).")]
        [Range(0f, 1f)] public float VelocityJitter = 0.05f;

        [Header("Spin")]
        [Tooltip("Random angular velocity magnitude per axis (rad/s). Small = slow tumble while falling.")]
        [Range(0f, 20f)] public float SpinMagnitude = 1.5f;

        [Header("Physics base values (override Magazine.prefab Rigidbody fields at spawn)")]
        [Range(0.01f, 2f)] public float Mass = 0.15f;
        [Range(0f, 5f)]    public float LinearDamping = 0.8f;
        [Range(0f, 5f)]    public float AngularDamping = 2f;

        [Header("Settle (auto-freeze)")]
        [Tooltip("Seconds after spawn before settle ramp begins. Magazines need a bit longer than casings — heavier object, more bounce.")]
        [Range(0.3f, 5f)] public float SettleDelay = 1.5f;

        [Tooltip("Settle ramp duration.")]
        [Range(0.1f, 3f)] public float SettleTimeout = 1f;

        [Range(1f, 100f)] public float MaxLinearDamping = 30f;
        [Range(1f, 100f)] public float MaxAngularDamping = 30f;

        [Tooltip("After ramp ends → kinematic freeze + disable collider so player walks through.")]
        public bool DisableColliderOnSettle = true;
    }
}
