using System;
using System.Collections.Generic;
using State;
using UnityEngine;

namespace Constants
{
    [Flags]
    public enum BotBehaviorFlags
    {
        None         = 0,
        Patrol       = 1 << 0,
        Chase        = 1 << 1,
        Shoot        = 1 << 2,
        Heal         = 1 << 4,
        Dodge        = 1 << 5,
        ThrowGrenade = 1 << 6,
        MeleeAttack  = 1 << 7,
        // Fires continuously in the bot's current facing direction. No target tracking,
        // no rotation toward target. Used by FeedbackRange test turrets.
        FireForward  = 1 << 8,
        // Fight from cover: pick a spot the enemy can't see, run there, then cycle
        // hide → peek → shoot → duck back (TakeCoverNode, SAIN-inspired).
        TakeCover    = 1 << 9,
    }

    /// <summary>A weapon assembly paired with a relative pick weight (weighted random loadout).</summary>
    public readonly struct WeightedWeapon
    {
        public readonly WeaponConfiguration Config;
        public readonly float Weight;

        public WeightedWeapon(WeaponConfiguration config, float weight)
        {
            Config = config;
            Weight = weight > 0f ? weight : 0f;
        }
    }

    /// <summary>An item definition id paired with a relative pick weight. Null id = "no item".</summary>
    public readonly struct WeightedId
    {
        public readonly string Id;
        public readonly float  Weight;

        public WeightedId(string id, float weight)
        {
            Id = id;
            Weight = weight > 0f ? weight : 0f;
        }
    }

