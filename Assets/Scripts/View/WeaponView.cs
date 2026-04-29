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

        ParticleSystem _muzzleFlashInstance;
        GameObject _attachedPayload;

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
            if (_muzzleFlashPrefab == null || _muzzlePoint == null) return;

            if (_muzzleFlashInstance == null)
            {
                _muzzleFlashInstance = Instantiate(_muzzleFlashPrefab, _muzzlePoint);
                _muzzleFlashInstance.transform.localPosition = Vector3.zero;
                _muzzleFlashInstance.transform.localRotation = Quaternion.identity;
            }

            _muzzleFlashInstance.Play();
        }

        // ── Animation triggers ─────────────────────────────────

        public void PlayFire(float duration)    => PlayClip("Fire", duration);
        public void PlayEquip(float duration)   => PlayClip("Equip", duration);
        public void PlayUnequip(float duration) => PlayClip("Unequip", duration);
        public void PlayReload(float duration)  => PlayClip("Reload", duration);
        public void PlayDryFire()               => _animator?.SetTrigger("DryFire");

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
