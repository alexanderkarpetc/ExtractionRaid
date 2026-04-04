using Dev;
using State;
using UnityEngine;

namespace View
{
    public class BotView : MonoBehaviour, IDamageableView
    {
        CharacterBody _body; // bound at runtime via BindBody()

        WorldHealthBar _healthBar;
        BotDebugLabel _debugLabel;

        public EId EId { get; private set; }
        public string TypeId { get; private set; }
        public CharacterBody Body => _body;

        /// <summary>Bind a CharacterBody at runtime (shell+body composition).</summary>
        public void BindBody(CharacterBody body)
        {
            _body = body;
        }

        public void Initialize(EId id, string typeId, string weaponPrefabId, float maxHp)
        {
            EId = id;
            TypeId = typeId;
            _healthBar = WorldHealthBar.Create(transform, maxHp);
            _debugLabel = BotDebugLabel.Create(transform);

            if (!string.IsNullOrEmpty(weaponPrefabId) && _body != null)
                _body.SwapWeaponModel(weaponPrefabId);
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

            if (_body != null)
            {
                if (_body.WeaponPivot != null && state.AimDirection.sqrMagnitude > 0.001f)
                    _body.WeaponPivot.rotation = Quaternion.LookRotation(state.AimDirection, Vector3.up);

                if (_body.Animator != null)
                    _body.Animator.SetBool("Run", state.Velocity.sqrMagnitude > 0.01f);

                _body.SyncRollVisual(state.IsRolling, state.RollDirection, transform);
            }

            if (_debugLabel != null)
                _debugLabel.UpdateLabel(state, currentHp, maxHp);

            var bb = state.Blackboard;
            GizmoHasTarget = bb.HasTarget;
            GizmoTargetPos = bb.LastKnownTargetPos;
            GizmoPatrolWaypoints = bb.PatrolWaypoints;
            GizmoPatrolIndex = bb.PatrolWaypointIndex;
        }

        // ── Armor delegation ────────────────────────────────

        public void SwapHelmetModel(string prefabId) => _body?.SwapHelmetModel(prefabId);
        public void SwapArmorModel(string prefabId) => _body?.SwapArmorModel(prefabId);
        public void ClearHelmetModel() => _body?.ClearHelmetModel();
        public void ClearArmorModel() => _body?.ClearArmorModel();
        public GameObject DetachHelmetModel() => _body?.DetachHelmetModel();

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
    }
}
