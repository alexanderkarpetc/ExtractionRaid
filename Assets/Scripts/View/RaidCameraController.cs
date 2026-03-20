using Dev;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace View
{
    public class RaidCameraController : MonoBehaviour
    {
        [Header("Offset")]
        [SerializeField] Vector3 _offset = new Vector3(0f, 15.6f, -11.7f);
        [SerializeField] float _pitch = 55f;

        [Header("Follow")]
        [SerializeField] float _followSpeed = 12f;

        [Header("Cursor Offset")]
        [SerializeField] float _cursorInfluence = 4f;
        [SerializeField] float _cursorSmoothing = 8f;
        [SerializeField] [Range(0f, 1f)] float _deadZone = 0.3f;

        Transform _target;
        Vector3 _cursorOffset;

        // ADS visual interpolant (view-layer only)
        float _adsAmount;
        Vignette _vignette;
        float _baseVignetteIntensity;

        public void SetTarget(Transform target)
        {
            _target = target;

            if (_target != null)
                transform.position = _target.position + _offset;
        }

        void Start()
        {
            // Cache vignette from global Volume for ADS effect
            var volume = FindFirstObjectByType<Volume>();
            if (volume != null && volume.profile.TryGet(out Vignette v))
            {
                _vignette = v;
                _baseVignetteIntensity = v.intensity.value;
            }
        }

        void LateUpdate()
        {
            if (_target == null) return;

            // Update ADS visual blend
            var player = App.App.Instance?.RaidSession?.RaidState?.PlayerEntity;
            float adsTarget = (player != null && player.IsADS) ? 1f : 0f;
            float adsSpeed = 1f / Mathf.Max(0.01f, DevCheats.AdsTransitionTime);
            _adsAmount = Mathf.MoveTowards(_adsAmount, adsTarget, Time.deltaTime * adsSpeed);

            // ADS-scaled cursor influence
            float effectiveInfluence = _cursorInfluence
                * Mathf.Lerp(1f, DevCheats.AdsCursorInfluenceMultiplier, _adsAmount);

            var desiredCursorOffset = Vector3.zero;

            var mouse = Mouse.current;
            var cam = GetComponent<Camera>();
            if (cam != null && mouse != null)
            {
                var ray = cam.ScreenPointToRay(mouse.position.ReadValue());
                var plane = new Plane(Vector3.up, _target.position);
                if (plane.Raycast(ray, out var dist))
                {
                    var mouseWorld = ray.GetPoint(dist);
                    var dir = mouseWorld - _target.position;
                    dir.y = 0f;

                    float maxRange = cam.orthographic
                        ? cam.orthographicSize
                        : Vector3.Distance(cam.transform.position, _target.position);
                    float normalizedDist = Mathf.Clamp01(dir.magnitude / maxRange);

                    float remapped = normalizedDist > _deadZone
                        ? (normalizedDist - _deadZone) / (1f - _deadZone)
                        : 0f;

                    desiredCursorOffset = dir.normalized * remapped * effectiveInfluence;
                }
            }

            _cursorOffset = Vector3.Lerp(_cursorOffset, desiredCursorOffset,
                Time.deltaTime * _cursorSmoothing);

            // ADS zoom — scale offset to bring camera closer
            float zoomFactor = Mathf.Lerp(1f, DevCheats.AdsZoomFactor, _adsAmount);
            var effectiveOffset = _offset * zoomFactor;

            var desiredPos = _target.position + _cursorOffset + effectiveOffset;
            transform.position = Vector3.Lerp(transform.position, desiredPos,
                Time.deltaTime * _followSpeed);

            transform.rotation = Quaternion.Euler(_pitch, 0f, 0f);

            // ADS vignette
            if (_vignette != null)
            {
                _vignette.intensity.value = Mathf.Lerp(
                    _baseVignetteIntensity, DevCheats.AdsVignetteIntensity, _adsAmount);
            }
        }
    }
}
