using Dev;
using UnityEngine;

namespace View
{
    public class WeaponView : MonoBehaviour
    {
        [SerializeField] Transform _muzzlePoint;
        [SerializeField] ParticleSystem _muzzleFlashPrefab;
        [SerializeField] Animator _animator;

        // Tier 8 Wave B: optional socket for payload mesh attachment (e.g., barrel for
        // Ballistic, emitter cone for Laser). Null on prefabs that don't yet expose one
        // → AttachPayload silently no-ops. Wired explicitly in Inspector per V-Q6.
        [SerializeField] Transform _payloadMount;

        // Gunplay A.5 — optional Point Light child of weapon prefab. If present, pulses
        // bright at PlayMuzzleFlash and decays to 0 over MuzzleVfx.LightDuration. Null = no pulse.
        // Wired via Inspector — typical setup: child GO с Light component on muzzle.
        [SerializeField] Light _muzzleLight;

        // Light pulse state — driven by DevCheats.Config.MuzzleVfx.
        float _muzzleLightElapsedUnscaled;
        float _muzzleLightDuration;
        float _muzzleLightPeak;

        // Tier 8 Wave D: delivery body that receives procedural recoil kick on Fire.
        // Mecanim clips authored against the legacy SM_Wep_AssaultRifle_01 transform
        // paths went stale after Wave B/C symmetric pivot — code-driven kick replaces
        // them across all archetypes without per-prefab clip authoring. Optional;
        // null = no procedural feedback. Real animator-driven anim is Tier 9 polish.
        [SerializeField] Transform _deliveryBody;
        [SerializeField] float     _recoilKickDistance = 0.04f;

        ParticleSystem _muzzleFlashInstance;
        GameObject _attachedPayload;

        // Procedural recoil state — local (not RaidState): purely visual feedback.
        Vector3 _bodyRestLocalPos;
        bool    _bodyRestCached;
        float   _kickElapsed;
        float   _kickDuration;

        static readonly int SpeedParam = Animator.StringToHash("Speed");

        public Transform MuzzlePoint  => _muzzlePoint;
        public Transform PayloadMount => _payloadMount;

        /// <summary>
        /// Spawns <paramref name="payloadPrefab"/> as a child of <see cref="PayloadMount"/>.
        /// Replaces any previously attached payload. No-op if either side is null.
        /// </summary>
        public void AttachPayload(GameObject payloadPrefab)
        {
            if (_attachedPayload != null)
            {
                Destroy(_attachedPayload);
                _attachedPayload = null;
            }

            if (payloadPrefab == null || _payloadMount == null)
                return;

            _attachedPayload = Instantiate(payloadPrefab, _payloadMount);
            _attachedPayload.transform.localPosition = Vector3.zero;
            _attachedPayload.transform.localRotation = Quaternion.identity;
        }

        public void PlayMuzzleFlash()
        {
            if (_muzzleFlashPrefab != null && _muzzlePoint != null)
            {
                if (_muzzleFlashInstance == null)
                {
                    _muzzleFlashInstance = Instantiate(_muzzleFlashPrefab, _muzzlePoint);
                    _muzzleFlashInstance.transform.localPosition = Vector3.zero;
                    _muzzleFlashInstance.transform.localRotation = Quaternion.identity;
                }
                _muzzleFlashInstance.Play();
            }

            // Gunplay A.5 — light pulse layer on muzzle moment. Per-prefab _muzzleLight
            // optional; DevCheats config drives intensity/duration/range/color.
            TriggerMuzzleLightPulse();
        }

        void TriggerMuzzleLightPulse()
        {
            var cfg = DevCheats.Config?.MuzzleVfx;
            if (cfg == null || !cfg.LightEnabled || cfg.LightDuration <= 0f) return;

            // Auto-create a Point Light child of MuzzlePoint if prefab didn't wire one —
            // zero-config visible pulse за умовчанням; per-prefab override через Inspector.
            if (_muzzleLight == null && _muzzlePoint != null)
            {
                var lightGO = new GameObject("MuzzleLight (auto)");
                lightGO.transform.SetParent(_muzzlePoint, false);
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
            if (_deliveryBody == null || _recoilKickDistance <= 0f) return;
            if (!_bodyRestCached)
            {
                _bodyRestLocalPos = _deliveryBody.localPosition;
                _bodyRestCached = true;
            }
            // Recovery scaled to fire interval — fast cadence (Auto, FireInterval=0.2)
            // gets snappy short kicks; slow cadence (Single, 0.4) gets longer kicks.
            // Floored so very fast cadences still feel a kick.
            _kickDuration = Mathf.Max(0.06f, fireDuration * 0.4f);
            _kickElapsed  = 0f;
            _deliveryBody.localPosition = _bodyRestLocalPos + new Vector3(0f, 0f, -_recoilKickDistance);
        }

        void Update()
        {
            UpdateRecoilKick();
            UpdateMuzzleLightPulse();
        }

        void UpdateRecoilKick()
        {
            if (!_bodyRestCached || _kickDuration <= 0f) return;

            _kickElapsed += Time.deltaTime;
            if (_kickElapsed >= _kickDuration)
            {
                _deliveryBody.localPosition = _bodyRestLocalPos;
                _kickDuration = 0f;
                return;
            }

            float t = _kickElapsed / _kickDuration;
            float eased = (1f - t) * (1f - t); // ease-out quad → snap back
            _deliveryBody.localPosition = _bodyRestLocalPos + new Vector3(0f, 0f, -_recoilKickDistance * eased);
        }

        void UpdateMuzzleLightPulse()
        {
            if (_muzzleLight == null || _muzzleLightDuration <= 0f) return;

            // Unscaled — light pulse must remain perceptible during hit pause.
            _muzzleLightElapsedUnscaled += Time.unscaledDeltaTime;
            if (_muzzleLightElapsedUnscaled >= _muzzleLightDuration)
            {
                _muzzleLight.intensity = 0f;
                _muzzleLight.enabled = false;
                _muzzleLightDuration = 0f;
                return;
            }

            float t = _muzzleLightElapsedUnscaled / _muzzleLightDuration;
            float eased = (1f - t) * (1f - t); // ease-out quad
            _muzzleLight.intensity = _muzzleLightPeak * eased;
        }

        /// <summary>
        /// Plays an animation clip at adjusted speed so it finishes in exactly <paramref name="duration"/> seconds.
        /// Uses the Animator "Speed" float parameter as Speed Multiplier on action states.
        /// Idle state should NOT use Speed parameter (multiplier = 1).
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
            if (_animator.runtimeAnimatorController == null) return 0f;

            foreach (var clip in _animator.runtimeAnimatorController.animationClips)
                if (clip.name == clipName) return clip.length;

            return 0f;
        }
    }
}
