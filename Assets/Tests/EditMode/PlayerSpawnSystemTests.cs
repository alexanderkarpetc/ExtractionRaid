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
        public void SpawnPlayer_SecondWeaponInHotbarSlotOne()
        {
            var state = RaidState.Create(EditModeTestsUtils.NewAllocator());
            var events = new FakeRaidEvents();

            PlayerSpawnSystem.SpawnPlayer(state, Vector3.zero, events);

            Assert.IsNotNull(state.PlayerEntity.Hotbar[1]);
            Assert.IsTrue(state.PlayerEntity.Hotbar[1].Id.IsValid);
            Assert.AreNotEqual(state.PlayerEntity.Hotbar[0].Id, state.PlayerEntity.Hotbar[1].Id);
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
        public void SpawnPlayer_EmptyInventory_GetsStartingArmorInBackpack()
        {
            var state = RaidState.Create(EditModeTestsUtils.NewAllocator());
            var events = new FakeRaidEvents();

            PlayerSpawnSystem.SpawnPlayer(state, Vector3.zero, events);

            // Armor starts in backpack (not equipped) — player must equip manually
            bool hasHelmet = false, hasArmor = false;
            for (int i = 0; i < InventoryState.BackpackSize; i++)
            {
                var item = App.Instance.Player.Inventory.Backpack[i];
                if (item?.DefinitionId == "Helmet_Basic") hasHelmet = true;
                if (item?.DefinitionId == "Armor_Basic") hasArmor = true;
            }
            Assert.IsTrue(hasHelmet, "Backpack should contain Helmet_Basic");
            Assert.IsTrue(hasArmor, "Backpack should contain Armor_Basic");
        }

        [Test]
        public void SpawnPlayer_ArmorInBackpack_NotInArmorMap()
        {
            var state = RaidState.Create(EditModeTestsUtils.NewAllocator());
            var events = new FakeRaidEvents();

            PlayerSpawnSystem.SpawnPlayer(state, Vector3.zero, events);

            // Armor is in backpack, not equipped — ArmorMap should be empty
            var playerId = state.PlayerEntity.Id;
            Assert.IsFalse(state.ArmorMap.ContainsKey(playerId),
                "ArmorMap should be empty when armor is in backpack, not equipped");
        }
    }
}
