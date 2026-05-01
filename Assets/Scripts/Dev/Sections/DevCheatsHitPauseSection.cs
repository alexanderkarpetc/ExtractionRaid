using UnityEngine;

namespace Dev
{
    /// <summary>
    /// Tier 9 / Gunplay A.1 — Hit Pause / Hitstop runtime tunables.
    /// Brief slowdown of <c>Time.timeScale</c> on hit confirmation. Returnal/Hades pattern —
    /// gives every successful shot perceptible weight. Granular per-event so headshots and
    /// kills feel more punctuated than chip damage.
    /// </summary>
    public class DevCheatsHitPauseSection : ScriptableObject
    {
        public bool Enabled = true;

        [Tooltip("Time.timeScale during pause window. 0.05 = nearly frozen, 0.2 = soft slow.")]
        [Range(0.01f, 0.5f)] public float PausedTimeScale = 0.05f;

        [Tooltip("Pause duration on regular body hit (seconds, unscaled).")]
        [Range(0f, 0.3f)] public float NormalDuration = 0.03f;

        [Tooltip("Pause duration on headshot (seconds, unscaled).")]
        [Range(0f, 0.3f)] public float HeadshotDuration = 0.06f;

        [Tooltip("Pause duration on kill (seconds, unscaled).")]
        [Range(0f, 0.3f)] public float KillDuration = 0.08f;

        [Tooltip("Pause duration on ricochet (less weight than a real hit).")]
        [Range(0f, 0.2f)] public float RicochetDuration = 0.02f;

        [Tooltip("Multiplier applied to all durations — global feel knob.")]
        [Range(0f, 3f)] public float GlobalScale = 1f;
    }
}
