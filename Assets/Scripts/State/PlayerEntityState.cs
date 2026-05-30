using Constants;
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
        public EId DeployTargetId;
        public EId NpcTargetId;
        public EId BuilderTargetId;

        // Extraction state — ExtractionSystem owns these. ActiveExtractionPointId is None
        // while the player is outside every zone; entering a zone sets it and ticks
        // ExtractionProgress01 from 0 → 1 over ExtractionConstants.ExtractDurationSeconds.
        // Leaving the zone resets both. The HUD presenter polls these each frame.
        public EId ActiveExtractionPointId;
        public float ExtractionProgress01;

        public int ActiveQuickSlot = -1;
        public bool QuickSlotHeld;

        public bool IsADS;
        public float AdsBlend; // 0 = hip, 1 = fully ADS — lerped each tick

        public bool IsInventoryOpen; // set by InventoryUI (Tab-opened inventory without loot target)
        public bool IsQuestLogOpen;

        public float Stamina;
        public float MaxStamina;
        public bool IsSprinting;
        public float LastSprintStopTime;
        // Hysteresis lockout: set true when Stamina hits 0, cleared only when Stamina
        // recovers past the configured recovery threshold (StaminaConfig.ExhaustionRecoveryRatio).
        // Prevents stutter-sprint at empty. Drives ring blink in WorldStaminaRing.
        public bool IsExhausted;

        public bool AreHandsBusy => IsUsingMedkit || IsUsingBandage || IsInGrenadeMode;
        // Gameplay-pausing modal states only. Inventory / Loot / Builder are
        // explicitly NOT here: with the UTK migration the player keeps walking
        // and (за cursor-not-over-UI gate) shooting while ці modal'и open.
        // Attack/ADS suppression for clicks landing на UI handled у InputAdapter
        // через IsPointerOverUi flag.
        public bool IsInMenu => IsQuestLogOpen
            || CraftTargetId != EId.None
            || DeployTargetId != EId.None
            || NpcTargetId != EId.None;

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
                Stamina = StaminaConstants.MaxStamina,
                MaxStamina = StaminaConstants.MaxStamina,
            };
        }
    }
}
