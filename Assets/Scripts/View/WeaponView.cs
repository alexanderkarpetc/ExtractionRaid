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

        // Tier 8.x — procedural reload/equip/unequip motion on the same KickGroup (replaces the
        // stale Mecanim clips). Serialized for fast in-Play tuning; migrates to ViewCheats once
        // the feel is locked. Signs are in KickGroup local space (Z = muzzle fwd, Y = up).
        [Header("Reload / Equip motion (Tier 8.x)")]
        [SerializeField] float _reloadDip = 0.05f;        // lower + pull back during the mag swap
        [SerializeField] float _reloadPitchAngle = 22f;   // muzzle tips as the gun cants for the swap
        [SerializeField] float _reloadRollAngle  = 14f;   // slight sideways cant
        [SerializeField] int   _reloadBobs = 2;           // mag-out / mag-in wobbles
        [SerializeField] float _equipLower = 0.07f;       // equip/unequip start lowered by this
        [SerializeField] float _equipPitchAngle = 45f;    // ...and muzzle-down by this, rising to rest

        ParticleSystem _muzzleFlashInstance;

        // Attached delivery state — resolved at AttachDelivery() and cached.
        GameObject _attachedDelivery;
        Transform  _resolvedMuzzlePoint;

        // B1 — barrel heat glow. Renderers cached at AttachDelivery time; MPB applied per-frame
        // by SetHeat. URP Lit needs _EMISSION keyword enabled — we force it per-instance via
        // material clone (renderer.materials triggers Unity's auto-instance).
        Renderer[] _barrelRenderers;
        MaterialPropertyBlock _heatMpb;
        static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
        float _appliedHeat = -1f; // track to avoid redundant MPB writes

        // Procedural pose state — local (not RaidState): purely visual feedback. Recoil kick +
        // reload/equip/unequip motion compose onto _recoilKickTarget (KickGroup) each frame.
        Vector3 _kickRestLocalPos;
        Quaternion _kickRestLocalRot;
        bool    _kickRestCached;
        float   _kickElapsed;
        float   _kickDuration;

        enum PhaseMotion { None, Reload, Equip, Unequip }
        PhaseMotion _phaseMotion;
        float _phaseElapsed;
        float _phaseDuration;

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

            // B1 — cache barrel renderers + enable emission keyword per-instance. Auto-instances
            // material via .materials accessor — keyword change isolated to this weapon view.
            _barrelRenderers = _attachedDelivery.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < _barrelRenderers.Length; i++)
            {
                var mats = _barrelRenderers[i].materials; // triggers instance clone
                for (int m = 0; m < mats.Length; m++)
                    if (mats[m] != null) mats[m].EnableKeyword("_EMISSION");
            }
            _heatMpb = new MaterialPropertyBlock();
            _appliedHeat = -1f;
        }

        /// <summary>
        /// B1 — push <see cref="WeaponEntityState.HeatLevel"/> from state each frame.
        /// Drives barrel emission color glow (quadratic ramp — early heat ≈ no visible glow,
        /// late heat = bright). Called by <c>PlayerPresenter.LateTick</c>.
        /// </summary>
        public void SetHeat(float heatLevel)
        {
            if (_barrelRenderers == null || _barrelRenderers.Length == 0 || _heatMpb == null) return;
            // Skip writes коли heat didn't change meaningfully — cheap optimization for cold-barrel idle.
            if (Mathf.Abs(heatLevel - _appliedHeat) < 0.005f) return;
            _appliedHeat = heatLevel;

            var cfg = DevCheats.Config?.BarrelHeat;
            if (cfg == null) return;

            // Quadratic ramp — visible glow only у upper half of heat.
            float t = heatLevel * heatLevel;
            var color = cfg.BarrelEmissionColor * (cfg.BarrelEmissionIntensity * t);
            _heatMpb.SetColor(EmissionColorId, color);

            for (int i = 0; i < _barrelRenderers.Length; i++)
                if (_barrelRenderers[i] != null) _barrelRenderers[i].SetPropertyBlock(_heatMpb);
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
        public void PlayEquip(float duration)   => StartPhaseMotion(PhaseMotion.Equip, duration);
        public void PlayUnequip(float duration) => StartPhaseMotion(PhaseMotion.Unequip, duration);
        public void PlayReload(float duration)  => StartPhaseMotion(PhaseMotion.Reload, duration);
        public void PlayDryFire()               => _animator?.SetTrigger("DryFire");

        // ── Procedural pose (recoil kick + reload/equip motion, composed each frame) ──────────

        void CacheRest()
        {
            if (_kickRestCached || _recoilKickTarget == null) return;
            _kickRestLocalPos = _recoilKickTarget.localPosition;
            _kickRestLocalRot = _recoilKickTarget.localRotation;
            _kickRestCached = true;
        }

        void TriggerRecoilKick(float fireDuration)
        {
            if (_recoilKickTarget == null || _recoilKickDistance <= 0f) return;
            CacheRest();
            _kickDuration = Mathf.Max(0.06f, fireDuration * 0.4f);
            _kickElapsed  = 0f;
        }

        void StartPhaseMotion(PhaseMotion motion, float duration)
        {
            if (_recoilKickTarget == null) return;
            CacheRest();
            _phaseMotion   = motion;
            _phaseDuration = Mathf.Max(0.05f, duration);
            _phaseElapsed  = 0f;
        }

        void Update()
        {
            UpdatePose();
            UpdateMuzzleLightPulse();
        }

        // Recoil kick + phase motion both write the KickGroup, so compose them into one
        // pos/rot offset per frame (they'd fight if each wrote the transform directly).
        void UpdatePose()
        {
            if (!_kickRestCached || _recoilKickTarget == null) return;
            float dt = Time.deltaTime;
            Vector3 pos = Vector3.zero;
            Vector3 euler = Vector3.zero;

            // Recoil kick — short Z pull-back, ease-out snap back.
            if (_kickDuration > 0f)
            {
                _kickElapsed += dt;
                float t = Mathf.Clamp01(_kickElapsed / _kickDuration);
                float e = (1f - t) * (1f - t);
                pos.z -= _recoilKickDistance * e;
                if (t >= 1f) _kickDuration = 0f;
            }

            // Reload / equip / unequip pose motion.
            if (_phaseMotion != PhaseMotion.None && _phaseDuration > 0f)
            {
                _phaseElapsed += dt;
                float t = Mathf.Clamp01(_phaseElapsed / _phaseDuration);
                AddPhaseMotion(t, ref pos, ref euler);
                if (t >= 1f) _phaseMotion = PhaseMotion.None;
            }

            _recoilKickTarget.localPosition = _kickRestLocalPos + pos;
            _recoilKickTarget.localRotation = _kickRestLocalRot * Quaternion.Euler(euler);
        }

        void AddPhaseMotion(float t, ref Vector3 pos, ref Vector3 euler)
        {
            switch (_phaseMotion)
            {
                case PhaseMotion.Reload:
                {
                    float hump = Mathf.Sin(t * Mathf.PI);             // 0 at ends, 1 mid — dip + return
                    float bob  = Mathf.Sin(t * Mathf.PI * 2f * Mathf.Max(1, _reloadBobs)) * hump;
                    pos.y -= _reloadDip * hump;
                    pos.z -= _reloadDip * 0.5f * hump;
                    euler.x += _reloadPitchAngle * hump + bob * 4f;   // tip + mag in/out wobble
                    euler.z += _reloadRollAngle * hump;               // sideways cant
                    break;
                }
                case PhaseMotion.Equip:
                {
                    float k = 1f - t; k *= k;                         // 1 → 0, ease-out (bring up + settle)
                    pos.y -= _equipLower * k;
                    euler.x += _equipPitchAngle * k;
                    break;
                }
                case PhaseMotion.Unequip:
                {
                    float k = t * t;                                  // 0 → 1, ease-in (lower away)
                    pos.y -= _equipLower * k;
                    euler.x += _equipPitchAngle * k;
                    break;
                }
            }
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
