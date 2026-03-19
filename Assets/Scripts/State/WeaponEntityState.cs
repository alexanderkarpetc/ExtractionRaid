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
        public float RecoilKickForward;
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

        public static WeaponEntityState CreateRifle(EId id)
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
                RecoilKickForward = 2f,
                RecoilKickSide = 1.5f,
                RecoilRecoverySpeed = 2f,
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

        public static WeaponEntityState CreateShotgun(EId id)
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
                RecoilKickForward = 3f,
                RecoilKickSide = 6f,
                RecoilRecoverySpeed = 3f,
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

        public static WeaponEntityState CreatePistol(EId id)
        {
            return new WeaponEntityState
            {
                Id = id,
                PrefabId = "Weapon_Pistol",
                FireInterval = 0.4f,
                ProjectileSpeed = 25f,
                ProjectileLifetime = 2.5f,
                ProjectileDamage = 15f,
                ProjectilesPerShot = 1,
                SpreadAngle = 0f,
                ConeHalfAngle = 35f,
                BodyRotationSpeed = 300f,
                AimFollowSharpness = 15f,
                RecoilKickForward = 1.5f,
                RecoilKickSide = 1f,
                RecoilRecoverySpeed = 4f,
                EquipTime = 0.2f,
                UnequipTime = 0.15f,
                AmmoType = "Ammo_Pistol",
                MagazineSize = 12,
                AmmoInMagazine = 12,
                ReloadTime = 1.5f,
                LastFireTime = -999f,
                Phase = WeaponPhase.Ready,
                PhaseStartTime = 0f,
            };
        }

        public static WeaponEntityState CreateFromDefinitionId(EId id, string definitionId)
        {
            return definitionId switch
            {
                "Rifle" => CreateRifle(id),
                "Shotgun" => CreateShotgun(id),
                "Pistol" => CreatePistol(id),
                _ => null,
            };
        }
    }
}
