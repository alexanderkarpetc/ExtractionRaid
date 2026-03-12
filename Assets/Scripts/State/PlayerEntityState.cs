using UnityEngine;

namespace State
{
    public class PlayerEntityState
    {
        public const int HotbarSize = 9;

        public EId Id;
        public Vector3 Position;
        public Vector3 Velocity;
        public Vector3 FacingDirection;
        public Vector3 AimDirection;
        public Vector3 RawAimPoint;
        public Vector3 WeaponAimPoint;
        public WeaponEntityState EquippedWeapon;

        public WeaponEntityState[] Hotbar = new WeaponEntityState[HotbarSize];
        public int SelectedHotbarSlot = -1;
        public int PendingHotbarSlot = -1;

        public bool IsRolling;
        public Vector3 RollDirection;
        public float RollStartTime;
        public float RollCooldownEndTime;

        public bool IsInGrenadeMode;
        public bool GrenadeThrowCharging;
        public float GrenadeTargetDistance;

        public static PlayerEntityState Create(EId id, Vector3 spawnPosition)
        {
            return new PlayerEntityState
            {
                Id = id,
                Position = spawnPosition,
                Velocity = Vector3.zero,
                FacingDirection = Vector3.forward,
                AimDirection = Vector3.forward,
                RawAimPoint = spawnPosition + Vector3.forward,
                WeaponAimPoint = spawnPosition + Vector3.forward,
            };
        }
    }
}
