using Constants;
using Dev;
using UnityEngine;

namespace View
{
    /// <summary>
    /// Shared character visual attachment logic — weapon, armor, roll animation,
    /// and weapon-wall pullback.
    /// Lives on the character MODEL prefab. PlayerView/BotView delegate to this.
    ///
    /// Execution order > 1000 so LateUpdate runs AFTER AppBootstrap.LateUpdate
    /// (which writes _weaponPivot.rotation via presenters). Otherwise the pullback
    /// SphereCast reads stale forward vectors and lags one frame behind aim.
    /// </summary>
    [DefaultExecutionOrder(2000)]
    public class CharacterBody : MonoBehaviour
    {
        [SerializeField] Transform _weaponPivot;
        [SerializeField] Transform _helmetSlot;
        [SerializeField] Transform _armorSlot;
        [SerializeField] Transform _capsuleVisual;
        [SerializeField] Animator _animator;
        [SerializeField] TwoBoneIK _rightHandIK; // optional; solves right hand → weapon RightHandGrip

        string _currentWeaponPrefabId;
        GameObject _currentWeaponModel;
        WeaponView _currentWeaponView;

        string _currentHelmetPrefabId;
        GameObject _currentHelmetModel;

        string _currentArmorPrefabId;
        GameObject _currentArmorModel;

        bool _wasRollingLastFrame;
        bool _wasMovingLastFrame;

        // Solution 3a: WeaponPivot pullback
        Vector3 _weaponPivotRestLocalPos;
        bool _weaponPivotRestCached;
        static readonly RaycastHit[] PullbackHitBuffer = new RaycastHit[16];

        // Throttling / LOD state
        float _pullbackCheckTimer; // accumulator for throttled physics queries
        float _lastClosestDistance = float.PositiveInfinity; // persisted between throttled casts
        float _lastPivotDistFromOrigin; // pivot-along-ray distance for the last cast

        // Is this the player? Set by PlayerPresenter after instantiation. Bots = false → throttled.
        bool _isPlayerPullback;

#if UNITY_EDITOR
        // Debug gizmo state (last raycast result) — editor only
        Vector3 _gizmoOrigin;
        Vector3 _gizmoForward;
        float _gizmoRayLength;
        float _gizmoRadius;
        bool _gizmoHit;
        Vector3 _gizmoHitPoint;
        float _gizmoRetract;
#endif

        // ── Public access ──────────────────────────────────

        public Transform WeaponPivot => _weaponPivot;
        public WeaponView WeaponView => _currentWeaponView;

        /// <summary>
        /// Mark this body as the local player. Enables every-frame pullback updates;
        /// otherwise the body is throttled to a low rate (LOD for distant bots).
        /// </summary>
        public void SetIsPlayerPullback(bool isPlayer) => _isPlayerPullback = isPlayer;

        void Awake()
        {
            // Cache WeaponPivot rest pose deterministically — before any code (weapon swap,
            // ADS, recoil) can mutate its localPosition. This avoids the "rest = retracted"
            // foot-gun if a future refactor writes to pivot.localPosition before LateUpdate.
            if (_weaponPivot != null)
            {
                _weaponPivotRestLocalPos = _weaponPivot.localPosition;
                _weaponPivotRestCached = true;
            }
        }
        public Transform MuzzlePoint => _currentWeaponView != null ? _currentWeaponView.MuzzlePoint : null;
        public Animator Animator => _animator;

        public void SyncAnimatorState(bool isRolling, Vector3 velocity, float maxSpeed)
        {
            bool isMoving = velocity.sqrMagnitude > 0.01f;

            if (_animator == null)
            {
                _wasRollingLastFrame = isRolling;
                _wasMovingLastFrame = isMoving;
                return;
            }

            // Locomotion blend tree expects normalized strafe/forward in character-local space.
            // Body is rotated to FacingDirection (aim), so local X = strafe, Z = forward.
            // Damped SetFloat smooths Idle↔Run transitions without needing separate animator states.
            float invMax = maxSpeed > 0.001f ? 1f / maxSpeed : 0f;
            var localVel = transform.InverseTransformDirection(velocity);
            float damp = DevCheats.Config.Player.LocomotionBlendDampTime;
            _animator.SetFloat("SpeedX", Mathf.Clamp(localVel.x * invMax, -1f, 1f), damp, Time.deltaTime);
            _animator.SetFloat("SpeedY", Mathf.Clamp(localVel.z * invMax, -1f, 1f), damp, Time.deltaTime);

            _animator.SetBool("Run", !isRolling && isMoving);

            if (isRolling && !_wasRollingLastFrame)
            {
                _animator.ResetTrigger("Roll");
                _animator.SetTrigger("Roll");
            }
            else if (!isRolling && _wasRollingLastFrame)
            {
                _animator.ResetTrigger("Roll");
            }

            if (!isRolling && !isMoving && (_wasRollingLastFrame || _wasMovingLastFrame))
            {
                _animator.ResetTrigger("Idle");
                _animator.SetTrigger("Idle");
            }

            _wasRollingLastFrame = isRolling;
            _wasMovingLastFrame = isMoving;
        }

