using Adapters;
using Dev;
using Session;
using UnityEngine;

namespace View
{
    /// <summary>
    /// Gunplay A.9 — listens до <see cref="RaidEventType.EntityDied"/> and converts dying
    /// bot character body into a physics-driven ragdoll з directional impulse based on
    /// kill shot. Picks one of two profiles (headshot vs bodyshot) so kill type is
    /// readable from the death silhouette.
    ///
    /// Lifecycle:
    /// <list type="number">
    /// <item>Bot dies → DamageSystem emits <c>EntityDied</c> з hitPoint + projectile direction
    /// + damage + isHeadshot.</item>
    /// <item>RagdollPresenter requests body release via <see cref="BotPresenter.TryReleaseCharacterBody"/>.</item>
    /// <item>BotPresenter detaches body from shell, destroys shell, returns body GO.</item>
    /// <item>RagdollPresenter reparents body under <c>[RagdollPool]</c> root, attaches
    /// <see cref="RagdollController"/>, activates з profile-specific impulse + stagger.
    /// Controller manages own settle/lifetime/destroy.</item>
    /// </list>
    ///
    /// <para>Order matters у App.LateTick: this presenter must run BEFORE BotPresenter so
    /// it has chance to grab the body before BotDespawned destroys the shell.</para>
    /// </summary>
    public class RagdollPresenter
    {
        Transform _root;
        Transform _weaponDropRoot;

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

            // Bot's movement velocity is packed into the event by DamageSystem at death
            // time — RaidState.Bots list is cleared у Tick before LateTick runs, so
            // we can't query it directly. Inheriting it makes corpse continue forward
            // into the fall у напрямку bot was moving.
            Vector3 movementVelocity = e.Velocity;

            if (!botPresenter.TryReleaseCharacterBody(e.Id, out var bodyGo) || bodyGo == null)
                return;

            // Reparent under our pool root, preserving world pose.
            bodyGo.transform.SetParent(_root, worldPositionStays: true);

            // EntityDied packs isHeadshot у MaxHp (see RaidEventBuffer.EntityDied).
            bool isHeadshot = e.MaxHp > 0.5f;
            var profile = isHeadshot ? cfg.Headshot : cfg.Bodyshot;

            // Impulse direction: shot direction projected horizontal + profile-specific upward bias.
            var horizontalDir = e.Direction;
            horizontalDir.y = 0f;
            if (horizontalDir.sqrMagnitude < 0.0001f) horizontalDir = Vector3.forward;
            horizontalDir.Normalize();
            var impulseDir = (horizontalDir + Vector3.up * profile.UpwardImpulseBias).normalized;

            float impulseMag = Mathf.Clamp(
                e.Damage * profile.ImpulseScale,
                profile.MinImpulse,
                profile.MaxImpulse);

            // Detach + drop weapon before ragdoll activation. WeaponPivot lives як sibling
            // of skeleton у CharacterBody, тому без detach зброя зависає у повітрі коли
            // skeleton goes ragdoll. Drop physics-driven по shot direction + лет lift.
            TryDropWeapon(bodyGo, horizontalDir, impulseMag);

            var controller = bodyGo.AddComponent<RagdollController>();
            controller.Activate(new RagdollController.ActivateParams
            {
                HitPoint                = e.Position,
                ImpulseDirection        = impulseDir,
                ImpulseMagnitude        = impulseMag,
                HipsImpulseScale        = profile.HipsImpulseScale,
                StaggerDuration         = profile.StaggerDuration,
                StaggerSpringMultiplier = profile.StaggerSpringMultiplier,
                MovementVelocity        = movementVelocity,
                LinearDamping           = cfg.LinearDamping,
                AngularDamping          = cfg.AngularDamping,
                JointSpringForce        = cfg.JointSpringForce,
                JointSpringDamper       = cfg.JointSpringDamper,
                HeadTwistLimit          = cfg.HeadTwistLimit,
                HeadSwingLimit          = cfg.HeadSwingLimit,
                HipsMass                = cfg.HipsMass,
                HeadMass                = cfg.HeadMass,
                UpperArmMass            = cfg.UpperArmMass,
                DeathTwist                 = cfg.DeathTwist,
                DeathTumble                = cfg.DeathTumble,
                GroundImpactFloorY         = cfg.GroundImpactFloorY,
                GroundImpactSpeedThreshold = cfg.GroundImpactSpeedThreshold,
                GroundImpactLinearDamping  = cfg.GroundImpactLinearDamping,
                GroundImpactAngularDamping = cfg.GroundImpactAngularDamping,
                GroundImpactDuration       = cfg.GroundImpactDuration,
                SettleAfter             = cfg.SettleAfter,
                Lifetime                = cfg.Lifetime,
            });
        }

        void EnsureRoot()
        {
            if (_root != null) return;
            var go = new GameObject("[RagdollPool]");
            _root = go.transform;
        }

        void EnsureWeaponDropRoot()
        {
            if (_weaponDropRoot != null) return;
            var go = new GameObject("[WeaponDropPool]");
            _weaponDropRoot = go.transform;
        }

        void TryDropWeapon(GameObject bodyGo, Vector3 horizontalDir, float impulseMag)
        {
            var dropCfg = ViewCheats.Config?.WeaponDrop;
            if (dropCfg == null || !dropCfg.Enabled) return;

            var charBody = bodyGo.GetComponent<CharacterBody>();
            var pivot = charBody?.WeaponPivot;
            if (pivot == null || pivot.childCount == 0) return;

            EnsureWeaponDropRoot();

            // Detach weapon from skeleton hierarchy → reparent у pool, preserve world pose.
            var weaponGO = pivot.GetChild(0).gameObject;
            weaponGO.transform.SetParent(_weaponDropRoot, worldPositionStays: true);

            // Disable any animator/IK behaviour on the weapon root that could fight physics.
            foreach (var mb in weaponGO.GetComponents<MonoBehaviour>())
                mb.enabled = false;
            foreach (var anim in weaponGO.GetComponentsInChildren<Animator>(true))
                anim.enabled = false;

            // Add Rigidbody + optional collider so weapon physics-falls naturally.
            var rb = weaponGO.AddComponent<Rigidbody>();
            rb.mass            = dropCfg.Mass;
            rb.linearDamping   = dropCfg.LinearDamping;
            rb.angularDamping  = dropCfg.AngularDamping;
            rb.useGravity      = true;

            if (dropCfg.AddCollider)
            {
                var col = weaponGO.AddComponent<BoxCollider>();
                col.size = dropCfg.ColliderHalfSize * 2f;
            }

            // Impulse: shot direction × scale, plus upward bias so weapon arcs out of hand.
            var dropDir = horizontalDir.sqrMagnitude > 0.0001f
                ? (horizontalDir.normalized + Vector3.up * dropCfg.UpwardImpulseBias).normalized
                : Vector3.up;
            rb.AddForce(dropDir * (impulseMag * dropCfg.ImpulseScale), ForceMode.Impulse);

            // Random tumble — weapon spins randomly як falls (looks natural).
            if (dropCfg.TorqueScale > 0f)
                rb.AddTorque(Random.insideUnitSphere * dropCfg.TorqueScale, ForceMode.Impulse);

            Object.Destroy(weaponGO, dropCfg.Lifetime);
        }

        public void Dispose()
        {
            if (_root != null)
            {
                Object.Destroy(_root.gameObject);
                _root = null;
            }
            if (_weaponDropRoot != null)
            {
                Object.Destroy(_weaponDropRoot.gameObject);
                _weaponDropRoot = null;
            }
        }
    }
}
