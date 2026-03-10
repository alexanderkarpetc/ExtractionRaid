namespace State
{
    public enum WeaponPhase : byte
    {
        Ready,
        Firing,
        Cooldown,
        Equipping,
        Unequipping,
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

        // Equip/unequip durations
        public float EquipTime;
        public float UnequipTime;

        // Runtime state
        public float LastFireTime;
        public WeaponPhase Phase;
        public float PhaseStartTime;

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
                EquipTime = 0.3f,
                UnequipTime = 0.2f,
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
                EquipTime = 0.4f,
                UnequipTime = 0.25f,
                LastFireTime = -999f,
                Phase = WeaponPhase.Ready,
                PhaseStartTime = 0f,
            };
        }
    }
}
