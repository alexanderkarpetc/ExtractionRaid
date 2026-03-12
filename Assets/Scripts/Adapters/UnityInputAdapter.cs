using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Adapters
{
    public class UnityInputAdapter : IInputAdapter, IDisposable
    {
        static readonly Key[] HotbarKeys =
        {
            Key.Digit1, Key.Digit2, Key.Digit3,
            Key.Digit4, Key.Digit5, Key.Digit6,
            Key.Digit7, Key.Digit8, Key.Digit9,
        };

        readonly InputSystem_Actions _actions;
        Camera _camera;
        Transform _muzzlePoint;

        public UnityInputAdapter()
        {
            _actions = new InputSystem_Actions();
            _actions.Player.Enable();
        }

        public Vector2 MoveInput => _actions.Player.Move.ReadValue<Vector2>();
        public bool SprintPressed => _actions.Player.Sprint.IsPressed();
        public bool AttackPressed => _actions.Player.Attack.IsPressed();

        public Vector3 AimWorldPoint
        {
            get
            {
                if (_camera == null) return Vector3.zero;

                var mousePos = Mouse.current?.position.ReadValue() ?? Vector2.zero;
                var ray = _camera.ScreenPointToRay(new Vector3(mousePos.x, mousePos.y, 0f));
                var plane = new Plane(Vector3.up, Vector3.zero);

                if (plane.Raycast(ray, out var dist))
                    return ray.GetPoint(dist);

                return Vector3.zero;
            }
        }

        public Vector3 MuzzleWorldPoint =>
            _muzzlePoint != null ? _muzzlePoint.position : Vector3.zero;

        public Vector3 CameraWorldPosition =>
            _camera != null ? _camera.transform.position : Vector3.zero;

        public int HotbarSlotPressed
        {
            get
            {
                var kb = Keyboard.current;
                if (kb == null) return -1;

                for (int i = 0; i < HotbarKeys.Length; i++)
                {
                    if (kb[HotbarKeys[i]].wasPressedThisFrame)
                        return i;
                }

                return -1;
            }
        }

        public bool InventoryTogglePressed
        {
            get
            {
                var kb = Keyboard.current;
                return kb != null && kb[Key.Tab].wasPressedThisFrame;
            }
        }

        public bool PickUpPressed
        {
            get
            {
                var kb = Keyboard.current;
                return kb != null && kb[Key.F].wasPressedThisFrame;
            }
        }

        public bool ReloadPressed
        {
            get
            {
                var kb = Keyboard.current;
                return kb != null && kb[Key.R].wasPressedThisFrame;
            }
        }

        public bool DodgePressed
        {
            get
            {
                var kb = Keyboard.current;
                return kb != null && kb[Key.Space].wasPressedThisFrame;
            }
        }

        public bool GrenadePressed
        {
            get
            {
                var kb = Keyboard.current;
                return kb != null && kb[Key.G].wasPressedThisFrame;
            }
        }

        public bool AttackJustReleased => _actions.Player.Attack.WasReleasedThisFrame();

        public void SetCamera(Camera camera)
        {
            _camera = camera;
        }

        public void SetMuzzlePoint(Transform muzzlePoint)
        {
            _muzzlePoint = muzzlePoint;
        }

        public void Dispose()
        {
            _actions.Player.Disable();
            _actions.Dispose();
        }
    }
}
