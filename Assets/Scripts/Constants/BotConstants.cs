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
            float maxHp = 100f,
            float healAmount = 0f, float healThreshold = 0f, float healCooldown = 0f,
            float moveSpeed = 4f, float patrolSpeed = 2f, float chaseSpeed = 5f,
            float visionRange = 30f, float visionAngle = 120f,
            float hearingRange = 6f, float targetMemoryDuration = 8f,
            float reactionTime = 0.5f, float accuracy = 0.6f, float engageRange = 20f,
            float dodgeCooldown = 0f,
            float fireInterval = 0.3f, float projectileSpeed = 20f, float projectileDamage = 10f,
            float projectileLifetime = 3f, int projectilesPerShot = 1, float spreadAngle = 5f,
            BotBehaviorFlags behaviors = BotBehaviorFlags.Patrol | BotBehaviorFlags.Chase | BotBehaviorFlags.Shoot)
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
            typeId: "Scav", prefabId: "BotView", weaponPrefabId: "Weapon_Rifle",
            maxHp: 80f, moveSpeed: 3.5f, chaseSpeed: 4f,
            visionRange: 25f, visionAngle: 110f,
            targetMemoryDuration: 5f, reactionTime: 0.8f, accuracy: 0.5f,
            fireInterval: 0.4f, projectileSpeed: 18f, projectileDamage: 8f,
            spreadAngle: 8f
        );

        public static readonly BotTypeConfig PMC = new(
            typeId: "PMC", prefabId: "BotBossView", weaponPrefabId: "Weapon_Rifle",
            healAmount: 30f, healThreshold: 0.35f, healCooldown: 15f,
            moveSpeed: 4.5f, patrolSpeed: 2.5f,
            visionRange: 35f,
            reactionTime: 0.4f, accuracy: 0.75f, engageRange: 28f,
            dodgeCooldown: 5f,
            fireInterval: 0.25f, projectileSpeed: 22f, projectileDamage: 12f,
            spreadAngle: 4f,
            behaviors: BotBehaviorFlags.Patrol | BotBehaviorFlags.Chase | BotBehaviorFlags.Shoot
                     | BotBehaviorFlags.TakeCover | BotBehaviorFlags.Heal | BotBehaviorFlags.Dodge
        );

        public static readonly BotTypeConfig Boss = new(
            typeId: "Boss", prefabId: "BotPmcView", weaponPrefabId: "Weapon_Shotgun",
            maxHp: 200f, chaseSpeed: 5.5f,
            visionRange: 40f, visionAngle: 140f,
            targetMemoryDuration: 12f, reactionTime: 0.3f, accuracy: 0.65f,
            engageRange: 15f, dodgeCooldown: 3f,
            fireInterval: 0.5f, projectileSpeed: 28f, projectileDamage: 7f,
            projectileLifetime: 2f, projectilesPerShot: 7, spreadAngle: 25f,
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
