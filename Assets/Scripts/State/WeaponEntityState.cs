namespace State
{
    public class WeaponEntityState
    {
        public EId Id;

        // Shooting parameters
        public float FireInterval;
        public float ProjectileSpeed;
        public float ProjectileLifetime;
        public float ProjectileDamage;

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
                FireInterval = 0.2f,
                ProjectileSpeed = 20f,
                ProjectileLifetime = 3f,
                ProjectileDamage = 10f,
                ConeHalfAngle = 45f,
                BodyRotationSpeed = 270,
                LastFireTime = -999f,
            };
        }
    }
}
