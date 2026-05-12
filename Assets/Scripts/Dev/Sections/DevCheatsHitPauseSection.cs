using UnityEngine;

namespace Dev
{
    /// <summary>
    /// Gunplay A.1 — Hit Pause / Hitstop runtime tunables.
    /// Brief slowdown of <c>Time.timeScale</c> on hit confirmation.
    ///
    /// Top-down twin-stick reality: at 600+ RPM auto fire and multi-target horde scenarios
    /// hit pause on *every* hit reads as input lag, not weight. Defaults therefore emphasize
    /// only low-frequency events — kills and headshots — following Hotline Miami / Hades pattern.
    /// Normal-hit and ricochet pauses default to 0; raise only if a specific weapon archetype
    /// wants extra punch and is shown to not stutter sustained fire.
    /// </summary>
    public class DevCheatsHitPauseSection : ScriptableObject
    {
        public bool Enabled = true;

        [Tooltip("Time.timeScale during pause window. 0.05 = nearly frozen, 0.2 = soft slow.")]
        [Range(0.01f, 0.5f)] public float PausedTimeScale = 0.05f;

        [Tooltip("Pause duration on regular body hit (seconds, unscaled). Default 0 — sustained-fire stutter at top-down tempo.")]
        [Range(0f, 0.3f)] public float NormalDuration = 0f;

        [Tooltip("Pause duration on headshot (seconds, unscaled). Rewards skill, low frequency.")]
        [Range(0f, 0.3f)] public float HeadshotDuration = 0.05f;

        [Tooltip("Pause duration on kill (seconds, unscaled). Low-frequency dramatic punctuation.")]
        [Range(0f, 0.3f)] public float KillDuration = 0.08f;

        [Tooltip("Pause duration on ricochet. Default 0 — blue spark marker is sufficient feedback.")]
        [Range(0f, 0.2f)] public float RicochetDuration = 0f;

        [Tooltip("Multiplier applied to all durations — global feel knob.")]
        [Range(0f, 3f)] public float GlobalScale = 1f;
    }
}