        // ── Weapon ─────────────────────────────────────────

        /// <summary>Equip weapon model. Returns the new WeaponView (or null).</summary>
        public WeaponView SwapWeaponModel(string prefabId)
        {
            if (_currentWeaponModel != null)
                Destroy(_currentWeaponModel);

            _currentWeaponPrefabId = prefabId;
            _currentWeaponView = null;

            if (string.IsNullOrEmpty(prefabId) || _weaponPivot == null)
                return null;

            var prefab = Resources.Load<GameObject>("Prefabs/Weapons/" + prefabId);
            if (prefab == null)
            {
                Debug.LogWarning($"[CharacterBody] Weapon prefab not found: Prefabs/Weapons/{prefabId}");
                return null;
            }

            _currentWeaponModel = Instantiate(prefab, _weaponPivot);
            _currentWeaponModel.transform.localPosition = Vector3.zero;
            _currentWeaponModel.transform.localRotation = Quaternion.identity;

            _currentWeaponView = _currentWeaponModel.GetComponent<WeaponView>();

            if (_rightHandIK != null)
                _rightHandIK.SetTarget(FindDeepChild(_currentWeaponModel.transform, "RightHandGrip"));

            return _currentWeaponView;
        }

        public void ClearWeaponModel()
        {
            if (_currentWeaponModel != null)
                Destroy(_currentWeaponModel);

            _currentWeaponPrefabId = null;
            _currentWeaponModel = null;
            _currentWeaponView = null;

            if (_rightHandIK != null)
                _rightHandIK.SetTarget(null);
        }

        static Transform FindDeepChild(Transform parent, string name)
        {
            if (parent.name == name) return parent;
            for (int i = 0; i < parent.childCount; i++)
            {
                var found = FindDeepChild(parent.GetChild(i), name);
                if (found != null) return found;
            }
            return null;
        }

        public string CurrentWeaponPrefabId => _currentWeaponPrefabId;

        // ── Helmet ─────────────────────────────────────────

        public void SwapHelmetModel(string prefabId)
        {
            if (prefabId == _currentHelmetPrefabId) return;
            ClearHelmetModel();
            _currentHelmetPrefabId = prefabId;
            if (string.IsNullOrEmpty(prefabId) || _helmetSlot == null) return;

            var prefab = Resources.Load<GameObject>("Prefabs/Armor/" + prefabId);
            if (prefab == null)
            {
                Debug.LogWarning($"[CharacterBody] Helmet prefab not found: Prefabs/Armor/{prefabId}");
                return;
            }

            _currentHelmetModel = Instantiate(prefab, _helmetSlot);
            _currentHelmetModel.transform.localPosition = Vector3.zero;
            _currentHelmetModel.transform.localRotation = Quaternion.identity;
        }

        public void ClearHelmetModel()
        {
            if (_currentHelmetModel != null)
                Destroy(_currentHelmetModel);
            _currentHelmetPrefabId = null;
            _currentHelmetModel = null;
        }

        /// <summary>Detach helmet for fly-off effect. Returns the GO without destroying it.</summary>
        public GameObject DetachHelmetModel()
        {
            var model = _currentHelmetModel;
            _currentHelmetPrefabId = null;
            _currentHelmetModel = null;
            return model;
        }

        // ── Body Armor ─────────────────────────────────────

        public void SwapArmorModel(string prefabId)
        {
            if (prefabId == _currentArmorPrefabId) return;
            ClearArmorModel();
            _currentArmorPrefabId = prefabId;
            if (string.IsNullOrEmpty(prefabId) || _armorSlot == null) return;

            var prefab = Resources.Load<GameObject>("Prefabs/Armor/" + prefabId);
            if (prefab == null)
            {
                Debug.LogWarning($"[CharacterBody] Armor prefab not found: Prefabs/Armor/{prefabId}");
                return;
            }

            _currentArmorModel = Instantiate(prefab, _armorSlot);
            _currentArmorModel.transform.localPosition = Vector3.zero;
            _currentArmorModel.transform.localRotation = Quaternion.identity;
        }

