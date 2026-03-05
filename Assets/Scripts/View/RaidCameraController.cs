using UnityEngine;
using UnityEngine.InputSystem;

namespace View
{
    public class RaidCameraController : MonoBehaviour
    {
        [Header("Offset")]
        [SerializeField] Vector3 _offset = new Vector3(0f, 12f, -9f);
        [SerializeField] float _pitch = 55f;

        [Header("Follow")]
        [SerializeField] float _followSpeed = 8f;

        [Header("Cursor Offset")]
        [SerializeField] float _cursorInfluence = 2.5f;
        [SerializeField] float _cursorSmoothing = 5f;

        Transform _target;
        Vector3 _cursorOffset;

        public void SetTarget(Transform target)
        {
            _target = target;

            if (_target != null)
                transform.position = _target.position + _offset;
        }

        void LateUpdate()
        {
            if (_target == null) return;

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
                    desiredCursorOffset = Vector3.ClampMagnitude(dir, 1f) * _cursorInfluence;
                }
            }

            _cursorOffset = Vector3.Lerp(_cursorOffset, desiredCursorOffset,
                Time.deltaTime * _cursorSmoothing);

            var desiredPos = _target.position + _cursorOffset + _offset;
            transform.position = Vector3.Lerp(transform.position, desiredPos,
                Time.deltaTime * _followSpeed);

            transform.rotation = Quaternion.Euler(_pitch, 0f, 0f);
        }
    }
}
