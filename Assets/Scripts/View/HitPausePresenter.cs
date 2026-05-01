using Adapters;
using Dev;
using Session;
using UnityEngine;

namespace View
{
    /// <summary>
    /// Gunplay A.1 — Hit Pause / Hitstop. Briefly slows <c>Time.timeScale</c> on every
    /// player-confirmed hit — gives each shot perceptible weight (Returnal/Hades pattern).
    ///
    /// Listens to <see cref="RaidEventType.HitConfirmed"/>; tracks pause end time via
    /// <c>Time.unscaledTime</c> so input remains responsive during pause. Restores
    /// timeScale to 1 once window expires.
    ///
    /// Note: this is view-layer only. Gameplay logic ticks via Unity's <c>Time.deltaTime</c>
    /// (which IS scaled), so the entire game world slows briefly — exactly the desired
    /// effect. Input polling is unaffected by timeScale, so player keeps responsiveness.
    /// </summary>
    public class HitPausePresenter
    {
        float _pauseEndTimeUnscaled;
        bool  _pauseActive;

        public void LateTick(RaidSession session)
        {
            var cfg = DevCheats.Config?.HitPause;

            if (session != null && cfg != null && cfg.Enabled)
            {
                var events = session.ConsumeEvents();
                foreach (var e in events.All)
                {
                    if (e.Type != RaidEventType.HitConfirmed) continue;

                    // RaidEventBuffer.HitConfirmed packs flags into:
                    //   Damage    = isKill ? 1 : 0
                    //   Direction.x = isHeadshot ? 1 : 0
                    //   MaxHp     = isRicochet ? 1 : 0
                    bool isKill     = e.Damage > 0.5f;
                    bool isHeadshot = e.Direction.x > 0.5f;
                    bool isRicochet = e.MaxHp > 0.5f;

                    float duration = ResolvePauseDuration(cfg, isKill, isHeadshot, isRicochet) * cfg.GlobalScale;
                    if (duration <= 0f) continue;

                    // Use unscaled time so consecutive hits during an active pause
                    // extend it correctly (we don't want a 0-deltaTime trap).
                    float candidateEnd = Time.unscaledTime + duration;
                    if (candidateEnd > _pauseEndTimeUnscaled)
                        _pauseEndTimeUnscaled = candidateEnd;
                }
            }

            // Drive timeScale state. Done every frame regardless of session/cfg
            // so we always restore to 1 if disabled mid-pause.
            bool shouldPause = cfg != null && cfg.Enabled && Time.unscaledTime < _pauseEndTimeUnscaled;
            if (shouldPause)
            {
                if (!_pauseActive)
                {
                    _pauseActive = true;
                    Time.timeScale = Mathf.Clamp(cfg.PausedTimeScale, 0.01f, 1f);
                }
            }
            else if (_pauseActive)
            {
                _pauseActive = false;
                Time.timeScale = 1f;
            }
        }

        public void Dispose()
        {
            // Always release timeScale on shutdown so editor/scene-swap doesn't keep slow time.
            if (_pauseActive)
            {
                Time.timeScale = 1f;
                _pauseActive = false;
            }
        }

        static float ResolvePauseDuration(DevCheatsHitPauseSection cfg, bool isKill, bool isHeadshot, bool isRicochet)
        {
            // Priority: kill > headshot > ricochet > normal. Single hit = single category.
            if (isKill)     return cfg.KillDuration;
            if (isHeadshot) return cfg.HeadshotDuration;
            if (isRicochet) return cfg.RicochetDuration;
            return cfg.NormalDuration;
        }
    }
}
