using UnityEngine;

namespace Adapters
{
    public interface IInputAdapter
    {
        Vector2 MoveInput { get; }
        bool SprintPressed { get; }
        bool AttackPressed { get; }
        Vector3 AimWorldPoint { get; }
        Vector3 MuzzleWorldPoint { get; }
        int HotbarSlotPressed { get; }
    }
}
