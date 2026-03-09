using UnityEngine;

namespace View
{
    public class WeaponView : MonoBehaviour
    {
        [SerializeField] Transform _muzzlePoint;
        [SerializeField] ParticleSystem _muzzleFlashPrefab;

        ParticleSystem _muzzleFlashInstance;

        public Transform MuzzlePoint => _muzzlePoint;

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
    }
}
