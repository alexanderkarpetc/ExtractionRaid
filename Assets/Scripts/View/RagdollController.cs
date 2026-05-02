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
    /// kinematic→dynamic on each bone Rigidbody, apply damping/drag, AddForceAtPosition
    /// impulse on nearest bone (chain transmits force через joints).</item>
    /// <item>Active phase: physics drives bones for <c>settleAfter</c> seconds.</item>
    /// <item>Settle phase: zero velocities + isKinematic = true → frozen pose.</item>
    /// <item>Lifetime expired: GameObject destroys self.</item>
    /// </list>
    /// </summary>
    public class RagdollController : MonoBehaviour
    {
        Rigidbody[]   _rbs;
        NavMeshAgent  _navMeshAgent;

        bool  _active;
        bool  _settled;
        float _activatedUnscaledTime;
        float _settleAfter;
        float _lifetime;

        public bool IsActive => _active;

        void Awake()
        {
            _rbs          = GetComponentsInChildren<Rigidbody>(true);
            _navMeshAgent = GetComponentInParent<NavMeshAgent>();
        }

        /// <summary>
        /// Switch to physics-driven ragdoll. Applies a dual impulse:
        ///   - Local (full magnitude) at hitPoint on the nearest bone — makes the struck
        ///     limb flop in the shot direction.
        ///   - Hips (magnitude × hipsImpulseScale) on the root Rigidbody — pushes the whole
        ///     body, so the corpse actually moves у напрямку shot, not just one limb flailing.
        /// Joint springs keep limbs from sagging instantly under gravity.
        /// </summary>
        public void Activate(Vector3 hitPoint, Vector3 impulseDirection, float impulseMagnitude,
                             float hipsImpulseScale,
                             float linearDamping, float angularDamping,
                             float jointSpringForce, float jointSpringDamper,
                             float headTwistLimit, float headSwingLimit,
                             float settleAfter, float lifetime)
        {
            if (_active) return;
            _active = true;
            _activatedUnscaledTime = Time.unscaledTime;
            _settleAfter = settleAfter;
            _lifetime    = lifetime;

            // Disable ALL animators у hierarchy. Без controller + destroyed component →
            // animator не може писати у bone transforms. Bones стають вільні для physics.
            var allAnimators = GetComponentsInChildren<Animator>(true);
            for (int i = 0; i < allAnimators.Length; i++)
            {
                var a = allAnimators[i];
                if (a == null) continue;
                a.runtimeAnimatorController = null;
                a.enabled = false;
                Destroy(a);
            }

            if (_navMeshAgent != null) _navMeshAgent.enabled = false;

            // Disable ALL other behaviours on the root GO — TwoBoneIK + CharacterBody (and
            // anything else) write to bone transforms у LateUpdate, fighting physics.
            // Only this controller stays alive.
            foreach (var mb in GetComponents<MonoBehaviour>())
            {
                if (mb == this) continue;
                mb.enabled = false;
            }

            // Switch all bones to physics + apply damping.
            for (int i = 0; i < _rbs.Length; i++)
            {
                var rb = _rbs[i];
                if (rb == null) continue;
                rb.isKinematic     = false;
                rb.useGravity      = true;
                rb.linearDamping   = linearDamping;
                rb.angularDamping  = angularDamping;
                rb.WakeUp();
            }

            // Joint stiffness pass:
            //   - Disable projection (snaps bones to anchor when constraint violated → can lock pose).
            //   - Add soft springs on swing/twist limits so limbs don't flap freely + sag instantly
            //     under gravity. Без spring, default joint = pure damper-less constraint inside the
            //     limit cone → arms/legs swing wildly then drop. Spring resists deviation from rest.
            var joints = GetComponentsInChildren<CharacterJoint>(true);
            var spring     = new SoftJointLimitSpring { spring = jointSpringForce,            damper = jointSpringDamper };
            // Stiff spring for head: soft global spring (10/2) lets a strong impulse blow
            // through the cone limits — head spins like exorcist. High spring ≈ rigid limit
            // enforcement: any deviation produces strong restoring force that snaps it back.
            var headSpring = new SoftJointLimitSpring { spring = jointSpringForce * 100f, damper = jointSpringDamper * 20f };
            int headJointsFound = 0;
            for (int i = 0; i < joints.Length; i++)
            {
                var j = joints[i];
                if (j == null) continue;
                j.enableProjection   = false;
                j.swingLimitSpring   = spring;
                j.twistLimitSpring   = spring;

                // Head-specific tightening: real necks barely twist + limited swing range.
                // Без цього — impulse on head spins it 360° like an exorcism. Hard spring on
                // head joint specifically; інші joints stay floppy so limbs still flop.
                if (j.name == "Head")
                {
                    headJointsFound++;
                    float halfTwist = headTwistLimit * 0.5f;
                    j.lowTwistLimit  = new SoftJointLimit { limit = -halfTwist,     bounciness = 0f };
                    j.highTwistLimit = new SoftJointLimit { limit =  halfTwist,     bounciness = 0f };
                    j.swing1Limit    = new SoftJointLimit { limit =  headSwingLimit, bounciness = 0f };
                    j.swing2Limit    = new SoftJointLimit { limit =  headSwingLimit, bounciness = 0f };
                    j.swingLimitSpring = headSpring;
                    j.twistLimitSpring = headSpring;
                }
            }
            if (headJointsFound == 0)
                Debug.LogWarning($"[Ragdoll] No 'Head' joint found on {name} — head limits skipped. Check bone naming.");

            // Dual-impulse model:
            //   1. Local impulse (full magnitude) at hitPoint on nearest bone → limb flop.
            //   2. Hips impulse (scaled) at hips center → whole-body push у напрямку shot.
            // Soft default joints don't transmit much force from a struck limb to Hips, so
            // without #2 the body barely reacts. Splitting magnitude lets us tune each ручкою.
            if (impulseMagnitude > 0f && impulseDirection.sqrMagnitude > 0.0001f)
            {
                var dir = impulseDirection.normalized;

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
                // Hips fallback: joint hierarchy roots its connectedBody chain at Hips, but if
                // naming differs, use the heaviest RB (Hips has the highest mass у setup).
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

                if (nearest != null)
                {
                    nearest.AddForceAtPosition(
                        dir * impulseMagnitude,
                        hitPoint,
                        ForceMode.Impulse);
                }
                if (hips != null && hipsImpulseScale > 0f)
                {
                    hips.AddForce(
                        dir * (impulseMagnitude * hipsImpulseScale),
                        ForceMode.Impulse);
                }
            }
        }

        void Update()
        {
            if (!_active) return;

            float elapsed = Time.unscaledTime - _activatedUnscaledTime;

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

            // Lifetime expired — destroy.
            if (elapsed >= _lifetime)
            {
                Destroy(gameObject);
            }
        }
    }
}
