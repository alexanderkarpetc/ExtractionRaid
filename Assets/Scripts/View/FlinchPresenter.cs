using System.Collections.Generic;
using Adapters;
using Dev;
using Session;
using State;
using UnityEngine;

namespace View
{
    /// <summary>
    /// Gunplay B.4 — Flinch / Stagger visual presenter. Listens до <see cref="RaidEventType.EntityHit"/>
    /// events, drives spine IK lean on character via spine + neck + head rotation overlay у LateUpdate.
    ///
    /// Lifecycle:
    /// <list type="number">
    /// <item>EntityHit fired by DamageSystem (per-target view feedback channel).</item>
    /// <item>FlinchPresenter looks up <see cref="CharacterBody"/> via BotPresenter, captures
    /// horizontal lean axis derived from shot direction relative to body's local frame.</item>
    /// <item>Per frame у LateUpdate, applies rotation overlay on Spine, Neck, Head bones з
    /// curve (ramp-up → hold → return). Multiplied onto whatever animator wrote so animation
    /// continues underneath.</item>
    /// <item>On expiration, clears the entry. Bones return до animator-written rest naturally.</item>
    /// </list>
    ///
    /// State-side stagger lockout (BotShootingSystem) is independent from this presenter — visual
    /// can be turned off via DevCheats.Stagger.Enabled toggle (lockout still works).
    /// </summary>
    public class FlinchPresenter
    {
        readonly Dictionary<EId, FlinchInstance> _active = new();
        readonly List<EId> _expired = new(4);

        public void LateTick(RaidSession session)
        {
            if (session == null) return;
            var cfg = DevCheats.Config?.Stagger;
            if (cfg == null || !cfg.Enabled)
            {
                if (_active.Count > 0) _active.Clear();
                return;
            }

            // Consume EntityHit events into new flinch instances.
            foreach (var e in session.ConsumeEvents().All)
            {
                if (e.Type != RaidEventType.EntityHit) continue;
                TryStartFlinch(e, cfg);
            }

            // Apply rotation overlay per active flinch. Done у LateUpdate so we run AFTER
            // animator wrote bone transforms — multiplying our lean rotation on top без
            // fighting the locomotion / pose.
            if (_active.Count == 0) return;

            float now = Time.unscaledTime;
            _expired.Clear();
            foreach (var kvp in _active)
            {
                var inst = kvp.Value;
                float elapsed = now - inst.StartUnscaledTime;
                float total   = cfg.RampUpTime + cfg.HoldTime + cfg.ReturnTime;
                if (elapsed >= total || inst.Spine == null)
                {
                    _expired.Add(kvp.Key);
                    continue;
                }

                float c = EvaluateCurve(elapsed, cfg.RampUpTime, cfg.HoldTime, cfg.ReturnTime);
                float angle = inst.PeakAngle * c;
                if (angle <= 0.001f) continue;

                ApplyLean(inst, angle, cfg);
            }

            for (int i = 0; i < _expired.Count; i++) _active.Remove(_expired[i]);
        }

        void TryStartFlinch(RaidEvent e, DevCheatsStaggerSection cfg)
        {
            var botPresenter = ApplicationCore.App.Instance?.BotPresenter;
            if (botPresenter == null) return;
            if (!botPresenter.TryGetCharacterBody(e.Id, out var body)) return;

            // EntityHit packs:
            //   CurrentHp = isHeadshot ? 1 : 0
            //   MaxHp     = isKill     ? 1 : 0
            //   Damage    = absorptionRatio
            bool isHeadshot = e.CurrentHp > 0.5f;
            bool isKill     = e.MaxHp     > 0.5f;
            if (isKill) return; // dying entity → ragdoll handles, don't fight it

            // Magnitude tier — нам бракує damage / MaxHp у event для "heavy" detection,
            // тому тут спрощуємо: light by default, headshot — strong. Heavy hit visual
            // різниця доходить через State stagger duration (AI lockout длиннее).
            float peakAngle = isHeadshot ? cfg.LeanAngleHeadshot : cfg.LeanAngleLight;

            // Lean axis у WORLD space: horizontal projection of shot direction crossed з up.
            // Rotation навколо це axis = body bends у напрямку bullet flight (head tilts
            // у same direction shot continues). World-space axis avoids bone-local frame
            // pitfalls (chibi bones have X along length → rotating navколо local X = twist,
            // not bend; що би виглядало як head turn sideways).
            var shotDir = e.Direction;
            shotDir.y = 0f;
            if (shotDir.sqrMagnitude < 0.0001f) return;
            shotDir.Normalize();

            var spine = body.SpineBone;
            var neck  = body.NeckBone;
            var head  = body.HeadBone;
            if (spine == null) return; // can't flinch without spine — skip

            var worldLeanAxis = Vector3.Cross(Vector3.up, shotDir).normalized;
            if (worldLeanAxis.sqrMagnitude < 0.0001f) worldLeanAxis = Vector3.right;

            _active[e.Id] = new FlinchInstance
            {
                Spine              = spine,
                Neck               = neck,
                Head               = head,
                WorldLeanAxis      = worldLeanAxis,
                StartUnscaledTime  = Time.unscaledTime,
                PeakAngle          = peakAngle,
            };
        }

        static void ApplyLean(FlinchInstance inst, float angle, DevCheatsStaggerSection cfg)
        {
            // World-space pre-multiplication: bone.rotation = leanRotation * bone.rotation.
            // Unity автоматично перерахує у localRotation з urahuvanniam parent chain. So lean
            // direction завжди читається консистентно у world space незалежно від rig orientation.
            if (inst.Spine != null && cfg.SpineLeanFraction > 0f)
            {
                var lean = Quaternion.AngleAxis(angle * cfg.SpineLeanFraction, inst.WorldLeanAxis);
                inst.Spine.rotation = lean * inst.Spine.rotation;
            }
            if (inst.Neck != null && cfg.NeckLeanFraction > 0f)
            {
                var lean = Quaternion.AngleAxis(angle * cfg.NeckLeanFraction, inst.WorldLeanAxis);
                inst.Neck.rotation = lean * inst.Neck.rotation;
            }
            if (inst.Head != null && cfg.HeadLeanFraction > 0f)
            {
                var lean = Quaternion.AngleAxis(angle * cfg.HeadLeanFraction, inst.WorldLeanAxis);
                inst.Head.rotation = lean * inst.Head.rotation;
            }
        }

        // Three-segment envelope: ramp 0→1, hold 1, return 1→0.
        static float EvaluateCurve(float t, float ramp, float hold, float ret)
        {
            if (t < ramp)            return ramp > 0f ? t / ramp : 1f;
            if (t < ramp + hold)     return 1f;
            float r = t - ramp - hold;
            return ret > 0f ? Mathf.Max(0f, 1f - r / ret) : 0f;
        }

        public void Dispose() { _active.Clear(); _expired.Clear(); }

        struct FlinchInstance
        {
            public Transform Spine, Neck, Head;
            public Vector3   WorldLeanAxis;
            public float     StartUnscaledTime;
            public float     PeakAngle;
        }
    }
}
