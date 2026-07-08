using State;
using UnityEngine;

namespace Constants
{
    [CreateAssetMenu(fileName = "BotTypeConfig", menuName = "Bots/Bot Type Config")]
    public class BotTypeConfigAsset : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] string _typeId = "Scav";
        [SerializeField] string _shellPrefabId = "BotShell";

        [Header("Visuals")]
        [Tooltip("CharacterBody prefab instantiated under the bot shell. Direct reference — bypasses Resources.Load when set.")]
        [SerializeField] GameObject _bodyPrefab;

        [Header("Weapon")]
        [Tooltip("Payload core — the projectile/ammo nature (Ballistic, Laser, ...). Drives damage, " +
                 "penetration, bleed, headshot multiplier. Leave null to fall back to Ballistic Round.")]
        [SerializeField] PayloadCoreDefinition _payload;
        [Tooltip("Delivery core — the firing mechanism (Single-Action, Auto, Scatter, ...). Drives fire " +
                 "rate, spread, magazine size, recoil. Leave null to fall back to Auto.")]
        [SerializeField] DeliveryCoreDefinition _delivery;
        [Tooltip("Optional Exotic mod. Leave null for none.")]
        [SerializeField] ExoticModDefinition _exotic;
        [Tooltip("Rarity tier applied to both cores (visual-only until per-tier stats are authored).")]
        [SerializeField] RarityTier _weaponRarity = RarityTier.Common;
        [Tooltip("Rounds loaded in the magazine at spawn.")]
        [SerializeField] int _magazineAmmo = 30;

        [Header("Health")]
        [SerializeField] float _maxHp = 100f;
        [SerializeField] float _healAmount = 0f;
        [SerializeField] float _healThreshold = 0f;
        [SerializeField] float _healCooldown = 0f;
        [SerializeField] float _emergencyHealThreshold = 0.3f;
        [SerializeField] float _emergencyHealDelay = 1.5f;
        [SerializeField] float _emergencyHealCooldown = 8f;
        [SerializeField] float _healSafeDelay = 3f;
        [SerializeField] float _healSafeEnemyDistance = 10f;
        [SerializeField] int _medkitCount = 0;

        [Header("Movement")]
        [SerializeField] float _moveSpeed = 4f;
        [SerializeField] float _patrolSpeed = 2f;
        [SerializeField] float _chaseSpeed = 5f;

        [Header("Perception")]
        [SerializeField] float _visionRange = 30f;
        [SerializeField] float _visionAngle = 120f;
        [SerializeField] float _hearingRange = 6f;
        [SerializeField] float _targetMemoryDuration = 8f;

        [Header("Combat")]
        [SerializeField] float _reactionTime = 0.5f;
        [SerializeField] float _accuracy = 0.6f;
        [SerializeField] float _engageRange = 20f;

        [Header("Dodge")]
        [SerializeField] float _dodgeCooldown = 0f;

        [Header("Grenade")]
        [SerializeField] int _grenadeCount = 0;
        [SerializeField] float _grenadeCooldown = 0f;
        [SerializeField] float _grenadeMinThrowDist = 5f;

        [Header("Melee")]
        [SerializeField] float _meleeAttackRadius = 1.5f;
        [SerializeField] float _meleeAttackDamage = 10f;
        [SerializeField] float _meleeAttackCooldown = 1f;

        [Header("Armor")]
        [SerializeField] string _helmetDefinitionId;
        [SerializeField] string _bodyArmorDefinitionId;

        [Header("Behavior")]
        [SerializeField] BotBehaviorFlags _behaviors = BotBehaviorFlags.Patrol | BotBehaviorFlags.Chase | BotBehaviorFlags.Shoot;

        public string TypeId => _typeId;
        public GameObject BodyPrefab => _bodyPrefab;

        public BotTypeConfig ToBotTypeConfig()
        {
            return new BotTypeConfig(
                typeId: _typeId,
                prefabId: _shellPrefabId,
                weaponConfig: BuildWeaponConfig(),
                bodyPrefabId: _bodyPrefab != null ? _bodyPrefab.name : "CharacterBody",
                maxHp: _maxHp,
                healAmount: _healAmount,
                healThreshold: _healThreshold,
                healCooldown: _healCooldown,
                emergencyHealThreshold: _emergencyHealThreshold,
                emergencyHealDelay: _emergencyHealDelay,
                emergencyHealCooldown: _emergencyHealCooldown,
                healSafeDelay: _healSafeDelay,
                healSafeEnemyDistance: _healSafeEnemyDistance,
                medkitCount: _medkitCount,
                moveSpeed: _moveSpeed,
                patrolSpeed: _patrolSpeed,
                chaseSpeed: _chaseSpeed,
                visionRange: _visionRange,
                visionAngle: _visionAngle,
                hearingRange: _hearingRange,
                targetMemoryDuration: _targetMemoryDuration,
                reactionTime: _reactionTime,
                accuracy: _accuracy,
                engageRange: _engageRange,
                dodgeCooldown: _dodgeCooldown,
                grenadeCount: _grenadeCount,
                grenadeCooldown: _grenadeCooldown,
                grenadeMinThrowDist: _grenadeMinThrowDist,
                meleeAttackRadius: _meleeAttackRadius,
                meleeAttackDamage: _meleeAttackDamage,
                meleeAttackCooldown: _meleeAttackCooldown,
                helmetDefinitionId: string.IsNullOrEmpty(_helmetDefinitionId) ? null : _helmetDefinitionId,
                bodyArmorDefinitionId: string.IsNullOrEmpty(_bodyArmorDefinitionId) ? null : _bodyArmorDefinitionId,
                behaviors: _behaviors);
        }

        // Composes the bot's weapon from the chosen Payload + Delivery cores — same
        // WeaponConfiguration the player's Weapon Builder produces, so bot weapon stats
        // flow through the identical assembly pipeline. Null cores fall back to a plain
        // Ballistic + Auto rifle so a half-authored asset still spawns a functional bot.
        WeaponConfiguration BuildWeaponConfig()
        {
            string payloadId  = _payload  != null ? _payload.Id  : "BallisticRound";
            string deliveryId = _delivery != null ? _delivery.Id : "Auto";
            ExoticModInstance? exotic = _exotic != null
                ? new ExoticModInstance(_exotic.Id)
                : (ExoticModInstance?)null;

            return new WeaponConfiguration(
                payload:        new PayloadCoreInstance(payloadId, _weaponRarity),
                delivery:       new DeliveryCoreInstance(deliveryId, _weaponRarity),
                exotic:         exotic,
                ammoInMagazine: _magazineAmmo);
        }

        public void ApplyToRegistry()
        {
            BotConstants.RegisterOrOverride(ToBotTypeConfig());
            if (_bodyPrefab != null)
                BotConstants.SetBodyPrefabOverride(_typeId, _bodyPrefab);
        }
    }
}
