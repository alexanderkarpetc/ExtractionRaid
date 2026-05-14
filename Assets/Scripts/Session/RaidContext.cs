using Adapters;
using State;
using UnityEngine;

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

    /// <summary>
    /// Player-centric global cap on bot fire range. Closes "off-screen damage without telegraph"
    /// UX gap — bots whose distance to player exceeds <see cref="MaxEngagementRadius"/> won't
    /// emit <c>WantsToFire</c> even if their per-type <c>EngageRange</c> allows it.
    /// Default = disabled (0) so unit tests + legacy paths keep working unchanged.
    /// </summary>
    public struct BotEngagementConfig
    {
        public bool  Enabled;
        public float MaxEngagementRadius;

        public static BotEngagementConfig Default => new BotEngagementConfig
        {
            Enabled = false,
            MaxEngagementRadius = 0f,
        };
    }

    /// <summary>
    /// Laser-archetype tunables. Two concerns:
    /// 1. Parabolic charge → damage curve (all lasers): <c>dmg = min + (1-min) × chargeRatio^power</c>.
    /// 2. Laser+Scatter signature mechanic: charge ratio modulates both spread cone and projectile
    ///    lifetime (range) — low charge = wide cone + short range, full charge = narrow cone + long range.
    /// </summary>
    public struct LaserConfig
    {
        public float ChargeDamageMin;        // 0 → multiplier at zero charge (default 0.1)
        public float ChargeDamagePower;      // curve exponent — 1 = linear, 2 = parabolic (default 2.0)
        public float ShotgunMinSpreadMult;   // multiplier on SpreadAngle at full charge (default 0.15)
        public float ShotgunMaxSpreadMult;   // multiplier on SpreadAngle at zero charge (default 1.5)
        public float ShotgunMinLifetimeMult; // multiplier on lifetime at zero charge (default 0.3)
        public float ShotgunMaxLifetimeMult; // multiplier on lifetime at full charge (default 1.5)
        // A4 — per-delivery charge time multipliers. Effective time = payload.ChargeTime × mult.
        public float SingleActionChargeMult; // Pistol — fast (default 0.6)
        public float AutoChargeMult;         // Rifle — baseline (default 1.0)
        public float ScatterChargeMult;      // Shotgun — slow (default 1.5)

        public static LaserConfig Default => new LaserConfig
        {
            ChargeDamageMin        = 0.1f,
            ChargeDamagePower      = 2f,
            ShotgunMinSpreadMult   = 0.15f,
            ShotgunMaxSpreadMult   = 1.5f,
            ShotgunMinLifetimeMult = 0.3f,
            ShotgunMaxLifetimeMult = 1.5f,
            SingleActionChargeMult = 0.6f,
            AutoChargeMult         = 1.0f,
            ScatterChargeMult      = 1.5f,
        };

        /// <summary>Parabolic charge → damage multiplier. Computes once; safe for all archetypes (ballistic chargeRatio=1 → returns 1).</summary>
        public float ChargeDamageMultiplier(float chargeRatio)
        {
            float r = Mathf.Clamp01(chargeRatio);
            float curve = Mathf.Pow(r, ChargeDamagePower);
            return ChargeDamageMin + (1f - ChargeDamageMin) * curve;
        }

        /// <summary>Charge-time multiplier за delivery <see cref="FiringPattern"/>. Single/unknown → pistol.</summary>
        public float ChargeTimeMultiplierFor(FiringPattern pattern) => pattern switch
        {
            FiringPattern.Auto    => AutoChargeMult,
            FiringPattern.Scatter => ScatterChargeMult,
            _                     => SingleActionChargeMult,
        };
    }

    /// <summary>
    /// Ballistic Rifle signature mechanic (B1). Sustained Ballistic+Auto fire grows weapon heat,
    /// heat multiplies spread (parabolic curve). Heat decays continuously via WeaponHeatSystem.
    /// Only Ballistic+Auto path increments — other archetypes have no contribution.
    /// </summary>
    public struct BarrelHeatConfig
    {
        public bool  Enabled;
        public int   MaxHeatShots;        // saturation point (shots to reach heat=1)
        public float DecayPerSecond;      // heat decay (1 → 0 over 1/decay seconds)
        public float HeatCurvePower;      // curve exponent for heat → spread mult
        public float MaxSpreadMultiplier; // spread multiplier at heat=1

        public static BarrelHeatConfig Default => new BarrelHeatConfig
        {
            Enabled             = false, // disabled у tests by default — opt-in
            MaxHeatShots        = 12,
            DecayPerSecond      = 0.5f,
            HeatCurvePower      = 1.8f,
            MaxSpreadMultiplier = 3f,
        };

        /// <summary>Effective spread multiplier за heat. <c>1 + curve × (max-1)</c>.</summary>
        public float SpreadMultiplier(float heatLevel)
        {
            float h = Mathf.Clamp01(heatLevel);
            float curve = Mathf.Pow(h, HeatCurvePower);
            return 1f + curve * (MaxSpreadMultiplier - 1f);
        }

        /// <summary>Heat increment per shot. <c>1/MaxHeatShots</c>.</summary>
        public float HeatPerShot => MaxHeatShots > 0 ? 1f / MaxHeatShots : 0f;
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
        public readonly BotEngagementConfig BotEngagementConfig;
        public readonly LaserConfig LaserConfig;
        public readonly BarrelHeatConfig BarrelHeatConfig;

        public RaidContext(float deltaTime, IRaidEvents events, ITimeAdapter time,
            IInputAdapter input, INavMeshAdapter navMesh, IPhysicsAdapter physics = null,
            IGrenadePositionAdapter grenadePositions = null,
            ICoreDefinitionRegistry coreDefinitions = null,
            AimConfig? aimConfig = null,
            ShootingConfig? shootingConfig = null,
            StaggerConfig? staggerConfig = null,
            ArmorConfig? armorConfig = null,
            FOVConfig? fovConfig = null,
            MovementConfig? movementConfig = null,
            BotEngagementConfig? botEngagementConfig = null,
            LaserConfig? laserConfig = null,
            BarrelHeatConfig? barrelHeatConfig = null)
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
            BotEngagementConfig = botEngagementConfig ?? BotEngagementConfig.Default;
            LaserConfig = laserConfig ?? LaserConfig.Default;
            BarrelHeatConfig = barrelHeatConfig ?? BarrelHeatConfig.Default;
        }
    }
}
