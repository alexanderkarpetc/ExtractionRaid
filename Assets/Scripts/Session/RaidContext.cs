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
        public float MinAimDistance;

        public static AimConfig Default => new AimConfig
        {
            AimSplitEnabled = true,
            AimFollowMultiplier = 1f,
            AdsAimFollowMultiplier = 1.5f,
            RecoilRecoveryMultiplier = 1f,
            AdsRecoilRecoveryMultiplier = 1.5f,
            MinAimDistance = 1.5f,
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

    public struct StaggerConfig
    {
        public bool  Enabled;
        public float DurationLight;
        public float DurationHeavy;
        public float DurationHeadshot;
        public float HeavyDamageThreshold;
        public bool  AIShootingLockout;

        public static StaggerConfig Default => new StaggerConfig
        {
            Enabled              = true,
            DurationLight        = 0.25f,
            DurationHeavy        = 0.5f,
            DurationHeadshot     = 0.6f,
            HeavyDamageThreshold = 0.3f,
            AIShootingLockout    = true,
        };
    }

    public struct ArmorConfig
    {
        public bool  ForceNoArmor;
        public bool  ForceMaxArmor;
        public float DamageReductionK;
        public float RicochetChance;

        public static ArmorConfig Default => new ArmorConfig
        {
            ForceNoArmor     = false,
            ForceMaxArmor    = false,
            DamageReductionK = 30f,
            RicochetChance   = 0.4f,
        };
    }

    public struct FOVConfig
    {
        public bool  Enabled;
        public bool  ForceShowAllBots;
        public float NearRadius;
        public float FarRadius;
        public float Angle;
        public bool  OcclusionEnabled;

        public static FOVConfig Default => new FOVConfig
        {
            Enabled          = true,
            ForceShowAllBots = false,
            NearRadius       = 6f,
            FarRadius        = 25f,
            Angle            = 130f,
            OcclusionEnabled = true,
        };
    }

    public struct MovementConfig
    {
        public float MoveSpeedMultiplier;
        public float AdsMoveSpeedMultiplier;

        public static MovementConfig Default => new MovementConfig
        {
            MoveSpeedMultiplier    = 1f,
            AdsMoveSpeedMultiplier = 0.7f,
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
        public readonly ICoreDefinitionRegistry CoreDefinitions;
        public readonly AimConfig AimConfig;
        public readonly ShootingConfig ShootingConfig;
        public readonly StaggerConfig StaggerConfig;
        public readonly ArmorConfig ArmorConfig;
        public readonly FOVConfig FOVConfig;
        public readonly MovementConfig MovementConfig;

        public RaidContext(float deltaTime, IRaidEvents events, ITimeAdapter time,
            IInputAdapter input, INavMeshAdapter navMesh, IPhysicsAdapter physics = null,
            IGrenadePositionAdapter grenadePositions = null,
            ICoreDefinitionRegistry coreDefinitions = null,
            AimConfig? aimConfig = null,
            ShootingConfig? shootingConfig = null,
            StaggerConfig? staggerConfig = null,
            ArmorConfig? armorConfig = null,
            FOVConfig? fovConfig = null,
            MovementConfig? movementConfig = null)
        {
            DeltaTime = deltaTime;
            Events = events;
            Time = time;
            Input = input;
            NavMesh = navMesh;
            Physics = physics;
            GrenadePositions = grenadePositions;
            CoreDefinitions = coreDefinitions;
            AimConfig = aimConfig ?? AimConfig.Default;
            ShootingConfig = shootingConfig ?? ShootingConfig.Default;
            StaggerConfig = staggerConfig ?? StaggerConfig.Default;
            ArmorConfig = armorConfig ?? ArmorConfig.Default;
            FOVConfig = fovConfig ?? FOVConfig.Default;
            MovementConfig = movementConfig ?? MovementConfig.Default;
        }
    }
}
