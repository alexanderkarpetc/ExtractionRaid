using State;
using UnityEngine;

namespace Constants
{
    public enum BotWeaponPreset
    {
        Pistol,
        Rifle,
        Shotgun,
    }

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
        [SerializeField] BotWeaponPreset _weapon = BotWeaponPreset.Rifle;

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
                weaponConfig: BotConstants.GetWeaponPreset(_weapon),
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

        public void ApplyToRegistry()
        {
            BotConstants.RegisterOrOverride(ToBotTypeConfig());
            if (_bodyPrefab != null)
                BotConstants.SetBodyPrefabOverride(_typeId, _bodyPrefab);
        }
    }
}
