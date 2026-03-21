using UnityEngine;

namespace State
{
    public class PlayerEntityState
    {
        public const int HotbarSize = 2;

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

        public bool IsUsingBandage;
        public float BandageUseStartTime;
        public int ActiveBandageSlot = -1;

        public bool IsUsingMedkit;
        public float MedkitUseStartTime;
        public bool MedkitHealingActive;
        public int ActiveMedkitSlot = -1;
        public float MedkitHealFraction;

        public EId LootTargetId;
        public EId CraftTargetId;

        public int ActiveQuickSlot = -1;
        public bool QuickSlotHeld;

        public bool IsADS;
        public float AdsBlend; // 0 = hip, 1 = fully ADS — lerped each tick

        public bool IsInventoryOpen; // set by InventoryUI (Tab-opened inventory without loot target)

        public bool AreHandsBusy => IsUsingMedkit || IsUsingBandage || IsInGrenadeMode;
        public bool IsInMenu => IsInventoryOpen || LootTargetId != EId.None || CraftTargetId != EId.None;

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