    public readonly struct BotTypeConfig
    {
        public readonly string TypeId;
        public readonly string PrefabId;       // shell prefab (View + Collider)
        public readonly string BodyPrefabId;   // visual body prefab (CharacterBody + mesh)

        // Tier 4a — bot weapon composition. Drives BotSpawnSystem → WeaponSyncSystem.BuildWeaponForItem.
        // Same pipeline as player; bot weapon stats are now derived from Payload + Delivery cores
        // (full Penetration / ArmorDamage / BleedChance / HeadshotMultiplier support).
        public readonly WeaponConfiguration WeaponConfig;

        // Optional weighted equipment pools (from a BotEquipmentConfigAsset). When a pool is
        // non-null/non-empty, BotSpawnSystem rolls that slot per spawn instead of using the
        // fixed WeaponConfig / HelmetDefinitionId / BodyArmorDefinitionId below.
        public readonly WeightedWeapon[] WeaponPool;
        public readonly WeightedId[]     HelmetPool;
        public readonly WeightedId[]     BodyArmorPool;

        // Optional loot table (from a BotLootConfigAsset). When null, LootSystem falls
        // back to the legacy default drop (caliber ammo + carried meds/bandages).
        public readonly AmmoLootRule?     AmmoLoot;
        public readonly ItemCountRule[]   GuaranteedItems;
        public readonly CategoryLootRule[] CategoryLoot;

        public bool HasLootTable => AmmoLoot.HasValue || GuaranteedItems != null || CategoryLoot != null;

        // Health
        public readonly float MaxHp;
        public readonly float HealAmount;
        public readonly float HealThreshold;
        public readonly float HealCooldown;
        public readonly float EmergencyHealThreshold;
        public readonly float EmergencyHealDelay;
        public readonly float EmergencyHealCooldown;
        public readonly float HealSafeDelay;
        public readonly float HealSafeEnemyDistance;
        public readonly int MedkitCount;

        // Movement
        public readonly float MoveSpeed;
        public readonly float PatrolSpeed;
        public readonly float ChaseSpeed;

        // Perception
        public readonly float VisionRange;
        public readonly float VisionAngle;
        public readonly float HearingRange;
        public readonly float TargetMemoryDuration;

        // Combat
        public readonly float ReactionTime;
        public readonly float Accuracy;
        public readonly float EngageRange;

        // Dodge
        public readonly float DodgeCooldown;

        // Grenade — GrenadeCount is the built-in/legacy carried count. Asset bots instead
        // source the count from their loot config as a per-spawn range (GrenadeMin/MaxCount);
        // BotSpawnSystem prefers the range when GrenadeMaxCount > 0. GrenadeCooldown /
        // GrenadeMinThrowDist are combat tuning (throwing), not loot.
        public readonly int   GrenadeCount;
        public readonly int   GrenadeMinCount;
        public readonly int   GrenadeMaxCount;
        public readonly float GrenadeCooldown;
        public readonly float GrenadeMinThrowDist;

        // Melee attack (Horde-mode zombie behaviour). Direct HP damage on cooldown
        // when a target enters MeleeAttackRadius. No projectile, no armor pipeline —
        // simple contact damage for crowd-shooting tests.
        public readonly float MeleeAttackRadius;
        public readonly float MeleeAttackDamage;
        public readonly float MeleeAttackCooldown;

        // Armor
        public readonly string HelmetDefinitionId;
        public readonly string BodyArmorDefinitionId;

        // Durability the armor SPAWNS with — and therefore DROPS with — as a fraction of the
        // item's max (models a bot that's already seen combat). 1 = pristine. Rolled per spawn
        // in BotSpawnSystem; the Meta region-loot sim rolls the same range so looted armor
        // isn't always full.
        public readonly float HelmetDurabilityMin;
        public readonly float HelmetDurabilityMax;
        public readonly float BodyArmorDurabilityMin;
        public readonly float BodyArmorDurabilityMax;

        // Behavior
        public readonly BotBehaviorFlags Behaviors;

        public bool Has(BotBehaviorFlags flag) => (Behaviors & flag) == flag;

        public BotTypeConfig(
            string typeId, string prefabId, WeaponConfiguration weaponConfig,
            string bodyPrefabId = "CharacterBody",
            float maxHp = 100f,
            float healAmount = 0f, float healThreshold = 0f, float healCooldown = 0f,
            float emergencyHealThreshold = 0.3f, float emergencyHealDelay = 1.5f,
            float emergencyHealCooldown = 8f, float healSafeDelay = 3f, float healSafeEnemyDistance = 10f,
            int medkitCount = 0,
            float moveSpeed = 4f, float patrolSpeed = 2f, float chaseSpeed = 5f,
            float visionRange = 30f, float visionAngle = 120f,
            float hearingRange = 6f, float targetMemoryDuration = 8f,
            float reactionTime = 0.5f, float accuracy = 0.6f, float engageRange = 20f,
            float dodgeCooldown = 0f,
            int grenadeCount = 0, float grenadeCooldown = 0f, float grenadeMinThrowDist = 5f,
            float meleeAttackRadius = 1.5f, float meleeAttackDamage = 10f, float meleeAttackCooldown = 1f,
            string helmetDefinitionId = null, string bodyArmorDefinitionId = null,
            BotBehaviorFlags behaviors = BotBehaviorFlags.Patrol | BotBehaviorFlags.Chase | BotBehaviorFlags.Shoot,
            WeightedWeapon[] weaponPool = null, WeightedId[] helmetPool = null, WeightedId[] bodyArmorPool = null,
            AmmoLootRule? ammoLoot = null, ItemCountRule[] guaranteedItems = null, CategoryLootRule[] categoryLoot = null,
            int grenadeMinCount = 0, int grenadeMaxCount = 0,
            float helmetDurabilityMin = 1f, float helmetDurabilityMax = 1f,
            float bodyArmorDurabilityMin = 1f, float bodyArmorDurabilityMax = 1f)
        {
            TypeId = typeId;
            PrefabId = prefabId;
            BodyPrefabId = bodyPrefabId;
            WeaponConfig = weaponConfig;
            WeaponPool = weaponPool;
            HelmetPool = helmetPool;
            BodyArmorPool = bodyArmorPool;
            AmmoLoot = ammoLoot;
            GuaranteedItems = guaranteedItems;
            CategoryLoot = categoryLoot;
            MaxHp = maxHp;
            HealAmount = healAmount;
            HealThreshold = healThreshold;
            HealCooldown = healCooldown;
            EmergencyHealThreshold = emergencyHealThreshold;
            EmergencyHealDelay = emergencyHealDelay;
            EmergencyHealCooldown = emergencyHealCooldown;
            HealSafeDelay = healSafeDelay;
            HealSafeEnemyDistance = healSafeEnemyDistance;
            MedkitCount = medkitCount;
            MoveSpeed = moveSpeed;
            PatrolSpeed = patrolSpeed;
            ChaseSpeed = chaseSpeed;
            VisionRange = visionRange;
            VisionAngle = visionAngle;
            HearingRange = hearingRange;
            TargetMemoryDuration = targetMemoryDuration;
            ReactionTime = reactionTime;
            Accuracy = accuracy;
            EngageRange = engageRange;
            DodgeCooldown = dodgeCooldown;
            GrenadeCount = grenadeCount;
            GrenadeMinCount = grenadeMinCount;
            GrenadeMaxCount = grenadeMaxCount;
            HelmetDurabilityMin = helmetDurabilityMin;
            HelmetDurabilityMax = helmetDurabilityMax;
            BodyArmorDurabilityMin = bodyArmorDurabilityMin;
            BodyArmorDurabilityMax = bodyArmorDurabilityMax;
            GrenadeCooldown = grenadeCooldown;
            GrenadeMinThrowDist = grenadeMinThrowDist;
            MeleeAttackRadius = meleeAttackRadius;
            MeleeAttackDamage = meleeAttackDamage;
            MeleeAttackCooldown = meleeAttackCooldown;
            HelmetDefinitionId = helmetDefinitionId;
            BodyArmorDefinitionId = bodyArmorDefinitionId;
            Behaviors = behaviors;
        }
    }

    public static class BotConstants
    {
        // --- Player ---
        public const float PlayerMaxHp = 100f;
        public const float PlayerEyeHeight = 1.0f;

        // --- Perception tuning ---
        public const float PerceptionTickInterval = 0.2f;

        // --- Graduated vision (TLOU-style awareness accumulator) ---
        // Within InstantFraction of VisionRange detection is immediate; beyond it,
        // detection takes DetectTimeMin..DetectTimeMax seconds scaled by distance
        // (TLOU used ~1-2 s for an unaware NPC at range). Peripheral targets (outer
        // band of the cone) detect slower. Awareness decays when sight is broken.
        public const float VisionInstantFraction     = 0.35f;
        public const float VisionDetectTimeMin       = 0.15f;
        public const float VisionDetectTimeMax       = 1.1f;
        public const float PeripheralAngleFraction   = 0.6f;   // of half-angle; beyond = peripheral
        public const float PeripheralDetectTimeMult  = 1.6f;
        public const float CombatDetectTimeMult      = 0.25f;  // already tracking a target → notice much faster
        public const float VisionAwarenessDecayPerSec = 0.5f;
        // 360° close-presence sense — humans notice someone standing next to them
        // regardless of facing (TLOU widened the cone at close range for the same reason).
        public const float CloseSenseRadius = 2.5f;

