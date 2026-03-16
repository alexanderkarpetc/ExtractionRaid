using System;
using Constants;
using Dev;
using State;
using Systems;
using UnityEngine;

namespace View
{
    public class PlayerView : MonoBehaviour, IDamageableView
    {
        [SerializeField] Transform _weaponPivot;
        [SerializeField] Transform _capsuleVisual;
        [SerializeField] Animator _animator;

        Transform _muzzlePoint;
        Action<Transform> _onMuzzlePointChanged;
        string _currentWeaponPrefabId;
        GameObject _currentWeaponModel;
        WorldHealthBar _healthBar;
        WorldProgressBar _progressBar;
        WeaponView _currentWeaponView;
        float _rollVisualAngle;

        public EId EId { get; private set; }
        public Transform MuzzlePoint => _muzzlePoint;
        public WeaponView WeaponView => _currentWeaponView;

        public void Initialize(EId id, Action<Transform> onMuzzlePointChanged)
        {
            EId = id;
            _onMuzzlePointChanged = onMuzzlePointChanged;
            _healthBar = WorldHealthBar.Create(transform);
            _progressBar = WorldProgressBar.Create(transform);
        }

        public void OnDamaged(float currentHp, float maxHp)
        {
            if (_healthBar != null)
                _healthBar.UpdateHealth(currentHp, maxHp);
        }

        public void SyncFromState(PlayerEntityState state, float elapsedTime)
        {
            transform.position = state.Position;

            if (_progressBar != null)
            {
                if (state.IsUsingBandage)
                {
                    float progress = (elapsedTime - state.BandageUseStartTime)
                                     / StatusEffectConstants.BandageUseTime;
                    _progressBar.SetProgress(progress);
                }
                else
                {
                    _progressBar.Hide();
                }
            }

            if (state.FacingDirection.sqrMagnitude > 0.001f)
            {
                transform.rotation = Quaternion.LookRotation(state.FacingDirection, Vector3.up);
            }

            SyncRollVisual(state);

            if (_animator != null)
                _animator.SetBool("Run", state.Velocity.sqrMagnitude > 0.01f);

            if (_weaponPivot != null)
            {
                bool hasWeapon = state.EquippedWeapon != null;
                _weaponPivot.gameObject.SetActive(hasWeapon);

                if (hasWeapon)
                {
                    if (state.EquippedWeapon.PrefabId != _currentWeaponPrefabId)
                        SwapWeaponModel(state.EquippedWeapon.PrefabId);

                    // Rotate weapon toward aim point (not just AimDirection from center)
                    var toAim = state.WeaponAimPoint - _weaponPivot.position;
                    toAim.y = 0f;
                    if (toAim.sqrMagnitude > 0.001f)
                    {
                        _weaponPivot.rotation = Quaternion.LookRotation(toAim.normalized, Vector3.up);
                    }
                }
                else if (_currentWeaponPrefabId != null)
                {
                    ClearWeaponModel();
                }
            }
        }

        void SyncRollVisual(PlayerEntityState state)
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

            if (string.IsNullOrEmpty(prefabId)) return;

            var prefab = Resources.Load<GameObject>("Prefabs/Weapons/" + prefabId);
            if (prefab == null)
            {
                Debug.LogWarning($"[PlayerView] Weapon prefab not found: Prefabs/Weapons/{prefabId}");
                return;
            }

            _currentWeaponModel = Instantiate(prefab, _weaponPivot);
            _currentWeaponModel.transform.localPosition = Vector3.zero;
            _currentWeaponModel.transform.localRotation = Quaternion.identity;

