using UnityEngine;

namespace Adapters
{
    public interface IInputAdapter
    {
        Vector2 MoveInput { get; }
        bool SprintPressed { get; }
    }
}
