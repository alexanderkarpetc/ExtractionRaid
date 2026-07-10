using State;
using UnityEngine;

namespace Constants
{
    public enum BotWeaponSource
    {
        // Use the Payload + Delivery configured on this asset directly.
        FromThisConfig,
        // Roll a weapon from the assigned Equipment config's weighted weapon pool.
        RandomFromEquipment,
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

        [Header("Equipment")]
        [Tooltip("Optional shared equipment pools (weapon / helmet / armor) rolled per spawn. " +
                 "Helmet & armor pools, when non-empty, override the fixed Armor fields below. " +
                 "Weapon pool is used only when Weapon Source is RandomFromEquipment.")]
        [SerializeField] BotEquipmentConfigAsset _equipment;
        [Tooltip("FromThisConfig: every bot of this type carries the weapon configured above. " +
                 "RandomFromEquipment: each bot rolls a weapon from the Equipment config's weapon pool.")]
        [SerializeField] BotWeaponSource _weaponSource = BotWeaponSource.FromThisConfig;

        [Tooltip("Optional loot table — what this bot drops beyond its weapon and armor " +
                 "(ammo mix, specific items, category loot). Leave null for the default drop.")]
        [SerializeField] BotLootConfigAsset _loot;

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

        [Header("Grenade (combat tuning — carried count lives in the Loot config)")]
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
            // Equipment pools: weapon pool only when explicitly opted in; helmet/armor pools
            // override the fixed Armor fields whenever the equipment config supplies them.
            WeightedWeapon[] weaponPool = _equipment != null && _weaponSource == BotWeaponSource.RandomFromEquipment
                ? _equipment.BuildWeaponPool()
                : null;
            WeightedId[] helmetPool    = _equipment != null ? _equipment.BuildHelmetPool()    : null;
            WeightedId[] bodyArmorPool = _equipment != null ? _equipment.BuildBodyArmorPool() : null;

            // Loot table (optional). Null asset → null rules → LootSystem uses the legacy default drop.
            AmmoLootRule?      ammoLoot        = _loot != null ? _loot.BuildAmmoRule()      : (AmmoLootRule?)null;
            ItemCountRule[]    guaranteedItems = _loot != null ? _loot.BuildItemRules()     : null;
            CategoryLootRule[] categoryLoot    = _loot != null ? _loot.BuildCategoryRules() : null;

            // Grenades the bot carries (thrown in combat + dropped as leftovers) — count lives
            // in the loot config. 0..0 when no loot config, so asset bots carry none by default.
            int grenadeMin = _loot != null ? _loot.grenadeMinCount : 0;
            int grenadeMax = _loot != null ? _loot.grenadeMaxCount : 0;

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
                grenadeCooldown: _grenadeCooldown,
                grenadeMinThrowDist: _grenadeMinThrowDist,
                meleeAttackRadius: _meleeAttackRadius,
                meleeAttackDamage: _meleeAttackDamage,
                meleeAttackCooldown: _meleeAttackCooldown,
                helmetDefinitionId: string.IsNullOrEmpty(_helmetDefinitionId) ? null : _helmetDefinitionId,
                bodyArmorDefinitionId: string.IsNullOrEmpty(_bodyArmorDefinitionId) ? null : _bodyArmorDefinitionId,
                behaviors: _behaviors,
                weaponPool: weaponPool,
                helmetPool: helmetPool,
                bodyArmorPool: bodyArmorPool,
                ammoLoot: ammoLoot,
                guaranteedItems: guaranteedItems,
                categoryLoot: categoryLoot,
                grenadeMinCount: grenadeMin,
                grenadeMaxCount: grenadeMax);
        }

        WeaponConfiguration BuildWeaponConfig() =>
            ComposeWeapon(_payload, _delivery, _exotic, _weaponRarity, _magazineAmmo);

        /// <summary>
        /// Composes a <see cref="WeaponConfiguration"/> from Payload + Delivery cores — the same
        /// assembly the player's Weapon Builder produces, so bot weapon stats flow through the
        /// identical pipeline. Null cores fall back to a plain Ballistic + Auto rifle so a
        /// half-authored asset still spawns a functional bot. Shared by this asset and
        /// <see cref="BotEquipmentConfigAsset"/> so the fallback logic lives in one place.
        /// </summary>
        public static WeaponConfiguration ComposeWeapon(
            PayloadCoreDefinition payload, DeliveryCoreDefinition delivery,
            ExoticModDefinition exotic, RarityTier rarity, int magazineAmmo)
        {
            string payloadId  = payload  != null ? payload.Id  : "BallisticRound";
            string deliveryId = delivery != null ? delivery.Id : "Auto";
            ExoticModInstance? exoticInstance = exotic != null
                ? new ExoticModInstance(exotic.Id)
                : (ExoticModInstance?)null;

            return new WeaponConfiguration(
                payload:        new PayloadCoreInstance(payloadId, rarity),
                delivery:       new DeliveryCoreInstance(deliveryId, rarity),
                exotic:         exoticInstance,
                ammoInMagazine: magazineAmmo);
        }

        public void ApplyToRegistry()
        {
            BotConstants.RegisterOrOverride(ToBotTypeConfig());
            if (_bodyPrefab != null)
                BotConstants.SetBodyPrefabOverride(_typeId, _bodyPrefab);
        }
    }
}