        // --- Hearing (noise tiers + gunshots) ---
        // Player noise radius: config.HearingRange is the base for normal movement.
        // Slow movement is quieter, sprinting much louder, gunshots are map-scale events.
        public const float SneakSpeedThreshold = 2.0f;   // m/s — below this movement is "quiet"
        public const float SneakNoiseMult      = 0.45f;
        public const float SprintNoiseMult     = 2.2f;
        public const float GunshotHearingRange = 40f;
        // A shot is "recent" for this long — must be >= PerceptionTickInterval so a
        // perception tick never misses a shot fired between ticks.
        public const float GunshotRecencyWindow = 0.25f;

        // --- Sound localization error ---
        // Heard-but-unseen contacts store a fuzzed position, not a GPS pin: error radius
        // scales with distance (louder = easier to localize). Damage from an unseen
        // shooter gives a direction-quality fix, not an exact one.
        public const float HeardPosErrorFraction   = 0.2f;   // of distance, movement noise
        public const float GunshotPosErrorFraction = 0.1f;   // of distance, gunshots are easier to place
        public const float DamagePosError          = 2.5f;   // meters, flat

        // --- Trigger discipline (burst fire) ---
        // Humans fire 2-5 round bursts with 0.3-0.9 s pauses, not a metronomic stream.
        // Aggression (per-bot personality) lengthens bursts and shortens pauses.
        public const int   BurstShotsMin = 2;
        public const int   BurstShotsMax = 5;
        public const float BurstPauseMin = 0.35f;
        public const float BurstPauseMax = 0.9f;

        // --- Aim settle ---
        // Accuracy ramps from StartMult -> 1 over AimSettleTime once the target is visible;
        // resets when the target re-appears after ResetUnseenTime out of sight. First
        // shots miss more — the classic Halo/Bioshock fairness trick, and it reads human.
        public const float AimSettleTime             = 0.9f;
        public const float AimSettleResetUnseenTime  = 1.2f;
        public const float AimSettleStartAccuracyMult = 0.45f;
        // Accuracy penalties: shooting while moving fast, or right after taking a hit.
        public const float MovingAccuracyMult           = 0.75f;
        public const float MovingAccuracySpeedThreshold = 1.5f;  // m/s
        public const float RecentDamageAccuracyMult     = 0.85f;
        public const float RecentDamageAccuracyWindow   = 1.5f;  // s

        // --- Reload ---
        // Bots consume magazine ammo and reload (infinite reserves). Tactical reload
        // when the mag runs low and the target is out of sight — like a player topping
        // up between peeks.
        public const float TacticalReloadFraction = 0.3f;

        // --- Heal cast ---
        // Medkit takes time; the bot retreats and cannot fire while casting — gives the
        // player the same counterplay window the player's own medkit cast has.
        public const float HealCastTime            = 2.0f;
        public const float HealRetreatSpeedFraction = 0.6f;

        // --- Grenade scatter ---
        // Grenades land near the last known position, not on it — error grows the longer
        // the target has been unseen.
        public const float GrenadeScatterBase          = 1.5f;
        public const float GrenadeScatterPerUnseenSec  = 0.4f;
        public const float GrenadeScatterMax           = 4f;

        // --- Fight-from-cover (TakeCoverNode, SAIN-inspired) ---
        // Cover search: sample a ring of candidate points around the bot, snap each to
        // the navmesh, and keep the ones the enemy's eye can't draw a line to. Scored
        // by run distance, with a penalty for sitting too close to the enemy and a
        // bonus when the head-height ray is blocked too (full cover vs torso-only).
        public static readonly float[] CoverSearchRadii = { 2.5f, 5f, 8f, 11f };
        public const int   CoverSearchDirections     = 12;
        public const float CoverNavSampleDistance    = 1.5f;
        public const float CoverMinEnemyDistance     = 4f;    // reject spots this close to the enemy
        public const float CoverPreferredEnemyDistance = 10f; // closer than this → scored down
        public const float CoverEnemyClosePenaltyWeight = 1.5f;
        public const float CoverFullCoverBonus       = 3f;    // score meters credited for head-height cover
        public const float CoverMaxEngageFraction    = 0.9f;  // spots must stay inside EngageRange × this
        public const float CoverSearchCooldown       = 1.5f;  // after a failed search — no raycast storms

        // Line-of-sight probe heights. The enemy is the player, so the "can they see
        // me there" ray starts at PlayerEyeHeight. Torso blocked = usable cover;
        // head blocked too = full cover (preferred — there is no crouch to hide the rest).
        public const float CoverBodyCheckHeight = 1.1f;
        public const float CoverHeadCheckHeight = 1.7f;

        // Peek: lateral step-out spot next to the cover point with a clear line to the
        // enemy. A cover point with no workable peek side is rejected outright — the
        // point of cover is shooting from it, not turtling.
        public const float CoverPeekOffset      = 1.6f;
        public const float CoverPeekSpeedFraction = 0.8f;     // of ChaseSpeed while stepping out/back
        public const float CoverPeekTimeout     = 2f;         // can't reach the peek spot → duck back

        // Hide/expose cycle. Hold is how long the bot stays fully hidden between peeks;
        // exposure is how long it fires before ducking back. Both windows are cut short
        // by taking a hit or starting a reload, and aggression shortens hold times.
        public const float CoverHoldTimeMin   = 1.2f;
        public const float CoverHoldTimeMax   = 2.8f;
        public const float CoverExposeTimeMin = 1.5f;
        public const float CoverExposeTimeMax = 3f;
        public const float CoverReloadHoldExtension = 0.4f;   // keep hiding while the mag refills

