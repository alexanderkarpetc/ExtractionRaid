using ApplicationCore;
using Systems;
using NUnit.Framework;
using State;
using Tests.EditMode.Fakes;
using UnityEngine;

namespace Tests.EditMode
{
    [TestFixture]
    public class PlayerSpawnSystemTests
    {
        [SetUp]
        public void SetUp() => EditModeTestsUtils.EnsureAppForTests();

        [TearDown]
        public void TearDown() => EditModeTestsUtils.ResetApp();

        [Test]
        public void SpawnPlayer_CreatesPlayerEntity()
        {
            var state = RaidState.Create(EditModeTestsUtils.NewAllocator());
            var events = new FakeRaidEvents();

            PlayerSpawnSystem.SpawnPlayer(state, Vector3.zero, events);

            Assert.IsNotNull(state.PlayerEntity);
            Assert.IsTrue(state.PlayerEntity.Id.IsValid);
        }

        [Test]
        public void SpawnPlayer_EmitsPlayerSpawnedEvent()
        {
            var state = RaidState.Create(EditModeTestsUtils.NewAllocator());
            var events = new FakeRaidEvents();

            PlayerSpawnSystem.SpawnPlayer(state, Vector3.zero, events);

            Assert.IsTrue(events.PlayerSpawnedCalled);
            Assert.AreEqual(state.PlayerEntity.Id, events.SpawnedId);
        }

        [Test]
        public void SpawnPlayer_CreatesEquippedWeapon()
        {
            var state = RaidState.Create(EditModeTestsUtils.NewAllocator());
            var events = new FakeRaidEvents();

            PlayerSpawnSystem.SpawnPlayer(state, Vector3.zero, events);

            Assert.IsNotNull(state.PlayerEntity.EquippedWeapon);
            Assert.IsTrue(state.PlayerEntity.EquippedWeapon.Id.IsValid);
            Assert.AreNotEqual(state.PlayerEntity.Id, state.PlayerEntity.EquippedWeapon.Id);
        }

        [Test]
        public void SpawnPlayer_WeaponInHotbarSlotZero()
        {
            var state = RaidState.Create(EditModeTestsUtils.NewAllocator());
            var events = new FakeRaidEvents();

            PlayerSpawnSystem.SpawnPlayer(state, Vector3.zero, events);

            Assert.IsNotNull(state.PlayerEntity.Hotbar[0]);
            Assert.AreEqual(state.PlayerEntity.EquippedWeapon, state.PlayerEntity.Hotbar[0]);
            Assert.AreEqual(0, state.PlayerEntity.SelectedHotbarSlot);
        }

        [Test]
        public void SpawnPlayer_BaselineFloor_OnlyFirstHotbarSlotFilled()
        {
            // Baseline floor = a single pistol → hotbar slot 0 filled, slot 1 empty.
            // (Test ranges get the full 6-weapon cheat kit; not exercised here.)
            var state = RaidState.Create(EditModeTestsUtils.NewAllocator());
            var events = new FakeRaidEvents();

            PlayerSpawnSystem.SpawnPlayer(state, Vector3.zero, events);

            Assert.IsNotNull(state.PlayerEntity.Hotbar[0]);
            Assert.IsNull(state.PlayerEntity.Hotbar[1]);
        }

        [Test]
        public void SpawnPlayer_DoesNotDoubleSpawn()
        {
            var state = RaidState.Create(EditModeTestsUtils.NewAllocator());
            var events = new FakeRaidEvents();

            PlayerSpawnSystem.SpawnPlayer(state, Vector3.zero, events);
            var firstId = state.PlayerEntity.Id;

            events.PlayerSpawnedCalled = false;
            PlayerSpawnSystem.SpawnPlayer(state, Vector3.zero, events);

            Assert.AreEqual(firstId, state.PlayerEntity.Id);
            Assert.IsFalse(events.PlayerSpawnedCalled);
        }

        // ── Armor ─────────────────────────────────────────────

        [Test]
        public void SpawnPlayer_BaselineFloor_HasNoArmor()
        {
            var state = RaidState.Create(EditModeTestsUtils.NewAllocator());
            var events = new FakeRaidEvents();

            PlayerSpawnSystem.SpawnPlayer(state, Vector3.zero, events);

            // Baseline floor is deliberately armor-free (PlayerSpawnSystem.GiveBaselineFloor).
            var inv = App.Instance.Player.Inventory;
            Assert.IsNull(inv.HelmetSlot, "Baseline floor grants no helmet");
            Assert.IsNull(inv.BodyArmorSlot, "Baseline floor grants no body armor");
        }

        [Test]
        public void SpawnPlayer_BaselineFloor_NotInArmorMap()
        {
            var state = RaidState.Create(EditModeTestsUtils.NewAllocator());
            var events = new FakeRaidEvents();

            PlayerSpawnSystem.SpawnPlayer(state, Vector3.zero, events);

            // No armor equipped → player has no ArmorMap entry.
            Assert.IsFalse(state.ArmorMap.ContainsKey(state.PlayerEntity.Id),
                "Baseline floor has no armor, so no ArmorMap entry");
        }
    }
}
