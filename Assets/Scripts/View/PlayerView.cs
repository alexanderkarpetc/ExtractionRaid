using System;
using Constants;
using Dev;
using State;
using Systems;
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
        WorldStatusIcons _statusIcons;

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
            _statusIcons = WorldStatusIcons.Create(transform, id);
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
            {
                _body.SyncAnimatorState(state.IsRolling, state.Velocity, MovementSystem.MoveSpeed);
                _body.SyncRollVisual(state.IsRolling, state.RollDirection, transform);
            }

            if (_body != null && _body.WeaponPivot != null)
            {
                bool hasWeapon = state.EquippedWeapon != null;
                _body.WeaponPivot.gameObject.SetActive(hasWeapon);

                if (hasWeapon)
                {
                    if (state.EquippedWeapon.PrefabId != _body.CurrentWeaponPrefabId)
                    {
                        // Tier 8.x*: payload = weapon base (root), delivery = barrel insert.
                        // Both required for valid weapon; if either null → assembly failed.
                        _body.SwapWeaponModel(
                            state.EquippedWeapon.BasePrefab,
                            state.EquippedWeapon.BarrelPrefab,
                            state.EquippedWeapon.PrefabId);
                        _onMuzzlePointChanged?.Invoke(_body.MuzzlePoint);
                    }

                    // ── Weapon pivot rotation ─────────────────────────────────────────────
                    // This block is load-bearing and has TWO non-obvious constraints. Read before
                    // changing — we've regressed here multiple times.
                    //
                    // CONSTRAINT 1: Direction is computed from WeaponPivot (not player.Position).
                    //   Why: when the player stands next to a wall and the barrel pokes around
                    //   the corner, the bullet must fly OUT OF THE BARREL toward the target —
                    //   not from the player's body through the wall. A pivot-based direction
                    //   keeps the barrel visually aligned with the crosshair AND the muzzle-to-
                    //   target vector matches the projectile spawn direction.
                    //   DO NOT replace with state.AimDirection (that's what BotView uses and is
                    //   wrong for the player — breaks the wall-peek case).
                    //
                    // CONSTRAINT 2: toAim.y = 0 — rifle stays horizontal in world space.
                    //   Why: the top-down tilted camera looks weird if the rifle pitches down to
                    //   aim at the ground. Players expect a horizontal barrel.
                    //
                    // PROBLEM these two create: the pivot sits at shoulder height (Y≈1.1) while
                    //   WeaponAimPoint sits on the ground (Y=0). A horizontal barrel from Y=1.1
                    //   does NOT visually hit a ground-plane crosshair on a tilted camera —
                    //   on-screen, the barrel appears to point "above" or "beside" close targets,
                    //   so the rifle visibly fails to rotate far enough toward the cursor when
                    //   the cursor is near the player. Far cursors: angle is tiny, no visible
                    //   mismatch. Close cursors (inside MinAimDistance clamp): angle is big,
                    //   barrel appears to miss.
                    //
                    // FIX: parallax correction — lerp the aim point toward the camera position
                    //   proportional to pivot height / camera height. This lifts the effective
                    //   aim target up toward the camera so a horizontal barrel from Y=pivotY
                    //   screen-projects onto the ground crosshair. Same formula ShootingSystem
                    //   uses for projectile direction (keep them in sync — see ShootingSystem.cs
                    //   "Parallax-corrected direction" block). Toggle via DevCheats.Parallax.
                    //   WeaponPivotParallaxCorrection to A/B the effect.
                    //
                    // ALSO RELATED: AimingSystem.MinAimDistance clamps WeaponAimPoint to a min
                    //   radius around the player to prevent jitter when cursor hovers over the
                    //   character. Don't lower it thinking it will fix this — it'll cause flip
                    //   jitter. The parallax correction here is the right lever.
                    var pivotPos = _body.WeaponPivot.position;
                    var aimPoint = state.WeaponAimPoint;
                    if (DevCheats.WeaponPivotParallaxCorrection && pivotPos.y > 0.01f)
                    {
                        var cam = Camera.main;
                        if (cam != null)
                        {
                            var camPos = cam.transform.position;
                            if (camPos.y > 0.1f)
                            {
                                float ratio = pivotPos.y / camPos.y;
                                aimPoint = Vector3.Lerp(aimPoint, camPos, ratio);
                            }
                        }
                    }

                    var toAim = aimPoint - pivotPos;
                    toAim.y = 0f; // keep rifle horizontal — see CONSTRAINT 2 above
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

        // ── Hit feedback delegation (CharacterHitFx on body) ──

        CharacterHitFx _hitFx;

        public void TriggerHitFlash(Color color, float intensity, float durationUnscaled)
        {
            ResolveHitFx()?.TriggerRimFlash(color, intensity, durationUnscaled);
        }

        public void AddHitDecal(Vector3 worldPos, Color tint = default)
        {
            ResolveHitFx()?.AddHitDecal(worldPos, tint);
        }

        CharacterHitFx ResolveHitFx()
        {
            if (_hitFx == null && _body != null)
                _hitFx = _body.GetComponent<CharacterHitFx>();
            return _hitFx;
        }

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
