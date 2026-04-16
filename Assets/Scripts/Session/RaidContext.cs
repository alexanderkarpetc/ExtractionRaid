using Adapters;

namespace Session
{
    public struct AimConfig
    {
        public bool AimSplitEnabled;
        public float AimFollowMultiplier;
        public float AdsAimFollowMultiplier;
        public float RecoilRecoveryMultiplier;
        public float AdsRecoilRecoveryMultiplier;

        public static AimConfig Default => new AimConfig
        {
            AimSplitEnabled = true,
            AimFollowMultiplier = 1f,
            AdsAimFollowMultiplier = 1.5f,
            RecoilRecoveryMultiplier = 1f,
            AdsRecoilRecoveryMultiplier = 1.5f,
        };
    }

    public struct ShootingConfig
    {
        public float ProjectileSpawnHeight;
        public bool ParallaxCorrection;
        public float ConvergenceBlend;
        public bool ConvergenceAimUp;
        public float AimUpHeightRatio;
        public float ProjectileSpeedMultiplier;
        public float DamageMultiplier;
        public bool NoRecoil;
        public float RecoilMultiplier;
        public float AdsRecoilMultiplier;
        public float RecoilForwardMultiplier;
        public float RecoilSideMultiplier;
        public bool InfiniteAmmo;
        public bool MuzzleBlockEnabled;
        public float MuzzleBlockBackoff;

        public static ShootingConfig Default => new ShootingConfig
        {
            ProjectileSpawnHeight = 0.3f,
            ParallaxCorrection = true,
            ConvergenceBlend = 0.3f,
            ConvergenceAimUp = true,
            AimUpHeightRatio = 0.85f,
            ProjectileSpeedMultiplier = 1f,
            DamageMultiplier = 1f,
            NoRecoil = false,
            RecoilMultiplier = 0.5f,
            AdsRecoilMultiplier = 0.6f,
            RecoilForwardMultiplier = 1f,
            RecoilSideMultiplier = 1f,
            InfiniteAmmo = false,
            MuzzleBlockEnabled = true,
            MuzzleBlockBackoff = 0.1f,
        };
    }

    public readonly struct RaidContext
    {
        public readonly float DeltaTime;
        public readonly IRaidEvents Events;
        public readonly ITimeAdapter Time;
        public readonly IInputAdapter Input;
        public readonly INavMeshAdapter NavMesh;
        public readonly IPhysicsAdapter Physics;
        public readonly IGrenadePositionAdapter GrenadePositions;
        public readonly AimConfig AimConfig;
        public readonly ShootingConfig ShootingConfig;

        public RaidContext(float deltaTime, IRaidEvents events, ITimeAdapter time,
            IInputAdapter input, INavMeshAdapter navMesh, IPhysicsAdapter physics = null,
            IGrenadePositionAdapter grenadePositions = null, AimConfig? aimConfig = null,
            ShootingConfig? shootingConfig = null)
        {
            DeltaTime = deltaTime;
            Events = events;
            Time = time;
            Input = input;
            NavMesh = navMesh;
            Physics = physics;
            GrenadePositions = grenadePositions;
            AimConfig = aimConfig ?? AimConfig.Default;
            ShootingConfig = shootingConfig ?? ShootingConfig.Default;
        }
    }
}