        // Fire on the move: while running to cover with eyes on the target, bots keep
        // shooting with a heavy accuracy penalty — running silently reads as passive.
        public const float CoverMoveFireAccuracyMult = 0.5f;

        // Only fight from cover while contact is fresh; stale memory falls through to
        // Chase/Search instead of camping an empty map.
        public const float CoverEngageMemoryTime = 4f;

        // Shot while "hidden" → the spot is compromised even though the raycast model
        // still calls it cover. Blacklist it briefly so the re-search picks elsewhere
        // (SAIN's "spotted point" mechanic, 2 s / ~1.4 m there).
        public const float CoverSpotBlacklistRadius   = 2f;
        public const float CoverSpotBlacklistDuration = 3f;

        // Movement / validation plumbing.
        public const int   CoverMaxPathCorners       = 32;
        public const float CoverRepathInterval       = 1f;
        public const float CoverCornerArrivalDistance = 0.5f;
        public const float CoverArriveDistance       = 0.6f;
        public const float CoverStuckAbandonTime     = 2f;    // pinned en route → drop this cover point
        public const float CoverRevalidateInterval   = 0.5f;
        public const float CoverEnemyMoveInvalidate  = 3f;    // LKP drifted this far → re-check the point

        // --- Search (after losing the target at last-known-position) ---
        public const float SearchDuration        = 4.5f;
        public const float SearchArriveDistance  = 1.5f;
        public const float SearchScanAmplitudeDeg = 80f;
        public const float SearchScanPeriod      = 2.2f;

        // --- Chase pathing (NavMesh) ---
        public const int   ChaseMaxPathCorners      = 32;
        public const float ChaseRepathInterval      = 0.75f;
        public const float ChaseRepathMoveThreshold = 2f;    // repath when LKP drifts this far from cached path target
        public const float ChaseCornerArrivalDistance = 0.5f;
        public const float ChaseArriveDistance        = 1f;

        // --- Personality (rolled once per spawn) ---
        public const float ReactionTimeMultMin = 0.85f;
        public const float ReactionTimeMultMax = 1.3f;
        public const float AccuracyMultMin     = 0.9f;
        public const float AccuracyMultMax     = 1.08f;
        public const float AggressionMin       = 0.7f;
        public const float AggressionMax       = 1.3f;

        // --- Weapon module rarity (rolled once per spawn, per module) ---
        // Bot loadouts in this file are authored as Common/Common, which made every corpse drop
        // the same grey gun. Payload and delivery are rolled independently, so a bot can carry a
        // Common receiver with a Rare barrel — rarity feeds composed stats and attachment slot
        // count, and the weapon drops with whatever it rolled.
        //
        // Only the fixed WeaponConfig is rolled. A weapon that came from a designer-authored pool
        // (BotEquipmentConfigAsset) keeps its authored rarity — someone typed that number on purpose.
        public const float BotWeaponCommonWeight   = 50f;
        public const float BotWeaponUncommonWeight = 40f;
        public const float BotWeaponRareWeight     = 10f;

        // Static environment that blocks both sight and physical projectiles.
        // Keep character/equipment layers out: actors must not become opaque walls
        // for perception, cover selection, fog of war, or sound propagation.
        public static LayerMask VisionBlockingMask = LayerMask.GetMask(
            "Default", "NonXRay", "Ground", "NavigationObstacle", "PropsStatic");

        // --- Patrol ---
        public const float WaypointArrivalDistance = 1f;

        // Wait at each waypoint is rolled in [Min, Max] — a fixed pause is the easiest
        // "robot" tell on a patrol loop.
        public const float PatrolWaitTimeMin = 1.0f;
        public const float PatrolWaitTimeMax = 3.5f;

        // --- Patrol pathing (NavMesh) ---
        public const int   PatrolMaxPathCorners        = 32;
        public const float PatrolCornerArrivalDistance = 0.4f;  // tighter than waypoint arrival — cuts corners naturally
        public const float PatrolRepathInterval        = 2f;    // periodic refresh while walking a leg
        public const float PatrolStuckRepathTime       = 0.6f;  // commanded-move with no displacement → recalc path
        public const float PatrolStuckSkipTime         = 3f;    // still pinned → give up on this waypoint, take the next

        // --- Patrol humanization ---
        // Per-leg speed scale: each stretch between waypoints walks at a slightly
        // different pace so the loop never looks metronomic.
        public const float PatrolSpeedScaleMin = 0.85f;
        public const float PatrolSpeedScaleMax = 1.1f;

        // Ease into waypoints instead of stopping dead: below SlowRadius speed lerps
        // down to MinSpeedFraction at the arrival point.
        public const float PatrolArrivalSlowRadius       = 2.5f;
        public const float PatrolArrivalMinSpeedFraction = 0.5f;

        // Steering turn rate for patrol velocity (deg/s). Heading changes sweep
        // through an arc instead of snapping to the new corner direction.
        public const float PatrolSteerTurnRateDeg = 270f;

        // Gentle Perlin wander applied to the steering direction — drifts the walk
        // line a few degrees side to side like an unfocused human gait.
        public const float PatrolWanderAmplitudeDeg = 6f;
        public const float PatrolWanderFrequency    = 0.2f;

