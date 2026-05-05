using UnityEngine;
using UnityEngine.AI;

namespace View
{
    /// <summary>
    /// Gunplay A.9 — Ragdoll runtime controller. Added at activation time by
    /// <see cref="RagdollPresenter"/> on a character body that has pre-authored Rigidbody +
    /// Collider + CharacterJoint chain (built by <c>RagdollSetupUtility</c>).
    ///
    /// Lifecycle:
    /// <list type="number">
    /// <item><see cref="Activate"/> — disable Animator + NavMeshAgent + other root behaviours,
    /// kinematic→dynamic on each bone Rigidbody, override masses, apply damping/drag,
    /// set stiff joint springs (stagger), AddForceAtPosition impulse.</item>
    /// <item>Stagger phase: joint springs ramp stiff → base over <c>staggerDuration</c>.
    /// Body "fights" gravity briefly (bodyshot) or skips entirely (headshot).</item>
    /// <item>Active phase: physics drives bones until <c>settleAfter</c>.</item>
    /// <item>Settle phase: zero velocities + isKinematic = true → frozen pose.</item>
    /// <item>Lifetime expired: GameObject destroys self.</item>
    /// </list>
    /// </summary>
    public class RagdollController : MonoBehaviour
    {
        Rigidbody[]        _rbs;
        CharacterJoint[]   _joints;
        NavMeshAgent       _navMeshAgent;

        bool  _active;
        bool  _settled;
        float _activatedUnscaledTime;
        float _settleAfter;
        float _lifetime;

        // Stagger ramp state
        float _staggerDuration;
        float _baseSpringForce;
        float _baseSpringDamper;
        float _peakSpringForce;
        float _peakSpringDamper;
        bool  _staggerActive;

        // Cached "Head" joint reference for per-frame stagger spring update без name compares.
        CharacterJoint _headJoint;
        float          _headSpringForce;
        float          _headSpringDamper;

        // Ground impact state — bone-touched-floor event triggers temporary damping boost.
        bool  _groundImpactArmed;
        bool  _groundImpactActive;
        float _groundImpactDuration;
        float _groundImpactEndUnscaledTime;
        float _groundImpactFloorY;
        float _groundImpactSpeedSqr;
        float _baseLinearDamping;
        float _baseAngularDamping;
        float _impactLinearDamping;
        float _impactAngularDamping;

        public bool IsActive => _active;

        void Awake()
        {
            _rbs          = GetComponentsInChildren<Rigidbody>(true);
            _joints       = GetComponentsInChildren<CharacterJoint>(true);
            _navMeshAgent = GetComponentInParent<NavMeshAgent>();
        }

