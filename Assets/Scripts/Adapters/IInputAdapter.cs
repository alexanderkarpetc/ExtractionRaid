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

        /// <summary>
        /// Transform whose hierarchy should be ignored when raycasting for the player's
        /// own shell (capsule + body + weapon + armor). Returns the player shell root,
        /// not the weapon root — name reflects USAGE, not source. Derived from the
        /// equipped weapon's transform (which is parented under the shell).
        /// Null if no weapon is equipped.
        /// </summary>
        Transform IgnoreCollisionRoot { get; }
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

        /// <summary>
        /// Convert a world position to screen coordinates using the active camera.
        /// </summary>
        Vector2 WorldToScreen(Vector3 worldPoint);

        /// <summary>
        /// Set the screen position of the weapon aim point (with recoil applied).
        /// Convergence raycast will use this instead of raw mouse position,
        /// so that recoil affects headshot detection.
        /// </summary>
        void SetWeaponAimScreenPos(Vector2 screenPos);
    }
}
