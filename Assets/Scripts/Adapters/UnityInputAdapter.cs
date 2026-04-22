using System;
using UnityEngine;
using UnityEngine.InputSystem;
using View;

namespace Adapters
{
    public class UnityInputAdapter : IInputAdapter, IDisposable
    {
        static readonly Key[] HotbarKeys =
        {
            Key.Digit1, Key.Digit2,
        };

        static readonly Key[] QuickSlotKeys =
        {
            Key.Digit3, Key.Digit4, Key.Digit5,
            Key.Digit6, Key.Digit7, Key.Digit8, Key.Digit9,
        };

        readonly InputSystem_Actions _actions;
        Camera _camera;
        Transform _muzzlePoint;

        // Per-frame convergence cache (single Physics.Raycast, reused by both properties)
        int _convergenceFrame = -1;
        Vector3? _cachedConvergence;
        Collider _cachedConvergenceCollider;

        // Weapon aim screen pos (set by AimingSystem, includes recoil)
        Vector2 _weaponAimScreenPos;

        public bool BlockGameplayInput { get; set; }

        public UnityInputAdapter()
        {
            _actions = new InputSystem_Actions();
            _actions.Player.Enable();
        }

        public Vector2 MoveInput => BlockGameplayInput ? Vector2.zero : _actions.Player.Move.ReadValue<Vector2>();
        public bool SprintPressed => !BlockGameplayInput && _actions.Player.Sprint.IsPressed();
        public bool AttackPressed => !BlockGameplayInput && _actions.Player.Attack.IsPressed();

        void UpdateConvergence()
        {
            int frame = Time.frameCount;
            if (frame == _convergenceFrame) return;
            _convergenceFrame = frame;
            _cachedConvergence = null;
            _cachedConvergenceCollider = null;

            if (_camera == null) return;

            // Use weapon aim screen position (includes recoil) instead of raw mouse
            var aimPos = _weaponAimScreenPos;
            var ray = _camera.ScreenPointToRay(new Vector3(aimPos.x, aimPos.y, 0f));

            if (!Physics.Raycast(ray, out var hit, 200f,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide))
                return;

            // Skip player's own collider
            if (_muzzlePoint != null && hit.transform.root == _muzzlePoint.root)
                return;

            // Skip other projectiles
            if (hit.collider.GetComponent<ProjectileView>() != null)
                return;

            _cachedConvergence = hit.point;
            _cachedConvergenceCollider = hit.collider;
        }

        public Vector3 AimWorldPoint
        {
            get
            {
                if (_camera == null) return Vector3.zero;

                // Always use ground plane for smooth crosshair tracking (no snapping)
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

        public Transform IgnoreCollisionRoot =>
            _muzzlePoint != null ? _muzzlePoint.root : null;

        public Vector3 CameraWorldPosition =>
            _camera != null ? _camera.transform.position : Vector3.zero;

        public Vector3? ConvergencePoint
        {
            get
            {
                UpdateConvergence();
                return _cachedConvergence;
            }
        }

        public Collider ConvergenceCollider
        {
            get
            {
                UpdateConvergence();
                return _cachedConvergenceCollider;
            }
        }

        public int HotbarSlotPressed
        {
            get
            {
                if (BlockGameplayInput) return -1;
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
                if (BlockGameplayInput) return false;
                var kb = Keyboard.current;
                return kb != null && kb[Key.F].wasPressedThisFrame;
            }
        }

        public bool InteractPressed
        {
            get
            {
                if (BlockGameplayInput) return false;
                var kb = Keyboard.current;
                return kb != null && kb[Key.E].wasPressedThisFrame;
            }
        }

        public bool ReloadPressed
        {
            get
            {
                if (BlockGameplayInput) return false;
                var kb = Keyboard.current;
                return kb != null && kb[Key.R].wasPressedThisFrame;
            }
        }

        public bool DodgePressed
        {
            get
            {
                if (BlockGameplayInput) return false;
                var kb = Keyboard.current;
                return kb != null && kb[Key.Space].wasPressedThisFrame;
            }
        }

        public bool GrenadePressed
        {
            get
            {
                if (BlockGameplayInput) return false;
                var kb = Keyboard.current;
                return kb != null && kb[Key.G].wasPressedThisFrame;
            }
        }

        public int QuickSlotPressed
        {
            get
            {
                if (BlockGameplayInput) return -1;
                var kb = Keyboard.current;
                if (kb == null) return -1;

                for (int i = 0; i < QuickSlotKeys.Length; i++)
                {
                    if (kb[QuickSlotKeys[i]].wasPressedThisFrame)
                        return i;
                }

                return -1;
            }
        }

        public int QuickSlotHeld
        {
            get
            {
                if (BlockGameplayInput) return -1;
                var kb = Keyboard.current;
                if (kb == null) return -1;

                for (int i = 0; i < QuickSlotKeys.Length; i++)
                {
                    if (kb[QuickSlotKeys[i]].IsPressed())
                        return i;
                }

                return -1;
            }
        }

        public bool AdsPressed => !BlockGameplayInput && (Mouse.current?.rightButton.isPressed ?? false);

        public bool AttackJustReleased => !BlockGameplayInput && _actions.Player.Attack.WasReleasedThisFrame();

        public void SetCamera(Camera camera)
        {
            _camera = camera;
        }

        public void SetMuzzlePoint(Transform muzzlePoint)
        {
            _muzzlePoint = muzzlePoint;
        }

        public Vector2 WorldToScreen(Vector3 worldPoint)
        {
            if (_camera == null) return Vector2.zero;
            var sp = _camera.WorldToScreenPoint(worldPoint);
            return new Vector2(sp.x, sp.y);
        }

        public void SetWeaponAimScreenPos(Vector2 screenPos)
        {
            _weaponAimScreenPos = screenPos;
            // Invalidate convergence cache so next query recalculates with new aim pos
            _convergenceFrame = -1;
        }

        public void Dispose()
        {
            _actions.Player.Disable();
            _actions.Dispose();
        }
    }
}