        // Head-scan during PatrolWait: sweep facing back-and-forth ±ScanAmplitude
        // around the inbound-travel direction. Pure cosmetic — perception still uses
        // FacingDirection so this also widens the effective vision sweep at rest.
        public const float PatrolScanAmplitudeDeg = 35f;
        public const float PatrolScanPeriod       = 2.4f;

        // --- Combat humanization ---
        // Strafe while shooting: lateral movement perpendicular to target. Slow enough
        // that bots stay in their engagement spot, fast enough to look intentional.
        public const float ShootStrafeSpeedFraction = 0.35f;   // of config.ChaseSpeed
        public const float ShootStrafeMinDuration   = 1.2f;
        public const float ShootStrafeMaxDuration   = 2.6f;

        // Combat stance: open-field footwork is re-rolled every few seconds between
        // hold / advance / strafe. Strafing nonstop was the tell — bots slid sideways
        // for entire fights. Weights are relative; Aggression biases the roll toward
        // pushing in (and away from planting). Cover fights keep pure strafe.
        public const float ShootStanceMinDuration   = 1.4f;
        public const float ShootStanceMaxDuration   = 3.2f;
        public const float ShootStanceHoldWeight    = 1.0f;   // divided by Aggression
        public const float ShootStanceAdvanceWeight = 0.8f;   // multiplied by Aggression
        public const float ShootStanceStrafeWeight  = 1.2f;
        // Rolling the same stance twice in a row reads as "nothing changed" — allowed,
        // but down-weighted so consecutive repeats are the exception.
        public const float ShootStanceRepeatWeightMult = 0.4f;

        // Advance: walk in while firing, but never crawl into melee range. Stop at
        // whichever is larger — a fraction of EngageRange or the absolute floor.
        public const float ShootAdvanceSpeedFraction = 0.5f;   // of config.ChaseSpeed
        public const float ShootAdvanceStopFraction  = 0.45f;  // of config.EngageRange
        public const float ShootAdvanceStopMin       = 3.5f;   // meters

        // Aim sway: Perlin-noise-driven lateral offset on the aim point. Sized so
        // per-shot hit rate at engage range stays roughly equal to baseline accuracy.
        public const float AimSwayAmplitude = 0.45f;           // meters at the aim point
        public const float AimSwayFrequency = 0.9f;            // Hz

        // Reaction jitter: randomized addition to config.ReactionTime, rolled on each
        // target acquisition. Keeps fixed ReactionTime from feeling metronomic.
        public const float ReactionJitterMin = -0.15f;
        public const float ReactionJitterMax =  0.35f;

        // Facing turn rate (deg/s) for bot-to-target rotation when stationary or
        // strafing. Snap was the biggest "robot" tell — this gives a believable
        // human turn-around.
        public const float FacingTurnRateDeg = 540f;

        // --- Bot weapon presets (Tier 4a — Builder configurations) ---
        // Bots inherit от Builder system: Payload + Delivery composition through
        // WeaponSyncSystem.BuildWeaponForItem. Stats (Damage, FireInterval, Spread,
        // Penetration, ArmorDamage, BleedChance, Headshot multiplier) all derived from cores.

        public static void RegisterOrOverride(BotTypeConfig config)
        {
            Registry[config.TypeId] = config;
        }

        static readonly Dictionary<string, GameObject> BodyPrefabOverrides = new();

        public static void SetBodyPrefabOverride(string typeId, GameObject prefab)
        {
            if (string.IsNullOrEmpty(typeId) || prefab == null) return;
            BodyPrefabOverrides[typeId] = prefab;
        }

        public static GameObject GetBodyPrefabOverride(string typeId)
        {
            return typeId != null && BodyPrefabOverrides.TryGetValue(typeId, out var p) ? p : null;
        }

        static readonly WeaponConfiguration RifleWeapon = new(
            payload:        new PayloadCoreInstance("BallisticRound", RarityTier.Common),
            delivery:       new DeliveryCoreInstance("Auto",          RarityTier.Common),
            exotic:         null,
            ammoInMagazine: 30);

        static readonly WeaponConfiguration PistolWeapon = new(
            payload:        new PayloadCoreInstance("BallisticRound", RarityTier.Common),
            delivery:       new DeliveryCoreInstance("SingleAction",  RarityTier.Common),
            exotic:         null,
            ammoInMagazine: 12);

        static readonly WeaponConfiguration ShotgunWeapon = new(
            payload:        new PayloadCoreInstance("BallisticRound", RarityTier.Common),
            delivery:       new DeliveryCoreInstance("Scatter",       RarityTier.Common),
            exotic:         null,
            ammoInMagazine: 5);

        // Laser counterparts — used by FeedbackRange test scene targets to cover all 6 archetypes.
        static readonly WeaponConfiguration LaserPistolWeapon = new(
            payload:        new PayloadCoreInstance("LaserCharge",   RarityTier.Common),
            delivery:       new DeliveryCoreInstance("SingleAction", RarityTier.Common),
            exotic:         null,
            ammoInMagazine: 12);

        static readonly WeaponConfiguration LaserRifleWeapon = new(
            payload:        new PayloadCoreInstance("LaserCharge", RarityTier.Common),
            delivery:       new DeliveryCoreInstance("Auto",       RarityTier.Common),
            exotic:         null,
            ammoInMagazine: 30);

        static readonly WeaponConfiguration LaserShotgunWeapon = new(
            payload:        new PayloadCoreInstance("LaserCharge", RarityTier.Common),
            delivery:       new DeliveryCoreInstance("Scatter",    RarityTier.Common),
            exotic:         null,
            ammoInMagazine: 5);

