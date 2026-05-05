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
        public void SpawnPlayer_BothHotbarSlotsFilled()
        {
            // Cheat loadout (2026-05-05): all 6 weapon variants — first 2 у hotbar.
            var state = RaidState.Create(EditModeTestsUtils.NewAllocator());
            var events = new FakeRaidEvents();

            PlayerSpawnSystem.SpawnPlayer(state, Vector3.zero, events);

            Assert.IsNotNull(state.PlayerEntity.Hotbar[0]);
            Assert.IsNotNull(state.PlayerEntity.Hotbar[1]);
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
        public void SpawnPlayer_EmptyInventory_GetsStartingArmorEquipped()
        {
            var state = RaidState.Create(EditModeTestsUtils.NewAllocator());
            var events = new FakeRaidEvents();

            PlayerSpawnSystem.SpawnPlayer(state, Vector3.zero, events);

            // Cheat loadout 2026-05-05: armor goes directly into equip slots, not backpack.
            var inv = App.Instance.Player.Inventory;
            Assert.AreEqual("Helmet_Basic", inv.HelmetSlot?.DefinitionId,
                "HelmetSlot should be pre-equipped with Helmet_Basic");
            Assert.AreEqual("Armor_Basic", inv.BodyArmorSlot?.DefinitionId,
                "BodyArmorSlot should be pre-equipped with Armor_Basic");
        }

        [Test]
        public void SpawnPlayer_StartingArmorInArmorMap()
        {
            var state = RaidState.Create(EditModeTestsUtils.NewAllocator());
            var events = new FakeRaidEvents();

            PlayerSpawnSystem.SpawnPlayer(state, Vector3.zero, events);

            // Cheat loadout equips armor → ArmorMap contains player entry з both slots.
            var playerId = state.PlayerEntity.Id;
            Assert.IsTrue(state.ArmorMap.TryGetValue(playerId, out var slots),
                "ArmorMap should contain player when armor is pre-equipped");
            Assert.IsNotNull(slots.Helmet, "Helmet should be in ArmorMap");
            Assert.IsNotNull(slots.BodyArmor, "BodyArmor should be in ArmorMap");
        }
    }
}
