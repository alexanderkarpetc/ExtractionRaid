using Dev;
using UnityEngine;

namespace View
{
    /// <summary>
    /// Tier 8.x* — lives on PAYLOAD prefab root (weapon "base"). Owns Animator, animation
    /// triggers, recoil kick, muzzle flash/light. Delivery prefab (barrel) attaches as child
    /// of <see cref="_deliverySocket"/> at equip time via <see cref="AttachDelivery"/>.
    /// MuzzlePoint resolves dynamically — comes from inside the attached delivery's hierarchy.
    /// </summary>
    public class WeaponView : MonoBehaviour
    {
        [SerializeField] ParticleSystem _muzzleFlashPrefab;
        [SerializeField] Animator _animator;

        // Tier 8.x* — socket where delivery (barrel) prefab instantiates at equip time.
        // Resolved dynamically. MuzzlePoint comes from inside the attached delivery.
        [SerializeField] Transform _deliverySocket;

        // Gunplay A.5 — optional Point Light child of muzzle. If present, pulses bright
        // at PlayMuzzleFlash and decays to 0 over MuzzleVfx.LightDuration. Null = no pulse.
        // Auto-created on attached delivery's MuzzlePoint якщо missing.
        [SerializeField] Light _muzzleLight;

        // Light pulse state — driven by DevCheats.Config.MuzzleVfx.
        float _muzzleLightElapsedUnscaled;
        float _muzzleLightDuration;
        float _muzzleLightPeak;

        // Tier 8.x*: child Transform that receives procedural recoil kick on Fire. Lives
        // inside payload prefab (KickGroup containing PayloadBaseMesh + DeliverySocket).
        // Kicking the group keeps RightHandGrip stationary (no IK weirdness) while visual
        // mesh recoils. Code-driven kick replaces stale Mecanim clips. Optional; null = no
        // procedural feedback.
        [SerializeField] Transform _recoilKickTarget;
        [SerializeField] float     _recoilKickDistance = 0.04f;

        ParticleSystem _muzzleFlashInstance;

        // Attached delivery state — resolved at AttachDelivery() and cached.
        GameObject _attachedDelivery;
        Transform  _resolvedMuzzlePoint;

        // Procedural recoil state — local (not RaidState): purely visual feedback.
        Vector3 _kickRestLocalPos;
        bool    _kickRestCached;
        float   _kickElapsed;
        float   _kickDuration;

        static readonly int SpeedParam = Animator.StringToHash("Speed");

        /// <summary>
        /// MuzzlePoint resolved через attached delivery. Null коли no delivery attached
        /// (e.g., assembly failed) — view-side VFX gracefully no-op.
        /// </summary>
        public Transform MuzzlePoint => _resolvedMuzzlePoint;

        /// <summary>
        /// Tier 8.x* — instantiates delivery (barrel) prefab as child of <see cref="_deliverySocket"/>.
        /// Resolves MuzzlePoint child within the attached delivery for VFX usage.
        /// Call after weapon equip / on barrel swap. Replaces previous delivery atomically.
        /// </summary>
        public void AttachDelivery(GameObject barrelPrefab)
        {
            if (_attachedDelivery != null)
            {
                Destroy(_attachedDelivery);
                _attachedDelivery = null;
                _resolvedMuzzlePoint = null;
            }

            if (barrelPrefab == null || _deliverySocket == null)
                return;

            _attachedDelivery = Instantiate(barrelPrefab, _deliverySocket);
            _attachedDelivery.transform.localPosition = Vector3.zero;
            _attachedDelivery.transform.localRotation = Quaternion.identity;

            _resolvedMuzzlePoint = FindDeepChild(_attachedDelivery.transform, "MuzzlePoint");
            if (_resolvedMuzzlePoint == null)
            {
                Debug.LogWarning($"[WeaponView] Attached delivery '{barrelPrefab.name}' has no MuzzlePoint child — VFX disabled.");
                return;
            }

            // Sync MuzzlePoint world Y з projectile spawn height — flash, light pulse, casing
            // eject, tracer all anchor here, але bullet world spawn position has fixed Y per
            // ShootingSystem (cfg.ProjectileSpawnHeight). Aligning них one-shot at attach так
            // VFX renders at exact bullet trajectory line. Top-down camera: player Y constant,
            // WeaponPivot Y constant → MuzzlePoint world Y stays correct у naступних frames.
            var cfg = DevCheats.Config?.Parallax;
            if (cfg != null)
            {
                var worldPos = _resolvedMuzzlePoint.position;
                worldPos.y = cfg.ProjectileSpawnHeight;
                _resolvedMuzzlePoint.position = worldPos;
            }
        }