        public void ClearArmorModel()
        {
            if (_currentArmorModel != null)
                Destroy(_currentArmorModel);
            _currentArmorPrefabId = null;
            _currentArmorModel = null;
        }

        // ── Roll animation ─────────────────────────────────

        /// <summary>Drive roll visual. Call every frame from SyncFromState.</summary>
        public void SyncRollVisual(bool isRolling, Vector3 rollDirection, Transform characterTransform)
        {
            if (_capsuleVisual != null)
                _capsuleVisual.localRotation = Quaternion.identity;
        }

        // ── Weapon pullback (Solution 3a) ───────────────────
        // Runs in LateUpdate (execution order 2000) so _weaponPivot.forward is already rotated
        // by PlayerView/BotView.SyncFromState invoked from AppBootstrap.LateUpdate (order 1000).
        // Muzzle point is a grandchild of WeaponPivot → pulling the pivot back automatically retracts
        // the muzzle flash and visible barrel, preventing wall clipping without explicit VFX suppression.
        //
        // Uses SphereCast (not Raycast) with a small radius + origin backoff:
        //   - SphereCast returns hit.distance==0 when the sphere overlaps a collider at the start —
        //     reliably catches "weapon already clipped into wall" case that Raycast misses.
        //   - Origin is offset backward along -forward so the cast starts inside the character body,
        //     never inside a wall collider (which can produce inconsistent hit results in Unity).
        //
        // Throttling: bots cast at a lower rate and skip entirely when far from the camera.
        // Lerp to current retract target runs every frame regardless, so visuals stay smooth.
        void LateUpdate()
        {
            if (_weaponPivot == null) return;

            // Cache weapon section once — avoids 9 property-chain accesses per frame.
            var cfg = DevCheats.Config.Weapon;

            float lerpAlpha = 1f - Mathf.Exp(-cfg.WeaponPullbackSpeed * Time.deltaTime);

            if (!cfg.WeaponPullbackEnabled)
            {
                _weaponPivot.localPosition = Vector3.Lerp(
                    _weaponPivot.localPosition, _weaponPivotRestLocalPos, lerpAlpha);
                _lastClosestDistance = float.PositiveInfinity;
#if UNITY_EDITOR
                _gizmoHit = false;
                _gizmoRetract = 0f;
#endif
                return;
            }

            // LOD + throttle: bots run physics queries at a low rate (and skip when far from camera).
            // Player always runs every frame for snappy first-person-like feedback.
            bool shouldCastThisFrame = _isPlayerPullback;
            if (!shouldCastThisFrame)
            {
                // Distance LOD — skip pullback physics for bots far from the camera.
                if (Camera.main != null)
                {
                    float lodSqr = cfg.BotPullbackLodDistance * cfg.BotPullbackLodDistance;
                    if ((transform.position - Camera.main.transform.position).sqrMagnitude > lodSqr)
                    {
                        _lastClosestDistance = float.PositiveInfinity;
                        _weaponPivot.localPosition = Vector3.Lerp(
                            _weaponPivot.localPosition, _weaponPivotRestLocalPos, lerpAlpha);
                        return;
                    }
                }

                _pullbackCheckTimer += Time.deltaTime;
                float tickInterval = 1f / Mathf.Max(1f, cfg.BotPullbackCheckRateHz);
                if (_pullbackCheckTimer >= tickInterval)
                {
                    _pullbackCheckTimer = 0f;
                    shouldCastThisFrame = true;
                }
            }

            float weaponLength = cfg.WeaponLength;

            if (shouldCastThisFrame)
            {
                float radius = cfg.WeaponPullbackRadius;
                float spawnHeight = DevCheats.Config.Parallax.ProjectileSpawnHeight;

                // Origin matches Solution 2 (ShootingSystem muzzle-block): player body at bullet
                // spawn height. Ensures S3a sees the SAME walls as S2 — avoids the desync where
                // the barrel would visually stick past a wall edge that the bullet ray was clamping.
                var origin = new Vector3(
                    transform.root.position.x,
                    spawnHeight,
                    transform.root.position.z);

                // Direction from body toward muzzle tip (projected at spawn height, same as S2).
                // Use MuzzlePoint if available; fall back to pivot + forward*weaponLength.
                Vector3 muzzleWorld = _currentWeaponView != null && _currentWeaponView.MuzzlePoint != null
                    ? _currentWeaponView.MuzzlePoint.position
                    : _weaponPivot.position + _weaponPivot.forward * weaponLength;
                var muzzleAtSpawnY = new Vector3(muzzleWorld.x, spawnHeight, muzzleWorld.z);

                var toMuzzle = muzzleAtSpawnY - origin;
                float bodyToMuzzleDist = toMuzzle.magnitude;
                if (bodyToMuzzleDist < 0.001f)
                {
                    _lastClosestDistance = float.PositiveInfinity;
                    _lastPivotDistFromOrigin = 0f;
                }
                else
                {
                    var rayDir = toMuzzle / bodyToMuzzleDist;
                    // Cast extends past the muzzle by weaponLength so retract math has headroom.
                    float castDist = bodyToMuzzleDist + weaponLength;

                    // Project pivot onto the ray to get its scalar distance from origin.
                    // This is the reference point for retract (retract=1 when wall is at pivot).
                    float pivotDistFromOrigin = Vector3.Dot(_weaponPivot.position - origin, rayDir);

                    // SphereCast — robust vs Raycast: catches walls even when pivot is inside a collider,
                    // and the small radius provides a "barrel width" tolerance for edge cases.
                    int count = Physics.SphereCastNonAlloc(origin, radius, rayDir, PullbackHitBuffer,
                        castDist, BotConstants.VisionBlockingMask);

                    float closest = float.PositiveInfinity;
                    Vector3 closestPoint = default;
                    var selfRoot = transform.root;
                    for (int i = 0; i < count; i++)
                    {
                        var hitRoot = PullbackHitBuffer[i].collider.transform.root;
                        // Hierarchy filter: ignore own character (shell + body + armor).
                        // Tiled-wall safe (position-based filter would misclassify bricks near the player).
                        if (hitRoot == selfRoot) continue;
                        // Also ignore OTHER characters — bots/players are not walls: the weapon
                        // should not retract when passing over/through another character's capsule
                        // (their capsule collider is on Default layer and would otherwise match).
                        if (hitRoot.GetComponent<PlayerView>() != null) continue;
                        if (hitRoot.GetComponent<BotView>() != null) continue;

                        if (PullbackHitBuffer[i].distance < closest)
                        {
                            closest = PullbackHitBuffer[i].distance;
                            closestPoint = PullbackHitBuffer[i].point;
                        }
                    }

                    _lastClosestDistance = closest;
                    _lastPivotDistFromOrigin = pivotDistFromOrigin;

#if UNITY_EDITOR
                    _gizmoOrigin = origin;
                    _gizmoForward = rayDir;
                    _gizmoRayLength = castDist;
                    _gizmoRadius = radius;
                    _gizmoHit = !float.IsPositiveInfinity(closest);
                    _gizmoHitPoint = closestPoint;
#endif
                }
            }

            float retract = WeaponPullbackMath.ComputeRetract(
                _lastClosestDistance, _lastPivotDistFromOrigin, weaponLength);

            var target = _weaponPivotRestLocalPos + Vector3.back * (cfg.WeaponPullbackAmount * retract);
            _weaponPivot.localPosition = Vector3.Lerp(
                _weaponPivot.localPosition, target, lerpAlpha);

#if UNITY_EDITOR
            _gizmoRetract = retract;
#endif
        }

#if UNITY_EDITOR
        void OnDrawGizmosSelected()
        {
            if (!DevCheats.WeaponPullbackDebugGizmos) return;
            if (_weaponPivot == null) return;

            // Ray path (green = clear, red = retracting)
            Gizmos.color = _gizmoRetract > 0f
                ? Color.Lerp(Color.yellow, Color.red, _gizmoRetract)
                : Color.green;
            Gizmos.DrawLine(_gizmoOrigin, _gizmoOrigin + _gizmoForward * _gizmoRayLength);

            // Start sphere
            Gizmos.color = new Color(0f, 1f, 1f, 0.5f);
            Gizmos.DrawWireSphere(_gizmoOrigin, _gizmoRadius);

            // End sphere
            Gizmos.color = new Color(1f, 1f, 0f, 0.3f);
            Gizmos.DrawWireSphere(_gizmoOrigin + _gizmoForward * _gizmoRayLength, _gizmoRadius);

            if (_gizmoHit)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawSphere(_gizmoHitPoint, 0.06f);
            }

            // Weapon pivot position marker
            Gizmos.color = Color.white;
            Gizmos.DrawWireCube(_weaponPivot.position, Vector3.one * 0.04f);
        }
#endif
    }
}
