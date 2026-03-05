using Adapters;
using Managers;
using NUnit.Framework;
using State;

namespace Tests.EditMode
{
    [TestFixture]
    public class PlayerSpawnManagerTests
    {
        class FakeRaidEvents : IRaidEvents
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
        }

        [Test]
        public void SpawnPlayer_CreatesPlayerEntity()
        {
            var state = RaidState.Create();
            var events = new FakeRaidEvents();

            PlayerSpawnManager.SpawnPlayer(state, events);

            Assert.IsNotNull(state.PlayerEntity);
            Assert.IsTrue(state.PlayerEntity.Id.IsValid);
        }

        [Test]
        public void SpawnPlayer_EmitsPlayerSpawnedEvent()
        {
            var state = RaidState.Create();
            var events = new FakeRaidEvents();

            PlayerSpawnManager.SpawnPlayer(state, events);

            Assert.IsTrue(events.PlayerSpawnedCalled);
            Assert.AreEqual(state.PlayerEntity.Id, events.SpawnedId);
        }

        [Test]
        public void SpawnPlayer_DoesNotDoubleSpawn()
        {
            var state = RaidState.Create();
            var events = new FakeRaidEvents();

            PlayerSpawnManager.SpawnPlayer(state, events);
            var firstId = state.PlayerEntity.Id;

            events.PlayerSpawnedCalled = false;
            PlayerSpawnManager.SpawnPlayer(state, events);

            Assert.AreEqual(firstId, state.PlayerEntity.Id);
            Assert.IsFalse(events.PlayerSpawnedCalled);
        }
    }
}
