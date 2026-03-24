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
        int QuickSlotPressed { get; }
        int QuickSlotHeld { get; }
        bool AdsPressed { get; }
        bool AttackJustReleased { get; }
        Vector3 CameraWorldPosition { get; }
        /// <summary>
        /// 3D convergence point: where camera ray through cursor hits a physics collider.
        /// Null when cursor is over empty space (no collider hit).
        /// </summary>
        Vector3? ConvergencePoint { get; }

        /// <summary>
        /// The collider hit by the convergence raycast (if any).
        /// Used to determine target bounds for aim-up adjustment.
        /// </summary>
        Collider ConvergenceCollider { get; }
    }
}