        /// <summary>
        /// Switch to physics-driven ragdoll з two-profile support. Caller picks profile based
        /// on hit zone (headshot vs bodyshot). Profile params: impulse split, stagger, etc.
        /// </summary>
        public void Activate(in ActivateParams p)
        {
            if (_active) return;
            _active = true;
            _activatedUnscaledTime = Time.unscaledTime;
            _settleAfter           = p.SettleAfter;
            _lifetime              = p.Lifetime;
            _staggerDuration       = p.StaggerDuration;
            _baseSpringForce       = p.JointSpringForce;
            _baseSpringDamper      = p.JointSpringDamper;
            _peakSpringForce       = p.JointSpringForce * Mathf.Max(1f, p.StaggerSpringMultiplier);
            _peakSpringDamper      = p.JointSpringDamper * Mathf.Max(1f, p.StaggerSpringMultiplier * 0.2f);
            _staggerActive         = p.StaggerDuration > 0.001f && p.StaggerSpringMultiplier > 1f;

            // Cache ground impact params + arm one-shot trigger.
            _baseLinearDamping             = p.LinearDamping;
            _baseAngularDamping            = p.AngularDamping;
            _impactLinearDamping           = p.GroundImpactLinearDamping;
            _impactAngularDamping          = p.GroundImpactAngularDamping;
            _groundImpactFloorY            = p.GroundImpactFloorY;
            _groundImpactSpeedSqr          = p.GroundImpactSpeedThreshold * p.GroundImpactSpeedThreshold;
            _groundImpactDuration          = p.GroundImpactDuration;
            _groundImpactArmed             = p.GroundImpactDuration > 0.001f
                                          && p.GroundImpactLinearDamping > p.LinearDamping;
            _groundImpactActive            = false;

            // ── Capture current bone pose BEFORE killing animator ─────────
            // Setting Animator.runtimeAnimatorController = null on Humanoid rig snaps
            // bones to bind pose (T-pose) — documented Unity behaviour. We restore the
            // captured pose immediately after, so physics inherits actual death pose
            // instead of T-pose попри snap.
            var captured = new BonePose[_rbs.Length];
            for (int i = 0; i < _rbs.Length; i++)
            {
                if (_rbs[i] == null) continue;
                var t = _rbs[i].transform;
                captured[i] = new BonePose
                {
                    Transform     = t,
                    LocalRotation = t.localRotation,
                    LocalPosition = t.localPosition,
                };
            }

            // Disable ALL animators — без controller + destroyed component → animator
            // не може писати у bone transforms. Bones стають вільні для physics.
            var allAnimators = GetComponentsInChildren<Animator>(true);
            for (int i = 0; i < allAnimators.Length; i++)
            {
                var a = allAnimators[i];
                if (a == null) continue;
                a.runtimeAnimatorController = null;
                a.enabled = false;
                Destroy(a);
            }

            // Restore captured pose — undoes any T-pose snap caused by нulling controller.
            for (int i = 0; i < captured.Length; i++)
            {
                var b = captured[i];
                if (b.Transform == null) continue;
                b.Transform.localRotation = b.LocalRotation;
                b.Transform.localPosition = b.LocalPosition;
            }

            if (_navMeshAgent != null) _navMeshAgent.enabled = false;

            // Disable ALL other behaviours on the root GO — TwoBoneIK + CharacterBody (and
            // anything else) write to bone transforms у LateUpdate, fighting physics.
            // CharacterHitFx survives — it pushes per-bone decal positions to MPB, which
            // physics-driven bones now update; without it decals freeze in world space.
            foreach (var mb in GetComponents<MonoBehaviour>())
            {
                if (mb == this) continue;
                if (mb is CharacterHitFx) continue;
                mb.enabled = false;
            }

            // Switch all bones to physics + apply damping + mass overrides + inherited velocity.
            // Mass distribution drives fall feel: heavy hips = stable knockback center,
            // light head + arms = trail naturally instead of leading the fall.
            // Movement velocity inheritance: bots use kinematic translation (NavMeshAgent
            // or BotMovementSystem writes Transform), so RBs have zero linearVelocity
            // when switched to dynamic. Without inheritance, a running bot would just
            // stop and drop straight down. Pre-seeding linearVelocity preserves momentum
            // so the corpse continues into the fall у напрямку bot was moving.
            for (int i = 0; i < _rbs.Length; i++)
            {
                var rb = _rbs[i];
                if (rb == null) continue;
                rb.isKinematic     = false;
                rb.useGravity      = true;
                rb.linearDamping   = p.LinearDamping;
                rb.angularDamping  = p.AngularDamping;
                rb.mass            = ResolveMass(rb.name, rb.mass, p);
                rb.linearVelocity  = p.MovementVelocity;
                rb.WakeUp();
            }

            // Joint pass: rest spring (will be boosted у Update under stagger), head limits.
            ApplyJointSprings(_baseSpringForce, _baseSpringDamper);
            ApplyHeadJointLimits(p.HeadTwistLimit, p.HeadSwingLimit);

            // Dual impulse: nearest bone (limb flop) + Hips (whole-body push, profile-scaled).
            ApplyImpulse(p.HitPoint, p.ImpulseDirection, p.ImpulseMagnitude, p.HipsImpulseScale,
                         p.DeathTwist, p.DeathTumble);
        }

        static float ResolveMass(string boneName, float currentMass, in ActivateParams p)
        {
            switch (boneName)
            {
                case "Hips":      return p.HipsMass;
                case "Head":      return p.HeadMass;
                case "UpperArm_L":
                case "UpperArm_R": return p.UpperArmMass;
                default:          return currentMass;
            }
        }

