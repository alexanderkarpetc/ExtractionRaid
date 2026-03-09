using State;
using UnityEngine;

namespace View
{
    public class BotView : MonoBehaviour, IDamageableView
    {
        [SerializeField] Transform _weaponPivot;

        string _currentWeaponPrefabId;
        GameObject _currentWeaponModel;
        WorldHealthBar _healthBar;
        BotDebugLabel _debugLabel;

        public EId EId { get; private set; }
        public string TypeId { get; private set; }

        public void Initialize(EId id, string typeId, string weaponPrefabId)
        {
            EId = id;
            TypeId = typeId;
            _healthBar = WorldHealthBar.Create(transform);
            _debugLabel = BotDebugLabel.Create(transform);

            if (!string.IsNullOrEmpty(weaponPrefabId))
                SwapWeaponModel(weaponPrefabId);
        }

        public void OnDamaged(float currentHp, float maxHp)
        {
            if (_healthBar != null)
                _healthBar.UpdateHealth(currentHp, maxHp);
        }

        // Gizmo data cached from state
        internal float GizmoVisionRange;
        internal float GizmoVisionAngle;
        internal bool GizmoHasTarget;
        internal Vector3 GizmoTargetPos;
        internal Vector3[] GizmoPatrolWaypoints;
        internal int GizmoPatrolIndex;

        public void SyncFromState(BotEntityState state, float currentHp, float maxHp)
        {
            transform.position = state.Position;

            if (state.FacingDirection.sqrMagnitude > 0.001f)
                transform.rotation = Quaternion.LookRotation(state.FacingDirection, Vector3.up);

            if (_weaponPivot != null && state.AimDirection.sqrMagnitude > 0.001f)
                _weaponPivot.rotation = Quaternion.LookRotation(state.AimDirection, Vector3.up);

            if (_debugLabel != null)
                _debugLabel.UpdateLabel(state, currentHp, maxHp);

            var bb = state.Blackboard;
            GizmoHasTarget = bb.HasTarget;
            GizmoTargetPos = bb.LastKnownTargetPos;
            GizmoPatrolWaypoints = bb.PatrolWaypoints;
            GizmoPatrolIndex = bb.PatrolWaypointIndex;
        }

#if UNITY_EDITOR
        void OnDrawGizmos()
        {
            var pos = transform.position + Vector3.up * 0.5f;
            var forward = transform.forward;

            DrawVisionCone(pos, forward);
            DrawTargetLine(pos);
            DrawPatrolPath();
        }

        void DrawVisionCone(Vector3 pos, Vector3 forward)
        {
            if (GizmoVisionRange <= 0f) return;

            Gizmos.color = new Color(1f, 1f, 0f, 0.3f);
            float halfAngle = GizmoVisionAngle * 0.5f;

            var leftDir = Quaternion.Euler(0f, -halfAngle, 0f) * forward;
            var rightDir = Quaternion.Euler(0f, halfAngle, 0f) * forward;

            Gizmos.DrawRay(pos, leftDir * GizmoVisionRange);
            Gizmos.DrawRay(pos, rightDir * GizmoVisionRange);

            int segments = 20;
            var prevPoint = pos + leftDir * GizmoVisionRange;
            for (int i = 1; i <= segments; i++)
            {
                float t = (float)i / segments;
                float angle = Mathf.Lerp(-halfAngle, halfAngle, t);
                var dir = Quaternion.Euler(0f, angle, 0f) * forward;
                var point = pos + dir * GizmoVisionRange;
                Gizmos.DrawLine(prevPoint, point);
                prevPoint = point;
            }
        }

        void DrawTargetLine(Vector3 pos)
        {
            if (!GizmoHasTarget) return;

            Gizmos.color = Color.red;
            Gizmos.DrawLine(pos, GizmoTargetPos + Vector3.up * 0.5f);
            Gizmos.DrawWireSphere(GizmoTargetPos + Vector3.up * 0.5f, 0.3f);
        }

        void DrawPatrolPath()
        {
            if (GizmoPatrolWaypoints == null || GizmoPatrolWaypoints.Length == 0) return;

            Gizmos.color = Color.green;
            for (int i = 0; i < GizmoPatrolWaypoints.Length; i++)
            {
                var wp = GizmoPatrolWaypoints[i];
                var next = GizmoPatrolWaypoints[(i + 1) % GizmoPatrolWaypoints.Length];
                Gizmos.DrawLine(wp + Vector3.up * 0.2f, next + Vector3.up * 0.2f);

                float sphereSize = (i == GizmoPatrolIndex) ? 0.5f : 0.2f;
                Gizmos.DrawWireSphere(wp + Vector3.up * 0.2f, sphereSize);
            }
        }
#endif

        void SwapWeaponModel(string prefabId)
        {
            if (_currentWeaponModel != null)
                Destroy(_currentWeaponModel);

            _currentWeaponPrefabId = prefabId;

            var prefab = Resources.Load<GameObject>("Prefabs/Weapons/" + prefabId);
            if (prefab == null) return;

            if (_weaponPivot == null) return;

            _currentWeaponModel = Instantiate(prefab, _weaponPivot);
            _currentWeaponModel.transform.localPosition = Vector3.zero;
            _currentWeaponModel.transform.localRotation = Quaternion.identity;
        }
    }
}
