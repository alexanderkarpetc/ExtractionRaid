using UnityEngine;

namespace State
{
    public enum WeaponPhase : byte
    {
        Ready,
        Firing,
        Cooldown,
        Equipping,
        Unequipping,
        Reloading,
    }

    public class WeaponEntityState
    {
        public EId Id;
        public string PrefabId;

        // Shooting parameters
        public float FireInterval;
        public float ProjectileSpeed;
        public float ProjectileLifetime;
        public float ProjectileDamage;
        public int ProjectilesPerShot;
        public float SpreadAngle;

        // Aiming parameters
        public float ConeHalfAngle;
        public float BodyRotationSpeed;
        public float AimFollowSharpness;

        // Recoil parameters
        public float RecoilKickBack;
        public float RecoilKickSide;
        public float RecoilRecoverySpeed;

        // Equip/unequip durations
        public float EquipTime;
        public float UnequipTime;

        // Ammo parameters
        public string AmmoType;
        public int MagazineSize;
        public int AmmoInMagazine;
        public float ReloadTime;

        // Runtime state
        public float LastFireTime;
        public WeaponPhase Phase;
        public float PhaseStartTime;
        public Vector3 RecoilOffset;

        public static WeaponEntityState CreateDefault(EId id)
        {
            return new WeaponEntityState
            {
                Id = id,
                PrefabId = "Weapon_Rifle",
                FireInterval = 0.2f,
                ProjectileSpeed = 20f,
                ProjectileLifetime = 3f,
                ProjectileDamage = 10f,
                ProjectilesPerShot = 1,
                SpreadAngle = 0f,
                ConeHalfAngle = 45f,
                BodyRotationSpeed = 270,
                AimFollowSharpness = 10f,
                RecoilKickBack = 4.5f,
                RecoilKickSide = 2.25f,
                RecoilRecoverySpeed = 4f,
                EquipTime = 0.3f,
                UnequipTime = 0.2f,
                AmmoType = "Ammo_Rifle",
                MagazineSize = 30,
                AmmoInMagazine = 30,
                ReloadTime = 2.0f,
                LastFireTime = -999f,
                Phase = WeaponPhase.Ready,
                PhaseStartTime = 0f,
            };
        }

        public static WeaponEntityState CreateSecondary(EId id)
        {
            return new WeaponEntityState
            {
                Id = id,
                PrefabId = "Weapon_Shotgun",
                FireInterval = 0.6f,
                ProjectileSpeed = 30f,
                ProjectileLifetime = 2f,
                ProjectileDamage = 8f,
                ProjectilesPerShot = 7,
                SpreadAngle = 30f,
                ConeHalfAngle = 20f,
                BodyRotationSpeed = 180f,
                AimFollowSharpness = 5f,
                RecoilKickBack = 12.0f,
                RecoilKickSide = 6.75f,
                RecoilRecoverySpeed = 2f,
                EquipTime = 0.4f,
                UnequipTime = 0.25f,
                AmmoType = "Ammo_Shotgun",
                MagazineSize = 5,
                AmmoInMagazine = 5,
                ReloadTime = 2.5f,
                LastFireTime = -999f,
                Phase = WeaponPhase.Ready,
                PhaseStartTime = 0f,
            };
        }
    }
}