        // --- Bot type definitions ---

        public static readonly BotTypeConfig Scav = new(
            typeId: "Scav", prefabId: "BotShell", weaponConfig: PistolWeapon,
            maxHp: 80f, moveSpeed: 3.5f, chaseSpeed: 4f,
            visionRange: 25f, visionAngle: 110f,
            targetMemoryDuration: 5f, reactionTime: 0.8f, accuracy: 0.5f,
            helmetDefinitionId: "Helmet_Basic"
        );

        public static readonly BotTypeConfig PMC = new(
            typeId: "PMC", prefabId: "BotShell", weaponConfig: RifleWeapon,
            healAmount: 30f, healThreshold: 0.5f, healCooldown: 15f,
            moveSpeed: 4.5f, patrolSpeed: 2.5f,
            visionRange: 35f,
            reactionTime: 0.4f, accuracy: 0.75f, engageRange: 28f,
            dodgeCooldown: 5f,
            grenadeCount: 2, grenadeCooldown: 20f, grenadeMinThrowDist: 5f,
            helmetDefinitionId: "Helmet_Basic", bodyArmorDefinitionId: "Armor_Basic",
            medkitCount: 2,
            behaviors: BotBehaviorFlags.Patrol | BotBehaviorFlags.Chase | BotBehaviorFlags.Shoot
                     | BotBehaviorFlags.Heal | BotBehaviorFlags.Dodge
                     | BotBehaviorFlags.ThrowGrenade | BotBehaviorFlags.TakeCover
        );

        public static readonly BotTypeConfig Boss = new(
            typeId: "Boss", prefabId: "BotShell", weaponConfig: ShotgunWeapon,
            maxHp: 200f, chaseSpeed: 5.5f,
            visionRange: 40f, visionAngle: 140f,
            targetMemoryDuration: 12f, reactionTime: 0.3f, accuracy: 0.65f,
            engageRange: 15f, dodgeCooldown: 3f,
            bodyArmorDefinitionId: "Armor_Basic",
            behaviors: BotBehaviorFlags.Chase | BotBehaviorFlags.Shoot
                     | BotBehaviorFlags.Dodge
        );

        public static readonly BotTypeConfig Target = new(
            typeId: "Target", prefabId: "BotShell", weaponConfig: RifleWeapon,
            maxHp: 10000f,
            visionRange: 0f, visionAngle: 0f, hearingRange: 0f,
            reactionTime: 999f, accuracy: 0f, engageRange: 0f,
            behaviors: BotBehaviorFlags.None
        );

        public static readonly BotTypeConfig TargetWeak = new(
            typeId: "TargetWeak", prefabId: "BotShell", weaponConfig: RifleWeapon,
            maxHp: 50f,
            visionRange: 0f, visionAngle: 0f, hearingRange: 0f,
            reactionTime: 999f, accuracy: 0f, engageRange: 0f,
            helmetDefinitionId: "Helmet_Basic",
            behaviors: BotBehaviorFlags.None
        );

        public static readonly BotTypeConfig TargetPatrol = new(
            typeId: "TargetPatrol", prefabId: "BotShell", weaponConfig: RifleWeapon,
            maxHp: 10000f, patrolSpeed: 3f,
            visionRange: 0f, visionAngle: 0f, hearingRange: 0f,
            reactionTime: 999f, accuracy: 0f, engageRange: 0f,
            behaviors: BotBehaviorFlags.Patrol
        );

        public static readonly BotTypeConfig TargetFast = new(
            typeId: "TargetFast", prefabId: "BotShell", weaponConfig: RifleWeapon,
            maxHp: 10000f, patrolSpeed: 6f,
            visionRange: 0f, visionAngle: 0f, hearingRange: 0f,
            reactionTime: 999f, accuracy: 0f, engageRange: 0f,
            behaviors: BotBehaviorFlags.Patrol
        );

        public static readonly BotTypeConfig TargetDodge = new(
            typeId: "TargetDodge", prefabId: "BotShell", weaponConfig: RifleWeapon,
            maxHp: 10000f, dodgeCooldown: 2f,
            visionRange: 0f, visionAngle: 0f, hearingRange: 0f,
            reactionTime: 999f, accuracy: 0f, engageRange: 0f,
            behaviors: BotBehaviorFlags.Dodge
        );

        // --- Armored target types (shooting range) ---

        public static readonly BotTypeConfig TargetLightArmor = new(
            typeId: "TargetLightArmor", prefabId: "BotShell", weaponConfig: RifleWeapon,
            maxHp: 10000f,
            visionRange: 0f, visionAngle: 0f, hearingRange: 0f,
            reactionTime: 999f, accuracy: 0f, engageRange: 0f,
            helmetDefinitionId: "Helmet_Basic",
            behaviors: BotBehaviorFlags.None
        );

        public static readonly BotTypeConfig TargetHeavyArmor = new(
            typeId: "TargetHeavyArmor", prefabId: "BotShell", weaponConfig: RifleWeapon,
            maxHp: 10000f,
            visionRange: 0f, visionAngle: 0f, hearingRange: 0f,
            reactionTime: 999f, accuracy: 0f, engageRange: 0f,
            helmetDefinitionId: "Helmet_Basic", bodyArmorDefinitionId: "Armor_Basic",
            behaviors: BotBehaviorFlags.None
        );

