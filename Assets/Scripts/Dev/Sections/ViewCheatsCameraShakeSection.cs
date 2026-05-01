using UnityEngine;

namespace Dev
{
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
        [Tooltip("Frequency of noise sampling (Hz) for tremor — higher = jittery, lower = sway.")]
        [Range(5f, 60f)] public float TremorFrequency = 25f;
    }
}
