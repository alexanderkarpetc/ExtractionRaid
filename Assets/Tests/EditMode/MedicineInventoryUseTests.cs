using NUnit.Framework;
using State;
using Systems;
using Tests.EditMode.Fakes;
using UnityEngine;

namespace Tests.EditMode
{
    [TestFixture]
    public class MedicineInventoryUseTests
    {
        RaidState _state;
        InventoryState _inventory;
        FakeRaidEvents _events;
        EId _playerId;

        [SetUp]
        public void SetUp()
        {
            _state = RaidState.Create(EditModeTestsUtils.NewAllocator());
            _playerId = _state.AllocateEId();
            _state.PlayerEntity = PlayerEntityState.Create(_playerId, Vector3.zero);
            _state.HealthMap[_playerId] = HealthState.Create(100f);
            _state.HealthMap[_playerId].CurrentHp = 50f;
            _inventory = new InventoryState();
            _events = new FakeRaidEvents();
        }

        [Test]
        public void AdvancedMedkit_IsQuickSlotAssignableResourceMedkit()
        {
            var item = ItemState.Create(_state.AllocateEId(), "Advanced_Medkit");

            Assert.IsTrue(QuickSlotRules.IsAssignable(item.DefinitionId));
            Assert.IsTrue(QuickSlotRules.IsMedkit(item.DefinitionId));
            Assert.Greater(item.CurrentResource, 0);
        }

        [TestCase("Medkit")]
        [TestCase("Advanced_Medkit")]
        public void Medkit_TryStartFromInventory_UsesBackpackSlot(string definitionId)
        {
            _inventory.Backpack[4] = ItemState.Create(_state.AllocateEId(), definitionId);

            bool started = MedkitSystem.TryStartFromInventory(_state, _inventory, 4, _events);

            Assert.IsTrue(started);
            Assert.IsTrue(_state.PlayerEntity.IsUsingMedkit);
            Assert.AreEqual(4, _state.PlayerEntity.ActiveMedkitSlot);
            Assert.AreEqual(PlayerEntityState.InventoryUseQuickSlot,
                _state.PlayerEntity.ActiveQuickSlot);
            Assert.IsTrue(_events.MedkitUseStartedCalled);
        }

        [Test]
        public void Medkit_TryStartFromInventory_RejectsFullHealth()
        {
            _inventory.Backpack[0] = ItemState.Create(_state.AllocateEId(), "Medkit");
            _state.HealthMap[_playerId].CurrentHp = 100f;

            bool started = MedkitSystem.TryStartFromInventory(_state, _inventory, 0, _events);

            Assert.IsFalse(started);
            Assert.IsFalse(_state.PlayerEntity.IsUsingMedkit);
        }

        [Test]
        public void Bandage_TryStartFromInventory_UsesBackpackSlot()
        {
            _inventory.Backpack[2] = ItemState.Create(_state.AllocateEId(), "Bandage", 2);

            bool started = BandageSystem.TryStartFromInventory(_state, _inventory, 2, _events);

            Assert.IsTrue(started);
            Assert.IsTrue(_state.PlayerEntity.IsUsingBandage);
            Assert.AreEqual(2, _state.PlayerEntity.ActiveBandageSlot);
            Assert.AreEqual(PlayerEntityState.InventoryUseQuickSlot,
                _state.PlayerEntity.ActiveQuickSlot);
        }
    }
}