        public static readonly BotTypeConfig TargetGlassCannon = new(
            typeId: "TargetGlassCannon", prefabId: "BotShell", weaponConfig: RifleWeapon,
            maxHp: 50f,
            visionRange: 0f, visionAngle: 0f, hearingRange: 0f,
            reactionTime: 999f, accuracy: 0f, engageRange: 0f,
            helmetDefinitionId: "Helmet_Basic", bodyArmorDefinitionId: "Armor_Basic",
            behaviors: BotBehaviorFlags.None
        );

        public static readonly BotTypeConfig TargetTank = new(
            typeId: "TargetTank", prefabId: "BotShell", weaponConfig: RifleWeapon,
            maxHp: 200f,
            visionRange: 0f, visionAngle: 0f, hearingRange: 0f,
            reactionTime: 999f, accuracy: 0f, engageRange: 0f,
            bodyArmorDefinitionId: "Armor_Basic",
            behaviors: BotBehaviorFlags.None
        );

        // --- Kill-feel test targets (ShootingScene_KillFeel) ---
        // Low-HP variants для testing kill mechanics + ragdoll feedback. HP tiers chosen
        // so common weapons one-shot or two-shot тhem — fast iteration on death feel.

        public static readonly BotTypeConfig TargetKillFeel10 = new(
            typeId: "TargetKillFeel10", prefabId: "BotShell", weaponConfig: RifleWeapon,
            maxHp: 10f,
            visionRange: 0f, visionAngle: 0f, hearingRange: 0f,
            reactionTime: 999f, accuracy: 0f, engageRange: 0f,
            behaviors: BotBehaviorFlags.None
        );

        public static readonly BotTypeConfig TargetKillFeel25 = new(
            typeId: "TargetKillFeel25", prefabId: "BotShell", weaponConfig: RifleWeapon,
            maxHp: 25f,
            visionRange: 0f, visionAngle: 0f, hearingRange: 0f,
            reactionTime: 999f, accuracy: 0f, engageRange: 0f,
            behaviors: BotBehaviorFlags.None
        );

        public static readonly BotTypeConfig TargetKillFeel50 = new(
            typeId: "TargetKillFeel50", prefabId: "BotShell", weaponConfig: RifleWeapon,
            maxHp: 50f,
            visionRange: 0f, visionAngle: 0f, hearingRange: 0f,
            reactionTime: 999f, accuracy: 0f, engageRange: 0f,
            behaviors: BotBehaviorFlags.None
        );

        public static readonly BotTypeConfig TargetKillFeel75 = new(
            typeId: "TargetKillFeel75", prefabId: "BotShell", weaponConfig: RifleWeapon,
            maxHp: 75f,
            visionRange: 0f, visionAngle: 0f, hearingRange: 0f,
            reactionTime: 999f, accuracy: 0f, engageRange: 0f,
            behaviors: BotBehaviorFlags.None
        );

        public static readonly BotTypeConfig TargetKillFeel100 = new(
            typeId: "TargetKillFeel100", prefabId: "BotShell", weaponConfig: RifleWeapon,
            maxHp: 100f,
            visionRange: 0f, visionAngle: 0f, hearingRange: 0f,
            reactionTime: 999f, accuracy: 0f, engageRange: 0f,
            behaviors: BotBehaviorFlags.None
        );

        public static readonly BotTypeConfig TargetKillFeelPatrol = new(
            typeId: "TargetKillFeelPatrol", prefabId: "BotShell", weaponConfig: RifleWeapon,
            maxHp: 50f, patrolSpeed: 3f,
            visionRange: 0f, visionAngle: 0f, hearingRange: 0f,
            reactionTime: 999f, accuracy: 0f, engageRange: 0f,
            behaviors: BotBehaviorFlags.Patrol
        );

        public static readonly BotTypeConfig TargetKillFeelFast = new(
            typeId: "TargetKillFeelFast", prefabId: "BotShell", weaponConfig: RifleWeapon,
            maxHp: 30f, patrolSpeed: 6f,
            visionRange: 0f, visionAngle: 0f, hearingRange: 0f,
            reactionTime: 999f, accuracy: 0f, engageRange: 0f,
            behaviors: BotBehaviorFlags.Patrol
        );

        public static readonly BotTypeConfig TargetKillFeelHelmet = new(
            typeId: "TargetKillFeelHelmet", prefabId: "BotShell", weaponConfig: RifleWeapon,
            maxHp: 50f,
            visionRange: 0f, visionAngle: 0f, hearingRange: 0f,
            reactionTime: 999f, accuracy: 0f, engageRange: 0f,
            helmetDefinitionId: "Helmet_Basic",
            behaviors: BotBehaviorFlags.None
        );

        // --- Ranged-combat test target ---
        // Streamlined PMC variant — pure ranged engagement without grenade/heal/dodge
        // noise. Long vision (70m) + engage (50m) so the bot opens fire from anywhere
        // on the ranged_range scene. Helmet Basic — gives ricochet feedback. Used by
        // ShootingScene_RangedRange to isolate range/cover combat behaviour.
        public static readonly BotTypeConfig RangedTarget = new(
            typeId: "RangedTarget", prefabId: "BotShell", weaponConfig: RifleWeapon,
            maxHp: 80f, moveSpeed: 3f, chaseSpeed: 3.5f, patrolSpeed: 0f,
            visionRange: 70f, visionAngle: 120f, hearingRange: 30f,
            targetMemoryDuration: 8f, reactionTime: 0.5f, accuracy: 0.6f, engageRange: 50f,
            helmetDefinitionId: "Helmet_Basic",
            behaviors: BotBehaviorFlags.Chase | BotBehaviorFlags.Shoot | BotBehaviorFlags.TakeCover
        );

