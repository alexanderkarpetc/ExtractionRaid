using NUnit.Framework;
using State;
using Systems;
using Tests.EditMode.Fakes;
using UnityEngine;

namespace Tests.EditMode
{
    [TestFixture]
    public class InventorySystemPickUpToSlotTests
    {
        RaidState _state;
        InventoryState _playerInv;
        FakeRaidEvents _events;

        [SetUp]
        public void SetUp()
        {
            EditModeTestsUtils.EnsureAppForTests();
            _state = RaidState.Create(EditModeTestsUtils.NewAllocator());
            _playerInv = new InventoryState();
            _events = new FakeRaidEvents();
        }

        [TearDown]
        public void TearDown() => EditModeTestsUtils.ResetApp();

        GroundItemState SpawnGround(string defId, int stack = 1)
        {
            var gi = GroundItemState.Create(_state.AllocateEId(), defId, Vector3.zero, stack);
            _state.GroundItems.Add(gi);
            return gi;
        }

        [Test]
        public void TryPickUpToSlot_PlacesIntoEmptyBackpackSlot()
        {
            var gi = SpawnGround("Medkit");

            bool ok = InventorySystem.TryPickUpToSlot(_state, _playerInv, gi.Id,
                InventorySlotRef.BackpackSlot(4), _events);

            Assert.IsTrue(ok);
            Assert.IsNotNull(_playerInv.Backpack[4]);
            Assert.AreEqual("Medkit", _playerInv.Backpack[4].DefinitionId);
            Assert.AreEqual(0, _state.GroundItems.Count, "Ground item must be despawned");
            Assert.IsTrue(_events.GroundItemDespawnedCalled);
        }

        [Test]
        public void TryPickUpToSlot_AllowedSlotsMismatch_ReturnsFalse_NoMutation()
        {
            // Medkit goes to backpack only, not helmet.
            var gi = SpawnGround("Medkit");

            bool ok = InventorySystem.TryPickUpToSlot(_state, _playerInv, gi.Id,
                InventorySlotRef.Helmet(), _events);

            Assert.IsFalse(ok);
            Assert.AreEqual(1, _state.GroundItems.Count, "Ground item must NOT despawn on rejection");
            Assert.IsNull(_playerInv.HelmetSlot);
            Assert.IsFalse(_events.GroundItemDespawnedCalled);
        }

        [Test]
        public void TryPickUpToSlot_TargetOccupied_ReturnsFalse_NoMutation()
        {
            var gi = SpawnGround("Medkit");
            var existing = ItemState.Create(_state.AllocateEId(), "Bandage");
            _playerInv.Backpack[2] = existing;

            bool ok = InventorySystem.TryPickUpToSlot(_state, _playerInv, gi.Id,
                InventorySlotRef.BackpackSlot(2), _events);

            Assert.IsFalse(ok);
            Assert.AreSame(existing, _playerInv.Backpack[2]);
            Assert.AreEqual(1, _state.GroundItems.Count);
        }

        [Test]
        public void TryPickUpToSlot_UnknownGroundEid_ReturnsFalse()
        {
            bool ok = InventorySystem.TryPickUpToSlot(_state, _playerInv,
                new EId(99999),
                InventorySlotRef.BackpackSlot(0), _events);

            Assert.IsFalse(ok);
            Assert.IsFalse(_events.GroundItemDespawnedCalled);
        }

        [Test]
        public void TryPickUpToSlot_HelmetGoesToHelmetSlot()
        {
            var gi = SpawnGround("Helmet_Basic");

            bool ok = InventorySystem.TryPickUpToSlot(_state, _playerInv, gi.Id,
                InventorySlotRef.Helmet(), _events);

            Assert.IsTrue(ok);
            Assert.IsNotNull(_playerInv.HelmetSlot);
            Assert.AreEqual("Helmet_Basic", _playerInv.HelmetSlot.DefinitionId);
        }

        [Test]
        public void TryPickUpToSlot_PreservesStackCount()
        {
            var gi = SpawnGround("Ammo_Rifle", stack: 25);

            bool ok = InventorySystem.TryPickUpToSlot(_state, _playerInv, gi.Id,
                InventorySlotRef.BackpackSlot(0), _events);

            Assert.IsTrue(ok);
            Assert.AreEqual(25, _playerInv.Backpack[0].StackCount);
        }

        [Test]
        public void TryPickUpToSlot_NullArgs_ReturnsFalse()
        {
            var gi = SpawnGround("Medkit");
            Assert.IsFalse(InventorySystem.TryPickUpToSlot(null, _playerInv, gi.Id,
                InventorySlotRef.BackpackSlot(0), _events));
            Assert.IsFalse(InventorySystem.TryPickUpToSlot(_state, null, gi.Id,
                InventorySlotRef.BackpackSlot(0), _events));
        }
    }
}
