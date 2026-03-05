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

        public void ProjectileSpawned(EId id, Vector3 position, Vector3 direction) { }
        public void ProjectileDespawned(EId id) { }
    }
}