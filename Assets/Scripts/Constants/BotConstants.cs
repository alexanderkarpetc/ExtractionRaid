using System;
using System.Collections.Generic;
using UnityEngine;

namespace Constants
{
    [Flags]
    public enum BotBehaviorFlags
    {
        None      = 0,
        Patrol    = 1 << 0,
        Chase     = 1 << 1,
        Shoot     = 1 << 2,
        TakeCover = 1 << 3,
        Heal      = 1 << 4,
        Dodge     = 1 << 5,
    }

    public readonly struct BotTypeConfig
    {
        public readonly string TypeId;
        public readonly string PrefabId;
        public readonly string WeaponPrefabId;

        // Health
        public readonly float MaxHp;
        public readonly float HealAmount;
        public readonly float HealThreshold;
        public readonly float HealCooldown;

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

        // Weapon overrides
        public readonly float FireInterval;
        public readonly float ProjectileSpeed;
        public readonly float ProjectileDamage;
        public readonly float ProjectileLifetime;
        public readonly int ProjectilesPerShot;
        public readonly float SpreadAngle;

        // Behavior
        public readonly BotBehaviorFlags Behaviors;

        public bool Has(BotBehaviorFlags flag) => (Behaviors & flag) == flag;

        public BotTypeConfig(
            string typeId, string prefabId, string weaponPrefabId,
            float maxHp, float healAmount, float healThreshold, float healCooldown,
            float moveSpeed, float patrolSpeed, float chaseSpeed,
            float visionRange, float visionAngle, float hearingRange, float targetMemoryDuration,
            float reactionTime, float accuracy, float engageRange,
            float dodgeCooldown,
            float fireInterval, float projectileSpeed, float projectileDamage,
            float projectileLifetime, int projectilesPerShot, float spreadAngle,
            BotBehaviorFlags behaviors)
        {
            TypeId = typeId;
            PrefabId = prefabId;
            WeaponPrefabId = weaponPrefabId;
            MaxHp = maxHp;
            HealAmount = healAmount;
            HealThreshold = healThreshold;
            HealCooldown = healCooldown;
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
            FireInterval = fireInterval;
            ProjectileSpeed = projectileSpeed;
            ProjectileDamage = projectileDamage;
            ProjectileLifetime = projectileLifetime;
            ProjectilesPerShot = projectilesPerShot;
            SpreadAngle = spreadAngle;
            Behaviors = behaviors;
        }
    }

    public static class BotConstants
    {
        // --- Player ---
        public const float PlayerMaxHp = 100f;

        // --- Perception tuning ---
        public const float PerceptionTickInterval = 0.2f;

        // Layer mask for vision raycasts — only these layers block line of sight.
        // Default: layer 0 ("Default"). Set via BotConstants or a ScriptableObject
        // if your obstacles live on a different layer.
        public static LayerMask VisionBlockingMask = 1 << 0;

        // --- Cover search ---
        public const float CoverSearchRadius = 10f;
        public const float MinCoverDistance = 2f;

        // --- Patrol ---
        public const float WaypointArrivalDistance = 1f;
        public const float PatrolWaitTime = 2f;

        // --- Bot type definitions ---

        public static readonly BotTypeConfig Scav = new(
            typeId: "Scav",
            prefabId: "BotView",
            weaponPrefabId: "Weapon_Rifle",
            maxHp: 80f,
            healAmount: 0f,
            healThreshold: 0f,
            healCooldown: 0f,
            moveSpeed: 3.5f,
            patrolSpeed: 2f,
            chaseSpeed: 4f,
            visionRange: 25f,
            visionAngle: 110f,
            hearingRange: 12f,
            targetMemoryDuration: 5f,
            reactionTime: 0.8f,
            accuracy: 0.5f,
            engageRange: 20f,
            dodgeCooldown: 0f,
            fireInterval: 0.4f,
            projectileSpeed: 18f,
            projectileDamage: 8f,
            projectileLifetime: 3f,
            projectilesPerShot: 1,
            spreadAngle: 8f,
            behaviors: BotBehaviorFlags.Patrol | BotBehaviorFlags.Chase | BotBehaviorFlags.Shoot
        );

        public static readonly BotTypeConfig PMC = new(
            typeId: "PMC",
            prefabId: "BotBossView",
            weaponPrefabId: "Weapon_Rifle",
            maxHp: 100f,
            healAmount: 30f,
            healThreshold: 0.35f,
            healCooldown: 15f,
            moveSpeed: 4.5f,
            patrolSpeed: 2.5f,
            chaseSpeed: 5f,
            visionRange: 35f,
            visionAngle: 120f,
            hearingRange: 18f,
            targetMemoryDuration: 8f,
            reactionTime: 0.4f,
            accuracy: 0.75f,
            engageRange: 28f,
            dodgeCooldown: 5f,
            fireInterval: 0.25f,
            projectileSpeed: 22f,
            projectileDamage: 12f,
            projectileLifetime: 3f,
            projectilesPerShot: 1,
            spreadAngle: 4f,
            behaviors: BotBehaviorFlags.Patrol | BotBehaviorFlags.Chase | BotBehaviorFlags.Shoot
                     | BotBehaviorFlags.TakeCover | BotBehaviorFlags.Heal | BotBehaviorFlags.Dodge
        );

        public static readonly BotTypeConfig Boss = new(
            typeId: "Boss",
            prefabId: "BotPmcView",
            weaponPrefabId: "Weapon_Shotgun",
            maxHp: 200f,
            healAmount: 0f,
            healThreshold: 0f,
            healCooldown: 0f,
            moveSpeed: 4f,
            patrolSpeed: 2f,
            chaseSpeed: 5.5f,
            visionRange: 40f,
            visionAngle: 140f,
            hearingRange: 25f,
            targetMemoryDuration: 12f,
            reactionTime: 0.3f,
            accuracy: 0.65f,
            engageRange: 15f,
            dodgeCooldown: 3f,
            fireInterval: 0.5f,
            projectileSpeed: 28f,
            projectileDamage: 7f,
            projectileLifetime: 2f,
            projectilesPerShot: 7,
            spreadAngle: 25f,
            behaviors: BotBehaviorFlags.Chase | BotBehaviorFlags.Shoot | BotBehaviorFlags.TakeCover
                     | BotBehaviorFlags.Dodge
        );

        static readonly Dictionary<string, BotTypeConfig> Registry = new()
        {
            { Scav.TypeId, Scav },
            { PMC.TypeId, PMC },
            { Boss.TypeId, Boss },
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
