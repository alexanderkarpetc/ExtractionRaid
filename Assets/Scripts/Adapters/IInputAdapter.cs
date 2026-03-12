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
        bool InventoryTogglePressed { get; }
        bool PickUpPressed { get; }
        bool ReloadPressed { get; }
        bool DodgePressed { get; }
        bool GrenadePressed { get; }
        bool AttackJustReleased { get; }
        Vector3 CameraWorldPosition { get; }
    }
}
