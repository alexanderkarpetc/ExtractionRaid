using Adapters;
using Dev;
using Session;
using UnityEngine;

namespace View
{
    /// <summary>
    /// Gunplay A.9 — listens до <see cref="RaidEventType.EntityDied"/> and converts dying
    /// bot character body into a physics-driven ragdoll з directional impulse based on
    /// kill shot.
    ///
    /// Lifecycle:
    /// <list type="number">
    /// <item>Bot dies → DamageSystem emits <c>EntityDied</c> з hitPoint + projectile direction + damage.</item>
    /// <item>RagdollPresenter requests body release via <see cref="BotPresenter.TryReleaseCharacterBody"/>.</item>
    /// <item>BotPresenter detaches body from shell, destroys shell, returns body GO.</item>
    /// <item>RagdollPresenter reparents body under <c>[RagdollPool]</c> root, attaches
    /// <see cref="RagdollController"/>, activates з impulse. Controller manages own
    /// settle/lifetime/destroy.</item>
    /// </list>
    ///
    /// <para>Order matters у App.LateTick: this presenter must run BEFORE BotPresenter so
    /// it has chance to grab the body before BotDespawned destroys the shell. Currently
    /// achieved by registering RagdollPresenter immediately ahead of BotPresenter.</para>
    /// </summary>
    public class RagdollPresenter
    {
        Transform _root;

        public void LateTick(RaidSession session)
        {
            if (session == null) return;
            var cfg = ViewCheats.Config?.Ragdoll;
            if (cfg == null || !cfg.Enabled) return;

            EnsureRoot();

            foreach (var e in session.ConsumeEvents().All)
            {
                if (e.Type != RaidEventType.EntityDied) continue;
                ProcessDeath(e, cfg);
            }
        }

        void ProcessDeath(RaidEvent e, ViewCheatsRagdollSection cfg)
        {
            var botPresenter = ApplicationCore.App.Instance?.BotPresenter;
            if (botPresenter == null) return;
            if (!botPresenter.TryReleaseCharacterBody(e.Id, out var bodyGo) || bodyGo == null)
                return;

            // Reparent under our pool root, preserving world pose.
            bodyGo.transform.SetParent(_root, worldPositionStays: true);

            // Compute impulse direction: hit direction projected horizontal + slight upward bias.
            var horizontalDir = e.Direction;
            horizontalDir.y = 0f;
            if (horizontalDir.sqrMagnitude < 0.0001f) horizontalDir = Vector3.forward;
            horizontalDir.Normalize();
            var impulseDir = (horizontalDir + Vector3.up * cfg.UpwardImpulseBias).normalized;

            // Final magnitude = clamp(damage × scale, min, max). Cap prevents extreme
            // damage values (explosive hits, crit headshots) from sending body flying.
            float impulseMag = Mathf.Clamp(
                e.Damage * cfg.ImpulseScale,
                cfg.MinImpulse,
                cfg.MaxImpulse);

            var controller = bodyGo.AddComponent<RagdollController>();
            controller.Activate(
                hitPoint:          e.Position,
                impulseDirection:  impulseDir,
                impulseMagnitude:  impulseMag,
                hipsImpulseScale:  cfg.HipsImpulseScale,
                linearDamping:     cfg.LinearDamping,
                angularDamping:    cfg.AngularDamping,
                jointSpringForce:  cfg.JointSpringForce,
                jointSpringDamper: cfg.JointSpringDamper,
                headTwistLimit:    cfg.HeadTwistLimit,
                headSwingLimit:    cfg.HeadSwingLimit,
                settleAfter:       cfg.SettleAfter,
                lifetime:          cfg.Lifetime);
        }

        void EnsureRoot()
        {
            if (_root != null) return;
            var go = new GameObject("[RagdollPool]");
            _root = go.transform;
        }

        public void Dispose()
        {
            if (_root != null)
            {
                Object.Destroy(_root.gameObject);
                _root = null;
            }
        }
    }
}