        // --- FeedbackRange targets (6 archetypes) ---
        // Stationary turrets for damage-feedback playtest. Fire continuously in current facing
        // direction (FireForward behavior — no target tracking, no rotation toward player).
        // Player walks into the firing lanes to sample hit reactions / VFX / HUD damage
        // indicators. High HP + full armor so dummies don't break during playtest. Pairs з
        // GodMode visual passthrough (DamageSystem zeroes player HP damage, fires all VFX events).
        static BotTypeConfig FeedbackTarget(string typeId, WeaponConfiguration weapon) => new(
            typeId: typeId, prefabId: "BotShell", weaponConfig: weapon,
            maxHp: 1000f, moveSpeed: 0f, chaseSpeed: 0f, patrolSpeed: 0f,
            visionRange: 0f, visionAngle: 0f, hearingRange: 0f,
            targetMemoryDuration: 0f, reactionTime: 0f, accuracy: 1f, engageRange: 0f,
            helmetDefinitionId: "Helmet_Basic", bodyArmorDefinitionId: "Armor_Basic",
            behaviors: BotBehaviorFlags.FireForward
        );

        public static readonly BotTypeConfig FeedbackTarget_BPistol  = FeedbackTarget("FeedbackTarget_BPistol",  PistolWeapon);
        public static readonly BotTypeConfig FeedbackTarget_BRifle   = FeedbackTarget("FeedbackTarget_BRifle",   RifleWeapon);
        public static readonly BotTypeConfig FeedbackTarget_BShotgun = FeedbackTarget("FeedbackTarget_BShotgun", ShotgunWeapon);
        public static readonly BotTypeConfig FeedbackTarget_LPistol  = FeedbackTarget("FeedbackTarget_LPistol",  LaserPistolWeapon);
        public static readonly BotTypeConfig FeedbackTarget_LRifle   = FeedbackTarget("FeedbackTarget_LRifle",   LaserRifleWeapon);
        public static readonly BotTypeConfig FeedbackTarget_LShotgun = FeedbackTarget("FeedbackTarget_LShotgun", LaserShotgunWeapon);

        // --- Horde-mode zombie ---
        // Always sees player (vision 999 / 360°), chases relentlessly, no ranged fire.
        // Carries PistolWeapon as a visual placeholder so WeaponPivot has something
        // mounted — swap mesh для pipe look later. weaponConfig can't be null without
        // touching BotSpawnSystem's Builder pipeline.
        public static readonly BotTypeConfig Zombie = new(
            typeId: "Zombie", prefabId: "BotShell", weaponConfig: PistolWeapon,
            maxHp: 70f, chaseSpeed: 2.8f,
            visionRange: 999f, visionAngle: 360f, hearingRange: 999f,
            targetMemoryDuration: 999f, reactionTime: 0.1f, accuracy: 0f, engageRange: 0f,
            meleeAttackRadius: 1.6f, meleeAttackDamage: 12f, meleeAttackCooldown: 1.0f,
            behaviors: BotBehaviorFlags.Chase | BotBehaviorFlags.MeleeAttack
        );

        static readonly Dictionary<string, BotTypeConfig> Registry = new()
        {
            { Scav.TypeId, Scav },
            { PMC.TypeId, PMC },
            { Boss.TypeId, Boss },
            { Target.TypeId, Target },
            { TargetWeak.TypeId, TargetWeak },
            { TargetPatrol.TypeId, TargetPatrol },
            { TargetFast.TypeId, TargetFast },
            { TargetDodge.TypeId, TargetDodge },
            { TargetLightArmor.TypeId, TargetLightArmor },
            { TargetHeavyArmor.TypeId, TargetHeavyArmor },
            { TargetGlassCannon.TypeId, TargetGlassCannon },
            { TargetTank.TypeId, TargetTank },
            { TargetKillFeel10.TypeId, TargetKillFeel10 },
            { TargetKillFeel25.TypeId, TargetKillFeel25 },
            { TargetKillFeel50.TypeId, TargetKillFeel50 },
            { TargetKillFeel75.TypeId, TargetKillFeel75 },
            { TargetKillFeel100.TypeId, TargetKillFeel100 },
            { TargetKillFeelPatrol.TypeId, TargetKillFeelPatrol },
            { TargetKillFeelFast.TypeId, TargetKillFeelFast },
            { TargetKillFeelHelmet.TypeId, TargetKillFeelHelmet },
            { Zombie.TypeId, Zombie },
            { RangedTarget.TypeId, RangedTarget },
            { FeedbackTarget_BPistol.TypeId,  FeedbackTarget_BPistol  },
            { FeedbackTarget_BRifle.TypeId,   FeedbackTarget_BRifle   },
            { FeedbackTarget_BShotgun.TypeId, FeedbackTarget_BShotgun },
            { FeedbackTarget_LPistol.TypeId,  FeedbackTarget_LPistol  },
            { FeedbackTarget_LRifle.TypeId,   FeedbackTarget_LRifle   },
            { FeedbackTarget_LShotgun.TypeId, FeedbackTarget_LShotgun },
        };

        public static BotTypeConfig GetConfig(string typeId)
        {
            return Registry[typeId];
        }

        public static bool TryGetConfig(string typeId, out BotTypeConfig config)
        {
            return Registry.TryGetValue(typeId, out config);
        }
    }
}
