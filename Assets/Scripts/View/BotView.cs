using Constants;
using Dev;
using State;
using UnityEngine;

namespace View
{
    public class BotView : MonoBehaviour, IDamageableView
    {
        [SerializeField] Transform _weaponPivot;
        [SerializeField] Transform _helmetSlot;  // assign Helmet01 bone in prefab
        [SerializeField] Transform _armorSlot;   // assign Spine02 bone in prefab
        [SerializeField] Transform _capsuleVisual;

        string _currentWeaponPrefabId;
        GameObject _currentWeaponModel;
        string _currentHelmetPrefabId;
        GameObject _currentHelmetModel;
        string _currentArmorPrefabId;
        GameObject _currentArmorModel;
        WorldHealthBar _healthBar;
        BotDebugLabel _debugLabel;
        float _rollVisualAngle;

        public EId EId { get; private set; }
        public string TypeId { get; private set; }

        public void Initialize(EId id, string typeId, string weaponPrefabId, float maxHp)
        {
            EId = id;
            TypeId = typeId;
            _healthBar = WorldHealthBar.Create(transform, maxHp);
            _debugLabel = BotDebugLabel.Create(transform);

            if (!string.IsNullOrEmpty(weaponPrefabId))
                SwapWeaponModel(weaponPrefabId);
        }

        public void OnDamaged(float currentHp, float maxHp)
        {
            if (_healthBar != null)
                _healthBar.UpdateHealth(currentHp, maxHp);
        }

        public void UpdateArmor(float helmetDurPercent, float vestDurPercent)
        {
            if (_healthBar != null)
                _healthBar.UpdateArmor(helmetDurPercent, vestDurPercent);
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
            // FOV visibility toggle
            bool shouldShow = state.IsVisibleToPlayer || !DevCheats.FOVEnabled || DevCheats.ForceShowAllBots;
            foreach (var r in GetComponentsInChildren<Renderer>(true))
                r.enabled = shouldShow;

            transform.position = state.Position;

            if (state.FacingDirection.sqrMagnitude > 0.001f)
                transform.rotation = Quaternion.LookRotation(state.FacingDirection, Vector3.up);

            if (_weaponPivot != null && state.AimDirection.sqrMagnitude > 0.001f)
                _weaponPivot.rotation = Quaternion.LookRotation(state.AimDirection, Vector3.up);

            SyncRollVisual(state);

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

        // ── Armor visual attachment ─────────────────────────

        public void SwapHelmetModel(string prefabId)
        {
            if (prefabId == _currentHelmetPrefabId) return;
            ClearHelmetModel();
            _currentHelmetPrefabId = prefabId;
            if (string.IsNullOrEmpty(prefabId) || _helmetSlot == null) return;

            var prefab = Resources.Load<GameObject>("Prefabs/Armor/" + prefabId);
            if (prefab == null) return;

            _currentHelmetModel = Instantiate(prefab, _helmetSlot);
            _currentHelmetModel.transform.localPosition = Vector3.zero;
            _currentHelmetModel.transform.localRotation = Quaternion.identity;
        }

        public void SwapArmorModel(string prefabId)
        {
            if (prefabId == _currentArmorPrefabId) return;
            ClearArmorModel();
            _currentArmorPrefabId = prefabId;
            if (string.IsNullOrEmpty(prefabId) || _armorSlot == null) return;

            var prefab = Resources.Load<GameObject>("Prefabs/Armor/" + prefabId);
            if (prefab == null) return;

            _currentArmorModel = Instantiate(prefab, _armorSlot);
            _currentArmorModel.transform.localPosition = Vector3.zero;
            _currentArmorModel.transform.localRotation = Quaternion.identity;
        }

        public void ClearHelmetModel()
        {
            if (_currentHelmetModel != null)
                Destroy(_currentHelmetModel);
            _currentHelmetPrefabId = null;
            _currentHelmetModel = null;
        }

        public void ClearArmorModel()
        {
            if (_currentArmorModel != null)
                Destroy(_currentArmorModel);
            _currentArmorPrefabId = null;
            _currentArmorModel = null;
        }

        public GameObject DetachHelmetModel()
        {
            var model = _currentHelmetModel;
            _currentHelmetPrefabId = null;
            _currentHelmetModel = null;
            return model;
        }

        void SyncRollVisual(BotEntityState state)
        {
            if (_capsuleVisual == null) return;

            if (state.IsRolling)
            {
                _rollVisualAngle += (360f / DodgeConstants.Duration) * Time.deltaTime;

                var rollAxis = Vector3.Cross(Vector3.up, state.RollDirection);
                if (rollAxis.sqrMagnitude < 0.001f)
                    rollAxis = Vector3.right;

                _capsuleVisual.localRotation = Quaternion.AngleAxis(
                    _rollVisualAngle,
                    transform.InverseTransformDirection(rollAxis.normalized));
            }
            else if (_rollVisualAngle != 0f)
            {
                _rollVisualAngle = 0f;
                _capsuleVisual.localRotation = Quaternion.identity;
            }
        }

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
