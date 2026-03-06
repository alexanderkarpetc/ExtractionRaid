using Systems;
using NUnit.Framework;
using State;
using Tests.EditMode.Fakes;

namespace Tests.EditMode
{
    [TestFixture]
    public class PlayerSpawnSystemTests
    {
        [Test]
        public void SpawnPlayer_CreatesPlayerEntity()
        {
            var state = RaidState.Create();
            var events = new FakeRaidEvents();

            PlayerSpawnSystem.SpawnPlayer(state, events);

            Assert.IsNotNull(state.PlayerEntity);
            Assert.IsTrue(state.PlayerEntity.Id.IsValid);
        }

        [Test]
        public void SpawnPlayer_EmitsPlayerSpawnedEvent()
        {
            var state = RaidState.Create();
            var events = new FakeRaidEvents();

            PlayerSpawnSystem.SpawnPlayer(state, events);

            Assert.IsTrue(events.PlayerSpawnedCalled);
            Assert.AreEqual(state.PlayerEntity.Id, events.SpawnedId);
        }

        [Test]
        public void SpawnPlayer_CreatesEquippedWeapon()
        {
            var state = RaidState.Create();
            var events = new FakeRaidEvents();

            PlayerSpawnSystem.SpawnPlayer(state, events);

            Assert.IsNotNull(state.PlayerEntity.EquippedWeapon);
            Assert.IsTrue(state.PlayerEntity.EquippedWeapon.Id.IsValid);
            Assert.AreNotEqual(state.PlayerEntity.Id, state.PlayerEntity.EquippedWeapon.Id);
        }

        [Test]
        public void SpawnPlayer_WeaponInHotbarSlotZero()
        {
            var state = RaidState.Create();
            var events = new FakeRaidEvents();

            PlayerSpawnSystem.SpawnPlayer(state, events);

            Assert.IsNotNull(state.PlayerEntity.Hotbar[0]);
            Assert.AreEqual(state.PlayerEntity.EquippedWeapon, state.PlayerEntity.Hotbar[0]);
            Assert.AreEqual(0, state.PlayerEntity.SelectedHotbarSlot);
        }

        [Test]
        public void SpawnPlayer_SecondWeaponInHotbarSlotOne()
        {
            var state = RaidState.Create();
            var events = new FakeRaidEvents();

            PlayerSpawnSystem.SpawnPlayer(state, events);

            Assert.IsNotNull(state.PlayerEntity.Hotbar[1]);
            Assert.IsTrue(state.PlayerEntity.Hotbar[1].Id.IsValid);
            Assert.AreNotEqual(state.PlayerEntity.Hotbar[0].Id, state.PlayerEntity.Hotbar[1].Id);
        }

        [Test]
        public void SpawnPlayer_DoesNotDoubleSpawn()
        {
            var state = RaidState.Create();
            var events = new FakeRaidEvents();

            PlayerSpawnSystem.SpawnPlayer(state, events);
            var firstId = state.PlayerEntity.Id;

            events.PlayerSpawnedCalled = false;
            PlayerSpawnSystem.SpawnPlayer(state, events);

            Assert.AreEqual(firstId, state.PlayerEntity.Id);
            Assert.IsFalse(events.PlayerSpawnedCalled);
        }
    }
}
