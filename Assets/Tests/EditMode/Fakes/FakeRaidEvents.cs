using Adapters;
using State;
using UnityEngine;

namespace Tests.EditMode.Fakes
{
    public class FakeRaidEvents : IRaidEvents
    {
        public bool PlayerSpawnedCalled;
        public EId SpawnedId;

        public void RaidStarted() { }
        public void RaidEnded() { }

        public void PlayerSpawned(EId id)
        {
            PlayerSpawnedCalled = true;
            SpawnedId = id;
        }

        public void ProjectileSpawned(EId id, Vector3 position, Vector3 direction, float damage) { }
        public void ProjectileDespawned(EId id) { }
        public void ProjectileHit(EId id, Vector3 position) { }

        public bool EntityDamagedCalled;
        public EId EntityDamagedId;
        public bool EntityDiedCalled;
        public EId EntityDiedId;

        public void EntityDamaged(EId id, float currentHp, float maxHp)
        {
            EntityDamagedCalled = true;
            EntityDamagedId = id;
        }

        public void EntityDied(EId id)
        {
            EntityDiedCalled = true;
            EntityDiedId = id;
        }

        public void GroundItemSpawned(EId id, Vector3 position, string definitionId) { }
        public void GroundItemDespawned(EId id) { }

        public bool BotSpawnedCalled;
        public EId BotSpawnedId;
        public string BotSpawnedTypeId;
        public bool BotDespawnedCalled;
        public EId BotDespawnedId;

        public void BotSpawned(EId id, Vector3 position, string typeId)
        {
            BotSpawnedCalled = true;
            BotSpawnedId = id;
            BotSpawnedTypeId = typeId;
        }

        public void BotDespawned(EId id)
        {
            BotDespawnedCalled = true;
            BotDespawnedId = id;
        }
        public void WeaponFired(Vector3 position, Vector3 direction) { }

        public bool WeaponEquipStartedCalled;
        public string WeaponEquipStartedPrefabId;
        public void WeaponEquipStarted(string prefabId)
        {
            WeaponEquipStartedCalled = true;
            WeaponEquipStartedPrefabId = prefabId;
        }

        public bool WeaponUnequipStartedCalled;
        public string WeaponUnequipStartedPrefabId;
        public void WeaponUnequipStarted(string prefabId)
        {
            WeaponUnequipStartedCalled = true;
            WeaponUnequipStartedPrefabId = prefabId;
        }

        public bool WeaponEquipFinishedCalled;
        public string WeaponEquipFinishedPrefabId;
        public void WeaponEquipFinished(string prefabId)
        {
            WeaponEquipFinishedCalled = true;
            WeaponEquipFinishedPrefabId = prefabId;
        }

        public bool WeaponReloadStartedCalled;
        public string WeaponReloadStartedPrefabId;
        public void WeaponReloadStarted(string prefabId)
        {
            WeaponReloadStartedCalled = true;
            WeaponReloadStartedPrefabId = prefabId;
        }

        public bool WeaponReloadFinishedCalled;
        public string WeaponReloadFinishedPrefabId;
        public void WeaponReloadFinished(string prefabId)
        {
            WeaponReloadFinishedCalled = true;
            WeaponReloadFinishedPrefabId = prefabId;
        }

        public bool WeaponDryFiredCalled;
        public string WeaponDryFiredPrefabId;
        public void WeaponDryFired(string prefabId)
        {
            WeaponDryFiredCalled = true;
            WeaponDryFiredPrefabId = prefabId;
        }

        public bool GrenadeSpawnedCalled;
        public EId GrenadeSpawnedId;
        public Vector3 GrenadeSpawnedVelocity;
        public void GrenadeSpawned(EId id, Vector3 position, Vector3 velocity)
        {
            GrenadeSpawnedCalled = true;
            GrenadeSpawnedId = id;
            GrenadeSpawnedVelocity = velocity;
        }

        public bool GrenadeExplodedCalled;
        public EId GrenadeExplodedId;
        public void GrenadeExploded(EId id, Vector3 position)
        {
            GrenadeExplodedCalled = true;
            GrenadeExplodedId = id;
        }

        public bool GrenadeDespawnedCalled;
        public EId GrenadeDespawnedId;
        public void GrenadeDespawned(EId id)
        {
            GrenadeDespawnedCalled = true;
            GrenadeDespawnedId = id;
        }

        public bool MedkitUseStartedCalled;
        public void MedkitUseStarted() { MedkitUseStartedCalled = true; }

        public bool MedkitUseStoppedCalled;
        public void MedkitUseStopped() { MedkitUseStoppedCalled = true; }

        public bool HitConfirmedCalled;
        public bool HitConfirmedIsKill;
        public void HitConfirmed(bool isKill)
        {
            HitConfirmedCalled = true;
            HitConfirmedIsKill = isKill;
        }

        public bool StatusEffectAppliedCalled;
        public string StatusEffectAppliedType;
        public void StatusEffectApplied(EId entityId, string effectType)
        {
            StatusEffectAppliedCalled = true;
            StatusEffectAppliedType = effectType;
        }

        public bool StatusEffectRemovedCalled;
        public string StatusEffectRemovedType;
        public void StatusEffectRemoved(EId entityId, string effectType)
        {
            StatusEffectRemovedCalled = true;
            StatusEffectRemovedType = effectType;
        }
    }
}
