using System;
using UnityEngine;

namespace Dev
{
    /// <summary>
    /// Per-delivery shake shape — defines geometry (kick magnitude/duration/direction-offset + tremor magnitude/duration).
    /// Composed з <see cref="PayloadShakeModifier"/> by <see cref="View.CameraShakePresenter"/>.
    /// </summary>
    [Serializable]
    public struct DeliveryShakeShape
    {
        [Range(0f, 0.5f)] public float KickMagnitude;
        [Range(0.02f, 0.5f)] public float KickDuration;
        [Tooltip("World-space direction offset added до base recoil vector (-fireDir). Y+ = climb, X+ = side shove.")]
        public Vector3 KickDirOffset;
        [Range(0f, 0.3f)] public float TremorMagnitude;
        [Range(0.02f, 0.5f)] public float TremorDuration;
    }

    /// <summary>
    /// Per-payload shake character — punchy ballistic vs smooth-electric laser.
    /// </summary>
    [Serializable]
    public struct PayloadShakeModifier
    {
        [Range(0.1f, 3f)] public float KickMagnitudeScale;
        [Range(0.1f, 3f)] public float TremorMagnitudeScale;
        [Range(5f, 60f)]  public float TremorFrequency;
    }

    /// <summary>
    /// Gunplay A.3 — Camera shake runtime tunables.
    /// Drives <see cref="View.CameraShake"/> via <see cref="View.CameraShakePresenter"/>.
    /// Position-only shake — top-down camera holds fixed pitch, rotation shake risks
    /// rolling off vertical and breaking aim alignment.
    /// </summary>
    public class ViewCheatsCameraShakeSection : ScriptableObject
    {
        public bool Enabled = true;

        [Tooltip("Master multiplier on every shake call. 0 = mute, 1 = authored values.")]
        [Range(0f, 3f)] public float GlobalScale = 1f;

        // ── Fire (per shot) ──────────────────────────────────────

        [Header("Fire kick")]
        [Tooltip("Base directional kick magnitude (world units) per shot — direction scaled by weapon's RecoilKickForward stat.")]
        [Range(0f, 0.5f)] public float FireKickMagnitude = 0.04f;

        [Tooltip("Mapping factor: weapon.Stats.RecoilKickForward × this = additional kick scale (so heavy weapons shake more).")]
        [Range(0f, 1f)] public float FireRecoilStatScale = 0.015f;

        [Tooltip("Kick recovery duration (seconds, unscaled). Ease-out-quad decay.")]
        [Range(0.02f, 0.5f)] public float FireKickDuration = 0.12f;

        [Tooltip("Omnidirectional tremor intensity layered on top of directional kick — per shot.")]
        [Range(0f, 0.3f)] public float FireTremorMagnitude = 0.02f;

        [Tooltip("Tremor recovery duration on fire (seconds, unscaled).")]
        [Range(0.02f, 0.5f)] public float FireTremorDuration = 0.10f;

        // ── Damage taken ─────────────────────────────────────────

        [Header("Player damage taken")]
        [Tooltip("Base tremor intensity when player takes damage. Scales з damage amount.")]
        [Range(0f, 0.5f)] public float DamageTremorMagnitude = 0.10f;

        [Tooltip("Damage-to-magnitude scaling: per HP point of damage adds this much intensity.")]
        [Range(0f, 0.05f)] public float DamageTremorPerHp = 0.005f;

        [Tooltip("Tremor recovery duration on damage (seconds, unscaled).")]
        [Range(0.05f, 1f)] public float DamageTremorDuration = 0.30f;

        // ── Tremor noise ─────────────────────────────────────────

        [Header("Tremor noise")]
        [Tooltip("Frequency of noise sampling (Hz) for tremor — higher = jittery, lower = sway. Used коли per-archetype shake disabled OR коли payload modifier inactive.")]
        [Range(5f, 60f)] public float TremorFrequency = 25f;

        // ── Per-archetype shake profiles (A1) ─────────────────────────

        [Header("Per-archetype shake (A1)")]
        [Tooltip("When enabled, fire shake selects DeliveryShape × PayloadModifier instead of using the global Fire* fields. Disabled = legacy single-profile path.")]
        public bool PerArchetypeEnabled = true;

        [Header("Delivery shapes")]
        public DeliveryShakeShape SingleActionShape = new DeliveryShakeShape
        {
            KickMagnitude = 0.04f, KickDuration = 0.10f,
            KickDirOffset = new Vector3(0f, 0.3f, 0f),
            TremorMagnitude = 0.015f, TremorDuration = 0.08f,
        };
        public DeliveryShakeShape AutoShape = new DeliveryShakeShape
        {
            KickMagnitude = 0.05f, KickDuration = 0.13f,
            KickDirOffset = new Vector3(0f, 0.6f, 0f),
            TremorMagnitude = 0.025f, TremorDuration = 0.12f,
        };
        public DeliveryShakeShape ScatterShape = new DeliveryShakeShape
        {
            KickMagnitude = 0.10f, KickDuration = 0.20f,
            KickDirOffset = new Vector3(0.4f, 0.8f, 0f),
            TremorMagnitude = 0.05f, TremorDuration = 0.18f,
        };

        [Header("Payload modifiers")]
        public PayloadShakeModifier BallisticModifier = new PayloadShakeModifier
        {
            KickMagnitudeScale = 1.0f, TremorMagnitudeScale = 1.0f, TremorFrequency = 30f,
        };
        public PayloadShakeModifier LaserModifier = new PayloadShakeModifier
        {
            KickMagnitudeScale = 0.7f, TremorMagnitudeScale = 1.3f, TremorFrequency = 18f,
        };
    }
}
