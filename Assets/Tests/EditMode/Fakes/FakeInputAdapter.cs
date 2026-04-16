using Adapters;
using UnityEngine;

namespace Tests.EditMode.Fakes
{
    public class FakeInputAdapter : IInputAdapter
    {
        public Vector2 MoveInput { get; set; }
        public bool SprintPressed { get; set; }
        public bool AttackPressed { get; set; }
        public Vector3 AimWorldPoint { get; set; }
        public Vector3 MuzzleWorldPoint { get; set; }
        public Transform IgnoreCollisionRoot { get; set; }
        public int HotbarSlotPressed { get; set; } = -1;
        public bool InventoryTogglePressed { get; set; }
        public bool PickUpPressed { get; set; }
        public bool ReloadPressed { get; set; }
        public bool DodgePressed { get; set; }
        public bool GrenadePressed { get; set; }
        public int QuickSlotPressed { get; set; } = -1;
        public int QuickSlotHeld { get; set; } = -1;
        public bool AdsPressed { get; set; }
        public bool AttackJustReleased { get; set; }
        public Vector3 CameraWorldPosition { get; set; }
        public Vector3? ConvergencePoint { get; set; }
        public Collider ConvergenceCollider { get; set; }
        public Vector2 WorldToScreen(Vector3 worldPoint) => Vector2.zero;
        public void SetWeaponAimScreenPos(Vector2 screenPos) { }
    }
}