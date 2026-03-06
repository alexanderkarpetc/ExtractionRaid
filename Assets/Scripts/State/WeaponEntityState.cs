namespace State
{
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

        // Runtime state
        public float LastFireTime;

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
                LastFireTime = -999f,
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
                LastFireTime = -999f,
            };
        }
    }
}
