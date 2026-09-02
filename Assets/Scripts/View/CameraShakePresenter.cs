using Adapters;
using Dev;
using Session;
using UnityEngine;

namespace View
{
    /// <summary>
    /// Gunplay A.3 — listens to raid events and dispatches kick/tremor calls to
    /// <see cref="CameraShake"/>. Keeps the MonoBehaviour focused on math; presenter
    /// owns event-routing concerns + DevCheats configuration lookup.
    ///
    /// Triggers:
    /// <list type="bullet">
    /// <item><b>WeaponFired</b> → directional kick along weapon direction +
    /// fire tremor. Magnitude scales з <c>FireRecoilStatScale</c> (we don't have
    /// the firing weapon stats in the event payload — magnitude derives from
    /// section's base values; per-archetype scaling layered later if needed).</item>
    /// <item><b>EntityDamaged</b> on player entity → omni tremor scaled by HP delta.</item>
    /// </list>
    /// </summary>
    public class CameraShakePresenter
    {
        CameraShake _shake;
        float       _lastPlayerHp = -1f;

        public void SetTarget(CameraShake shake) => _shake = shake;

        public void LateTick(RaidSession session)
        {
            if (session == null || _shake == null) return;
            var cfg = ViewCheats.Config?.CameraShake;
            if (cfg == null || !cfg.Enabled) return;

            float scale = cfg.GlobalScale;
            if (scale <= 0f) return;

            var player = session.RaidState.PlayerEntity;

            foreach (var e in session.ConsumeEvents().All)
            {
                switch (e.Type)
                {
                    case RaidEventType.WeaponFired when player != null && e.Id == player.Id:
                        // RaidEventBuffer.WeaponFired packs:
                        //   Position        = origin
                        //   Direction       = fire direction
                        //   StringPayload   = "Ballistic"/"Laser"
                        //   DeliveryPattern = Single/Auto/Scatter
                        // A1 — per-archetype path composes shape × modifier; fallback to legacy
                        // FireKick*/FireTremor* fields if disabled.
                        if (cfg.PerArchetypeEnabled)
                        {
                            var r = ArchetypeShakeResolver.Resolve(cfg, e.StringPayload, e.DeliveryPattern);
                            var kickDir = (-e.Direction + r.KickDirOffset).normalized;
                            _shake.Kick(
                                direction:        kickDir,
                                magnitude:        r.KickMagnitude * scale,
                                durationUnscaled: r.KickDuration);
                            _shake.Tremor(
                                magnitude:        r.TremorMagnitude * scale,
                                durationUnscaled: r.TremorDuration,
                                frequency:        r.TremorFrequency);
                        }
                        else
                        {
                            // Legacy single-profile path. Kick pushes camera AGAINST shot direction.
                            _shake.Kick(
                                direction:        -e.Direction,
                                magnitude:        cfg.FireKickMagnitude * scale,
                                durationUnscaled: cfg.FireKickDuration);
                            _shake.Tremor(
                                magnitude:        cfg.FireTremorMagnitude * scale,
                                durationUnscaled: cfg.FireTremorDuration,
                                frequency:        cfg.TremorFrequency);
                        }
                        break;

                    case RaidEventType.EntityDamaged:
                        if (player != null && e.Id == player.Id)
                        {
                            float hpDelta = ResolveHpDelta(e.CurrentHp, e.MaxHp);
                            if (hpDelta <= 0f) break;
                            float magnitude = (cfg.DamageTremorMagnitude
                                               + cfg.DamageTremorPerHp * hpDelta) * scale;
                            _shake.Tremor(
                                magnitude:        magnitude,
                                durationUnscaled: cfg.DamageTremorDuration,
                                frequency:        cfg.TremorFrequency);
                        }
                        break;
                }
            }
        }

        // We don't get explicit damage amount from EntityDamaged event (only currentHp/maxHp),
        // so we infer the delta from the previously-tracked HP. First damage tick uses MaxHp
        // as fallback estimate (means: first hit has slightly more shake, which feels right
        // since first hit IS more punctuating).
        float ResolveHpDelta(float currentHp, float maxHp)
        {
            float delta;
            if (_lastPlayerHp >= 0f)
                delta = Mathf.Max(0f, _lastPlayerHp - currentHp);
            else
                delta = Mathf.Max(0f, maxHp - currentHp);

            _lastPlayerHp = currentHp;
            return delta;
        }

        public void Dispose()
        {
            _shake = null;
        }
    }
}
