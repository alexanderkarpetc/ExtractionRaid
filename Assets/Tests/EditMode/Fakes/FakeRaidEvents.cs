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
    }
}