            _currentWeaponView = _currentWeaponModel.GetComponent<WeaponView>();
            _muzzlePoint = _currentWeaponView != null ? _currentWeaponView.MuzzlePoint : null;
            _onMuzzlePointChanged?.Invoke(_muzzlePoint);
        }

#if UNITY_EDITOR
        void OnDrawGizmos()
        {
            if (!DevCheats.FOVEnabled) return;

            var drawPos = transform.position + Vector3.up * 0.1f;
            var rayOrigin = transform.position + Vector3.up * BotConstants.PlayerEyeHeight;
            var forward = transform.forward;
            float nearR = DevCheats.FOVNearRadius;
            float farR = DevCheats.FOVFarRadius;
            float halfAngle = DevCheats.FOVAngle * 0.5f;
            bool occlusion = DevCheats.FOVOcclusionEnabled;
            int layerMask = BotConstants.VisionBlockingMask;

            if (!occlusion)
            {
                // Simple wireframe (no raycasts)
                Gizmos.color = new Color(1f, 1f, 0f, 0.4f);
                Gizmos.DrawWireSphere(drawPos, nearR);

                Gizmos.color = new Color(0f, 1f, 0f, 0.4f);
                var leftDir = Quaternion.Euler(0f, -halfAngle, 0f) * forward;
                var rightDir = Quaternion.Euler(0f, halfAngle, 0f) * forward;
                Gizmos.DrawLine(drawPos, drawPos + leftDir * farR);
                Gizmos.DrawLine(drawPos, drawPos + rightDir * farR);

                int segments = 24;
                var prevPoint = drawPos + leftDir * farR;
                for (int i = 1; i <= segments; i++)
                {
                    float t = (float)i / segments;
                    float a = Mathf.Lerp(-halfAngle, halfAngle, t);
                    var dir = Quaternion.Euler(0f, a, 0f) * forward;
                    var point = drawPos + dir * farR;
                    Gizmos.DrawLine(prevPoint, point);
                    prevPoint = point;
                }
                return;
            }

            // Temporarily disable player colliders so rays don't hit self
            var colliders = GetComponentsInChildren<Collider>();
            foreach (var c in colliders) c.enabled = false;

            var clearYellow = new Color(1f, 1f, 0f, 0.4f);
            var clearGreen = new Color(0f, 1f, 0f, 0.4f);
            var blockedColor = new Color(1f, 0.2f, 0f, 0.3f);

            // Inner sphere: 360°, 5° step
            DrawOccludedArc(rayOrigin, drawPos, forward, nearR, -180f, 180f, 5f,
                layerMask, clearYellow, blockedColor);

            // Outer sector: FOV angle, 1° step
            DrawOccludedArc(rayOrigin, drawPos, forward, farR, -halfAngle, halfAngle, 1f,
                layerMask, clearGreen, blockedColor);

            foreach (var c in colliders) c.enabled = true;
        }

        void DrawOccludedArc(Vector3 rayOrigin, Vector3 drawOrigin, Vector3 forward,
            float maxDist, float startAngle, float endAngle, float stepDeg, int layerMask,
            Color clearColor, Color blockedColor)
        {
            Vector3 prevPoint = drawOrigin;
            bool prevBlocked = false;
            bool first = true;

            for (float a = startAngle; a <= endAngle; a += stepDeg)
            {
                var dir = Quaternion.Euler(0f, a, 0f) * forward;
                float drawDist = maxDist;
                bool blocked = false;

                if (Physics.Raycast(rayOrigin, dir, out var hit, maxDist, layerMask))
                {
                    drawDist = hit.distance;
                    blocked = true;
                }

                var point = drawOrigin + dir * drawDist;

                if (blocked)
                {
                    // Green up to hit, red beyond
                    var hitPoint = drawOrigin + dir * drawDist;
                    var endPoint = drawOrigin + dir * maxDist;
                    Gizmos.color = clearColor;
                    Gizmos.DrawLine(drawOrigin, hitPoint);
                    Gizmos.color = blockedColor;
                    Gizmos.DrawLine(hitPoint, endPoint);
                }
                else
                {
                    Gizmos.color = clearColor;
                    Gizmos.DrawLine(drawOrigin, point);
                }

                if (!first)
                {
                    Gizmos.color = (blocked || prevBlocked) ? blockedColor : clearColor;
                    Gizmos.DrawLine(prevPoint, point);
                }

                prevPoint = point;
                prevBlocked = blocked;
                first = false;
            }
        }
#endif

        void ClearWeaponModel()
        {
            if (_currentWeaponModel != null)
                Destroy(_currentWeaponModel);

            _currentWeaponPrefabId = null;
            _currentWeaponModel = null;
            _currentWeaponView = null;
            _muzzlePoint = null;
            _onMuzzlePointChanged?.Invoke(null);
        }
    }
}
