using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Adapters
{
    public class UnityInputAdapter : IInputAdapter, IDisposable
    {
        readonly InputSystem_Actions _actions;
        Camera _camera;

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

        public void SetCamera(Camera camera)
        {
            _camera = camera;
        }

        public void Dispose()
        {
            _actions.Player.Disable();
            _actions.Dispose();
        }
    }
}