        void ApplyJointSprings(float springForce, float springDamper)
        {
            var spring     = new SoftJointLimitSpring { spring = springForce,         damper = springDamper };
            // Head spring uses much higher constant (×100/×20) regardless of stagger because
            // soft head spring lets head spin like exorcist під strong impulse. Always rigid.
            var headSpring = new SoftJointLimitSpring { spring = springForce * 100f,  damper = springDamper * 20f };

            for (int i = 0; i < _joints.Length; i++)
            {
                var j = _joints[i];
                if (j == null) continue;
                j.enableProjection = false;

                if (j.name == "Head")
                {
                    j.swingLimitSpring = headSpring;
                    j.twistLimitSpring = headSpring;
                    _headJoint         = j;
                    _headSpringForce   = headSpring.spring;
                    _headSpringDamper  = headSpring.damper;
                }
                else
                {
                    j.swingLimitSpring = spring;
                    j.twistLimitSpring = spring;
                }
            }
        }

        void ApplyHeadJointLimits(float twistLimit, float swingLimit)
        {
            int found = 0;
            float halfTwist = twistLimit * 0.5f;
            for (int i = 0; i < _joints.Length; i++)
            {
                var j = _joints[i];
                if (j == null || j.name != "Head") continue;
                found++;
                j.lowTwistLimit  = new SoftJointLimit { limit = -halfTwist, bounciness = 0f };
                j.highTwistLimit = new SoftJointLimit { limit =  halfTwist, bounciness = 0f };
                j.swing1Limit    = new SoftJointLimit { limit =  swingLimit, bounciness = 0f };
                j.swing2Limit    = new SoftJointLimit { limit =  swingLimit, bounciness = 0f };
            }
            if (found == 0)
                Debug.LogWarning($"[Ragdoll] No 'Head' joint found on {name} — head limits skipped.");
        }

        void ApplyImpulse(Vector3 hitPoint, Vector3 impulseDirection, float impulseMagnitude,
                          float hipsImpulseScale, float deathTwist, float deathTumble)
        {
            var dir = impulseDirection.sqrMagnitude > 0.0001f ? impulseDirection.normalized : Vector3.zero;
            bool applyImpulse = impulseMagnitude > 0f && dir != Vector3.zero;

            Rigidbody nearest = null;
            Rigidbody hips    = null;
            float bestSqr = float.MaxValue;
            for (int i = 0; i < _rbs.Length; i++)
            {
                var rb = _rbs[i];
                if (rb == null) continue;

                if (rb.name == "Hips") hips = rb;

                float sqr = (rb.position - hitPoint).sqrMagnitude;
                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                    nearest = rb;
                }
            }
            if (hips == null)
            {
                float maxMass = 0f;
                for (int i = 0; i < _rbs.Length; i++)
                {
                    var rb = _rbs[i];
                    if (rb == null) continue;
                    if (rb.mass > maxMass) { maxMass = rb.mass; hips = rb; }
                }
            }

            if (applyImpulse && nearest != null)
                nearest.AddForceAtPosition(dir * impulseMagnitude, hitPoint, ForceMode.Impulse);
            if (applyImpulse && hips != null && hipsImpulseScale > 0f)
                hips.AddForce(dir * (impulseMagnitude * hipsImpulseScale), ForceMode.Impulse);

            // Random torque on Hips: твist (around Y, vertical) + tumble (around X/Z).
            // Each death looks різна — body spins/tilts unpredictably as it falls. Без
            // цього всі smерті look identical (predictable, "sack of potatoes" fall).
            if (hips != null && (deathTwist > 0f || deathTumble > 0f))
            {
                var torque = new Vector3(
                    Random.Range(-1f, 1f) * deathTumble,
                    Random.Range(-1f, 1f) * deathTwist,
                    Random.Range(-1f, 1f) * deathTumble);
                hips.AddTorque(torque, ForceMode.Impulse);
            }
        }

