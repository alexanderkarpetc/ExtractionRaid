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

        // Grenade
        public readonly int   GrenadeCount;
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
            BotBehaviorFlags behaviors = BotBehaviorFlags.Patrol | BotBehaviorFlags.Chase | BotBehaviorFlags.Shoot)
        {
            TypeId = typeId;
            PrefabId = prefabId;
            BodyPrefabId = bodyPrefabId;
            WeaponConfig = weaponConfig;
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

        // Layer mask for vision raycasts — only these layers block line of sight.
        // Default: layer 0 ("Default"). Set via BotConstants or a ScriptableObject
        // if your obstacles live on a different layer.
        public static LayerMask VisionBlockingMask = 1 << 0;

        // --- Patrol ---
        public const float WaypointArrivalDistance = 1f;
        public const float PatrolWaitTime = 2f;

        // --- Bot weapon presets (Tier 4a — Builder configurations) ---
        // Bots inherit от Builder system: Payload + Delivery composition through
        // WeaponSyncSystem.BuildWeaponForItem. Stats (Damage, FireInterval, Spread,
        // Penetration, ArmorDamage, BleedChance, Headshot multiplier) all derived from cores.

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
                     | BotBehaviorFlags.ThrowGrenade
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