        public void PlayMuzzleFlash()
        {
            if (_muzzleFlashPrefab != null && _resolvedMuzzlePoint != null)
            {
                if (_muzzleFlashInstance == null)
                {
                    _muzzleFlashInstance = Instantiate(_muzzleFlashPrefab, _resolvedMuzzlePoint);
                    _muzzleFlashInstance.transform.localPosition = Vector3.zero;
                    _muzzleFlashInstance.transform.localRotation = Quaternion.identity;
                }
                _muzzleFlashInstance.Play();
            }

            // Gunplay A.5 — light pulse layer on muzzle moment.
            TriggerMuzzleLightPulse();
        }

        void TriggerMuzzleLightPulse()
        {
            var cfg = DevCheats.Config?.MuzzleVfx;
            if (cfg == null || !cfg.LightEnabled || cfg.LightDuration <= 0f) return;

            // Auto-create a Point Light on resolved muzzle if prefab didn't wire one.
            if (_muzzleLight == null && _resolvedMuzzlePoint != null)
            {
                var lightGO = new GameObject("MuzzleLight (auto)");
                lightGO.transform.SetParent(_resolvedMuzzlePoint, false);
                _muzzleLight = lightGO.AddComponent<Light>();
                _muzzleLight.type = LightType.Point;
                _muzzleLight.shadows = LightShadows.None;
            }
            if (_muzzleLight == null) return;

            _muzzleLight.color     = cfg.LightColor;
            _muzzleLight.range     = cfg.LightRange;
            _muzzleLight.intensity = cfg.LightIntensity;
            _muzzleLight.enabled   = true;
            _muzzleLightPeak              = cfg.LightIntensity;
            _muzzleLightDuration          = cfg.LightDuration;
            _muzzleLightElapsedUnscaled   = 0f;
        }

        // ── Animation triggers ─────────────────────────────────

        public void PlayFire(float duration)
        {
            PlayClip("Fire", duration);
            TriggerRecoilKick(duration);
        }
        public void PlayEquip(float duration)   => PlayClip("Equip", duration);
        public void PlayUnequip(float duration) => PlayClip("Unequip", duration);
        public void PlayReload(float duration)  => PlayClip("Reload", duration);
        public void PlayDryFire()               => _animator?.SetTrigger("DryFire");

        // ── Procedural recoil ──────────────────────────────────

        void TriggerRecoilKick(float fireDuration)
        {
            if (_recoilKickTarget == null || _recoilKickDistance <= 0f) return;
            if (!_kickRestCached)
            {
                _kickRestLocalPos = _recoilKickTarget.localPosition;
                _kickRestCached = true;
            }
            _kickDuration = Mathf.Max(0.06f, fireDuration * 0.4f);
            _kickElapsed  = 0f;
            _recoilKickTarget.localPosition = _kickRestLocalPos + new Vector3(0f, 0f, -_recoilKickDistance);
        }

        void Update()
        {
            UpdateRecoilKick();
            UpdateMuzzleLightPulse();
        }

        void UpdateRecoilKick()
        {
            if (!_kickRestCached || _kickDuration <= 0f) return;

            _kickElapsed += Time.deltaTime;
            if (_kickElapsed >= _kickDuration)
            {
                _recoilKickTarget.localPosition = _kickRestLocalPos;
                _kickDuration = 0f;
                return;
            }

            float t = _kickElapsed / _kickDuration;
            float eased = (1f - t) * (1f - t); // ease-out quad → snap back
            _recoilKickTarget.localPosition = _kickRestLocalPos + new Vector3(0f, 0f, -_recoilKickDistance * eased);
        }

        void UpdateMuzzleLightPulse()
        {
            if (_muzzleLight == null || _muzzleLightDuration <= 0f) return;

            _muzzleLightElapsedUnscaled += Time.unscaledDeltaTime;
            if (_muzzleLightElapsedUnscaled >= _muzzleLightDuration)
            {
                _muzzleLight.intensity = 0f;
                _muzzleLight.enabled = false;
                _muzzleLightDuration = 0f;
                return;
            }

            float t = _muzzleLightElapsedUnscaled / _muzzleLightDuration;
            float eased = (1f - t) * (1f - t);
            _muzzleLight.intensity = _muzzleLightPeak * eased;
        }

        /// <summary>
        /// Plays an animation clip at adjusted speed so it finishes in exactly <paramref name="duration"/> seconds.
        /// </summary>
        void PlayClip(string triggerName, float duration)
        {
            if (_animator == null) return;

            float clipLength = GetClipLength(triggerName);
            float speed = (clipLength > 0f && duration > 0f)
                ? clipLength / duration
                : 1f;

            _animator.SetFloat(SpeedParam, speed);
            _animator.SetTrigger(triggerName);
        }

        float GetClipLength(string clipName)
        {
            if (_animator == null || _animator.runtimeAnimatorController == null) return 0f;

            foreach (var clip in _animator.runtimeAnimatorController.animationClips)
                if (clip.name == clipName) return clip.length;

            return 0f;
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
    }
}
