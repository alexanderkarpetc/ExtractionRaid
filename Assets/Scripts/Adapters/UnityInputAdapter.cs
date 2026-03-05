using System;
using UnityEngine;

namespace Adapters
{
    public class UnityInputAdapter : IInputAdapter, IDisposable
    {
        readonly InputSystem_Actions _actions;

        public UnityInputAdapter()
        {
            _actions = new InputSystem_Actions();
            _actions.Player.Enable();
        }

        public Vector2 MoveInput => _actions.Player.Move.ReadValue<Vector2>();
        public bool SprintPressed => _actions.Player.Sprint.IsPressed();

        public void Dispose()
        {
            _actions.Player.Disable();
            _actions.Dispose();
        }
    }
}