        void Update()
        {
            if (!_active) return;

            float elapsed = Time.unscaledTime - _activatedUnscaledTime;

            // Stagger ramp: linear from peak → base over staggerDuration. Once t >= 1 → springs
            // settled, controller stops touching them (cheap idle path).
            if (_staggerActive)
            {
                float t = _staggerDuration > 0f ? Mathf.Clamp01(elapsed / _staggerDuration) : 1f;
                float force  = Mathf.Lerp(_peakSpringForce,  _baseSpringForce,  t);
                float damper = Mathf.Lerp(_peakSpringDamper, _baseSpringDamper, t);
                ApplyJointSpringsRamp(force, damper);

                if (t >= 1f) _staggerActive = false;
            }

            // Ground impact: detect first bone-touches-floor event. Once any bone descends
            // below floorY while moving fast, bump damping for groundImpactDuration → energy
            // dissipates → body settles solidly без sliding/bouncing. One-shot (armed=false
            // після trigger), потім reverts до base damping коли timer expires.
            if (_groundImpactArmed && !_groundImpactActive)
            {
                for (int i = 0; i < _rbs.Length; i++)
                {
                    var rb = _rbs[i];
                    if (rb == null) continue;
                    if (rb.position.y < _groundImpactFloorY
                        && rb.linearVelocity.sqrMagnitude > _groundImpactSpeedSqr)
                    {
                        _groundImpactActive          = true;
                        _groundImpactArmed           = false;
                        _groundImpactEndUnscaledTime = Time.unscaledTime + _groundImpactDuration;
                        ApplyDamping(_impactLinearDamping, _impactAngularDamping);
                        break;
                    }
                }
            }
            else if (_groundImpactActive && Time.unscaledTime >= _groundImpactEndUnscaledTime)
            {
                _groundImpactActive = false;
                ApplyDamping(_baseLinearDamping, _baseAngularDamping);
            }

            // Settle phase: zero velocity + freeze kinematic.
            if (!_settled && elapsed >= _settleAfter)
            {
                for (int i = 0; i < _rbs.Length; i++)
                {
                    var rb = _rbs[i];
                    if (rb == null) continue;
                    rb.linearVelocity  = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                    rb.isKinematic     = true;
                }
                _settled = true;
            }

            if (elapsed >= _lifetime)
                Destroy(gameObject);
        }

        void ApplyDamping(float linearDamping, float angularDamping)
        {
            for (int i = 0; i < _rbs.Length; i++)
            {
                var rb = _rbs[i];
                if (rb == null) continue;
                rb.linearDamping  = linearDamping;
                rb.angularDamping = angularDamping;
            }
        }

        void ApplyJointSpringsRamp(float springForce, float springDamper)
        {
            // Hot-path version: skips Head joint (kept rigid), reuses spring struct.
            var spring = new SoftJointLimitSpring { spring = springForce, damper = springDamper };
            for (int i = 0; i < _joints.Length; i++)
            {
                var j = _joints[i];
                if (j == null || j == _headJoint) continue;
                j.swingLimitSpring = spring;
                j.twistLimitSpring = spring;
            }
        }

        struct BonePose
        {
            public Transform  Transform;
            public Quaternion LocalRotation;
            public Vector3    LocalPosition;
        }

        public struct ActivateParams
        {
            public Vector3 HitPoint;
            public Vector3 ImpulseDirection;
            public float   ImpulseMagnitude;
            public float   HipsImpulseScale;
            public float   StaggerDuration;
            public float   StaggerSpringMultiplier;
            public Vector3 MovementVelocity;
            public float   LinearDamping;
            public float   AngularDamping;
            public float   JointSpringForce;
            public float   JointSpringDamper;
            public float   HeadTwistLimit;
            public float   HeadSwingLimit;
            public float   HipsMass;
            public float   HeadMass;
            public float   UpperArmMass;
            public float   DeathTwist;
            public float   DeathTumble;
            public float   GroundImpactFloorY;
            public float   GroundImpactSpeedThreshold;
            public float   GroundImpactLinearDamping;
            public float   GroundImpactAngularDamping;
            public float   GroundImpactDuration;
            public float   SettleAfter;
            public float   Lifetime;
        }
    }
}
