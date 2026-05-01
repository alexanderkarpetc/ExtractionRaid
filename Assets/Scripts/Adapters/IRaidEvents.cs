using State;
using UnityEngine;

namespace Adapters
{
    public interface IRaidEvents
    {
        void RaidStarted();
        void RaidEnded();
        void PlayerSpawned(EId id);
        void ProjectileSpawned(EId id, Vector3 position, Vector3 direction, float damage);
        void ProjectileDespawned(EId id);
        void ProjectileHit(EId id, Vector3 position, Vector3 normal, string hitType = "surface");
        void EntityDamaged(EId id, float currentHp, float maxHp);
        void EntityDied(EId id, EId killerId = default);
        void GroundItemSpawned(EId id, Vector3 position, string definitionId);
        void GroundItemDespawned(EId id);

        void BotSpawned(EId id, Vector3 position, string typeId);
        void BotDespawned(EId id);
        void WeaponFired(Vector3 position, Vector3 direction);
        void WeaponEquipStarted(string prefabId);
        void WeaponUnequipStarted(string prefabId);
        void WeaponEquipFinished(string prefabId);
        void WeaponReloadStarted(string prefabId);
        void WeaponReloadFinished(string prefabId);
        void WeaponDryFired(string prefabId);
        void WeaponChargeStarted(string prefabId);
        void WeaponChargeCompleted(string prefabId);
        void WeaponChargeCancelled(string prefabId);

        void GrenadeSpawned(EId id, Vector3 position, Vector3 velocity);
        void GrenadeExploded(EId id, Vector3 position);
        void GrenadeDespawned(EId id);

        void MedkitUseStarted();
        void MedkitUseStopped();

        void HitConfirmed(bool isKill, bool isHeadshot = false,
            float absorptionRatio = 0f, bool isRicochet = false);

        /// <summary>
        /// Per-target hit event for view-layer feedback (hit flash, future blood spray, decal projection).
        /// Emitted for all hits regardless of projectile owner (HitConfirmed, by contrast,
        /// fires only for player-owned shots and drives crosshair markers).
        /// </summary>
        void EntityHit(EId targetEid, Vector3 hitPoint, Vector3 projectileDirection,
            bool isHeadshot, bool isRicochet, bool isKill, float absorptionRatio);
        void StatusEffectApplied(EId entityId, string effectType);
        void StatusEffectRemoved(EId entityId, string effectType);

        void LootableSpawned(EId id, Vector3 position, string typeId);
        void LootableDespawned(EId id);

        void DamageNumberSpawned(Vector3 worldPos, float damage, bool isHeadshot, bool isKill, Vector3 bulletDir,
            float absorptionRatio = 0f);

        void ArmorBroken(EId entityId, bool isHelmet);
        void ProjectileRicochet(EId projectileId, Vector3 position, Vector3 direction);

        /// <summary>Emitted when a <c>WeaponConfiguration</c> fails to assemble — per D7 ghost-weapon pattern.</summary>
        void WeaponAssemblyFailed(string weaponIdentifier, string reason);
    }
}
