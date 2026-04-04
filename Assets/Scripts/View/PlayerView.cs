using System;
using Constants;
using Dev;
using State;
using UnityEngine;
using View.FogOfWar;

namespace View
{
    public class PlayerView : MonoBehaviour, IDamageableView
    {
        CharacterBody _body; // bound at runtime via BindBody()

        Action<Transform> _onMuzzlePointChanged;
        WorldHealthBar _healthBar;
        WorldProgressBar _progressBar;

        public EId EId { get; private set; }
        public Transform MuzzlePoint => _body != null ? _body.MuzzlePoint : null;
        public WeaponView WeaponView => _body != null ? _body.WeaponView : null;
        public CharacterBody Body => _body;

        /// <summary>Bind a CharacterBody at runtime (shell+body composition).</summary>
        public void BindBody(CharacterBody body)
        {
            _body = body;
        }

        public void Initialize(EId id, Action<Transform> onMuzzlePointChanged, float maxHp)
        {
            EId = id;
            _onMuzzlePointChanged = onMuzzlePointChanged;
            _healthBar = WorldHealthBar.Create(transform, maxHp);
            _progressBar = WorldProgressBar.Create(transform);
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
                transform.rotation = Quaternion.LookRotation(state.FacingDirection, Vector3.up);

            if (_body != null)
                _body.SyncRollVisual(state.IsRolling, state.RollDirection, transform);

            if (_body != null && _body.Animator != null)
                _body.Animator.SetBool("Run", state.Velocity.sqrMagnitude > 0.01f);

            if (_body != null && _body.WeaponPivot != null)
            {
                bool hasWeapon = state.EquippedWeapon != null;
                _body.WeaponPivot.gameObject.SetActive(hasWeapon);

                if (hasWeapon)
                {
                    if (state.EquippedWeapon.PrefabId != _body.CurrentWeaponPrefabId)
                    {
                        _body.SwapWeaponModel(state.EquippedWeapon.PrefabId);
                        _onMuzzlePointChanged?.Invoke(_body.MuzzlePoint);
                    }

                    var toAim = state.WeaponAimPoint - _body.WeaponPivot.position;
                    toAim.y = 0f;
                    if (toAim.sqrMagnitude > 0.001f)
                        _body.WeaponPivot.rotation = Quaternion.LookRotation(toAim.normalized, Vector3.up);
                }
                else if (_body.CurrentWeaponPrefabId != null)
                {
                    _body.ClearWeaponModel();
                    _onMuzzlePointChanged?.Invoke(null);
                }
            }
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
            if (!DevCheats.FOVEnabled) return;

            var drawPos = transform.position + Vector3.up * 0.1f;
            var forward = transform.forward;
            float halfAngle = DevCheats.FOVAngle * 0.5f;

            if (!DevCheats.FOVOcclusionEnabled)
            {
                Gizmos.color = new Color(1f, 1f, 0f, 0.4f);
                Gizmos.DrawWireSphere(drawPos, DevCheats.FOVNearRadius);

                Gizmos.color = new Color(0f, 1f, 0f, 0.4f);
                var leftDir = Quaternion.Euler(0f, -halfAngle, 0f) * forward;
                var rightDir = Quaternion.Euler(0f, halfAngle, 0f) * forward;
                Gizmos.DrawLine(drawPos, drawPos + leftDir * DevCheats.FOVFarRadius);
                Gizmos.DrawLine(drawPos, drawPos + rightDir * DevCheats.FOVFarRadius);

                int segments = 24;
                var prevPoint = drawPos + leftDir * DevCheats.FOVFarRadius;
                for (int i = 1; i <= segments; i++)
                {
                    float t = (float)i / segments;
                    float a = Mathf.Lerp(-halfAngle, halfAngle, t);
                    var dir = Quaternion.Euler(0f, a, 0f) * forward;
                    var point = drawPos + dir * DevCheats.FOVFarRadius;
                    Gizmos.DrawLine(prevPoint, point);
                    prevPoint = point;
                }
                return;
            }

            var rays = FOVRaySweep.LastRawRays;
            if (rays.Count == 0) return;

            var clearYellow = new Color(1f, 1f, 0f, 0.4f);
            var clearGreen = new Color(0f, 1f, 0f, 0.4f);
            var blockedColor = new Color(1f, 0.2f, 0f, 0.3f);
            var edgeColor = Color.cyan;
            const float edgeThreshold = 0.5f;

            Vector3 prevPoint2 = drawPos;
            bool prevBlocked = false;
            bool first = true;

            for (int i = 0; i < rays.Count; i++)
            {
                var ray = rays[i];
                var dir = Quaternion.Euler(0f, ray.Angle, 0f) * forward;
                var point = drawPos + dir * ray.Dist;
                bool isInFOV = Mathf.Abs(ray.Angle) <= halfAngle;
                var clearColor = isInFOV ? clearGreen : clearYellow;

                if (ray.Hit)
                {
                    var hitPoint = drawPos + dir * ray.Dist;
                    var endPoint = drawPos + dir * ray.MaxDist;
                    Gizmos.color = clearColor;
                    Gizmos.DrawLine(drawPos, hitPoint);
                    Gizmos.color = blockedColor;
                    Gizmos.DrawLine(hitPoint, endPoint);
                }
                else
                {
                    Gizmos.color = clearColor;
                    Gizmos.DrawLine(drawPos, point);
                }

                if (!first)
                {
                    Gizmos.color = (ray.Hit || prevBlocked) ? blockedColor : clearColor;
                    Gizmos.DrawLine(prevPoint2, point);

                    if (Mathf.Abs(ray.Dist - rays[i - 1].Dist) > edgeThreshold)
                    {
                        Gizmos.color = edgeColor;
                        Gizmos.DrawLine(prevPoint2, point);
                    }
                }

                prevPoint2 = point;
                prevBlocked = ray.Hit;
                first = false;
            }
        }
#endif
    }
}
