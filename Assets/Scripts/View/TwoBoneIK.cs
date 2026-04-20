using Dev;
using UnityEngine;

namespace View
{
    /// <summary>
    /// Analytic 2-bone IK. Place on character; assign root/mid/end bones (shoulder→forearm→hand)
    /// and a pole hint (elbow direction). Call SetTarget at runtime to drive the chain.
    ///
    /// Execution order 3000 → runs AFTER CharacterBody (order 2000) writes WeaponPivot recoil,
    /// so the hand tracks the current weapon position with zero frame lag.
    /// </summary>
    [DefaultExecutionOrder(3000)]
    public class TwoBoneIK : MonoBehaviour
    {
        [SerializeField] Transform _root;         // shoulder / upper arm
        [SerializeField] Transform _mid;          // forearm
        [SerializeField] Transform _end;          // hand (leaf)
        [SerializeField] Transform _poleHint;     // elbow bend direction hint
        [SerializeField] bool _matchEndRotation = true; // align hand rotation to target
        [Range(0f, 1f)] [SerializeField] float _weight = 1f;

        Transform _target;

        public void SetTarget(Transform target) => _target = target;

        void LateUpdate()
        {
            if (_root == null || _mid == null || _end == null) return;
            if (!DevCheats.Config.Player.HandIKEnabled) return;

            float weight = _weight * DevCheats.Config.Player.HandIKWeight;
            if (_target == null || weight <= 0f) return;

            var origRootRot = _root.rotation;
            var origMidRot = _mid.rotation;
            var origEndRot = _end.rotation;

            Vector3 polePos = _poleHint != null
                ? _poleHint.position
                : _mid.position - _root.forward; // fallback: behind the shoulder

            Solve(_root, _mid, _end, _target.position, polePos);

            if (_matchEndRotation)
                _end.rotation = _target.rotation;

            if (weight < 1f)
            {
                _root.rotation = Quaternion.Slerp(origRootRot, _root.rotation, weight);
                _mid.rotation = Quaternion.Slerp(origMidRot, _mid.rotation, weight);
                _end.rotation = Quaternion.Slerp(origEndRot, _end.rotation, weight);
            }
        }

        static void Solve(Transform root, Transform mid, Transform end,
                          Vector3 targetPos, Vector3 polePos)
        {
            Vector3 rootPos = root.position;
            float lenRM = Vector3.Distance(rootPos, mid.position);
            float lenME = Vector3.Distance(mid.position, end.position);

            Vector3 toTarget = targetPos - rootPos;
            float dist = Mathf.Clamp(toTarget.magnitude, 0.001f, lenRM + lenME - 0.001f);
            Vector3 toTargetDir = toTarget.normalized;
            Vector3 actualTarget = rootPos + toTargetDir * dist;

            // Law of cosines: interior angle at root in triangle root-mid-target.
            float cosA = Mathf.Clamp((lenRM * lenRM + dist * dist - lenME * lenME) / (2f * lenRM * dist), -1f, 1f);
            float sinA = Mathf.Sqrt(Mathf.Max(0f, 1f - cosA * cosA));

            // Bend direction = pole projected onto plane perpendicular to root→target.
            Vector3 toPole = polePos - rootPos;
            Vector3 bendDir = Vector3.ProjectOnPlane(toPole, toTargetDir);
            if (bendDir.sqrMagnitude < 1e-6f)
            {
                bendDir = Vector3.Cross(toTargetDir, Vector3.up);
                if (bendDir.sqrMagnitude < 1e-6f)
                    bendDir = Vector3.Cross(toTargetDir, Vector3.right);
            }
            bendDir.Normalize();

            Vector3 newMidPos = rootPos + toTargetDir * (lenRM * cosA) + bendDir * (lenRM * sinA);

            // Rotate root so mid lands on newMidPos.
            Vector3 oldMidDir = (mid.position - rootPos).normalized;
            Vector3 newMidDir = (newMidPos - rootPos).normalized;
            root.rotation = Quaternion.FromToRotation(oldMidDir, newMidDir) * root.rotation;

            // Rotate mid so end lands on actualTarget.
            Vector3 oldEndDir = (end.position - mid.position).normalized;
            Vector3 newEndDir = (actualTarget - mid.position).normalized;
            mid.rotation = Quaternion.FromToRotation(oldEndDir, newEndDir) * mid.rotation;
        }

#if UNITY_EDITOR
        void OnDrawGizmosSelected()
        {
            if (_root == null || _mid == null || _end == null) return;

            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(_root.position, _mid.position);
            Gizmos.DrawLine(_mid.position, _end.position);
            Gizmos.DrawWireSphere(_root.position, 0.03f);
            Gizmos.DrawWireSphere(_mid.position, 0.03f);
            Gizmos.DrawWireSphere(_end.position, 0.03f);

            if (_poleHint != null)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawWireSphere(_poleHint.position, 0.05f);
                Gizmos.DrawLine(_mid.position, _poleHint.position);
            }

            if (_target != null)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawWireSphere(_target.position, 0.04f);
                Gizmos.DrawLine(_end.position, _target.position);
            }
        }
#endif
    }
}
