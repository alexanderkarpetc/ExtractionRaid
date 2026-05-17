using System.Collections.Generic;
using NUnit.Framework;
using State;
using Systems;
using Tests.EditMode.Fakes;

namespace Tests.EditMode
{
    [TestFixture]
    public class StashSystemTests
    {
        InventoryState _playerInv;
        List<ItemState> _stash;
        System.Func<EId> _alloc;

        [SetUp]
        public void SetUp()
        {
            EditModeTestsUtils.EnsureAppForTests();
            _playerInv = new InventoryState();
            _stash = new List<ItemState>();
            _alloc = EditModeTestsUtils.NewAllocator();
        }

        [TearDown]
        public void TearDown() => EditModeTestsUtils.ResetApp();

        ItemState MakeItem(string defId, int stack = 1) =>
            ItemState.Create(_alloc(), defId, stack);

        // ── TryDeposit ───────────────────────────────────────

        [Test]
        public void TryDeposit_MovesItemFromBackpackToStash()
        {
            var medkit = MakeItem("Medkit");
            _playerInv.Backpack[3] = medkit;

            bool ok = StashSystem.TryDeposit(_playerInv, _stash,
                InventorySlotRef.BackpackSlot(3));

            Assert.IsTrue(ok);
            Assert.IsNull(_playerInv.Backpack[3]);
            Assert.AreEqual(1, _stash.Count);
            Assert.AreSame(medkit, _stash[0]);
        }

        [Test]
        public void TryDeposit_EmptySource_ReturnsFalse_NoMutation()
        {
            bool ok = StashSystem.TryDeposit(_playerInv, _stash,
                InventorySlotRef.BackpackSlot(0));

            Assert.IsFalse(ok);
            Assert.AreEqual(0, _stash.Count);
        }

        [Test]
        public void TryDeposit_NullInventory_ReturnsFalse()
        {
            bool ok = StashSystem.TryDeposit(null, _stash, InventorySlotRef.BackpackSlot(0));
            Assert.IsFalse(ok);
        }

        [Test]
        public void TryDeposit_NullStash_ReturnsFalse()
        {
            _playerInv.Backpack[0] = MakeItem("Medkit");
            bool ok = StashSystem.TryDeposit(_playerInv, null, InventorySlotRef.BackpackSlot(0));
            Assert.IsFalse(ok);
            Assert.IsNotNull(_playerInv.Backpack[0], "Source must NOT be cleared on failure");
        }

        [Test]
        public void TryDeposit_AppendsToEndOfStash()
        {
            _stash.Add(MakeItem("Bandage"));
            var grenade = MakeItem("Grenade");
            _playerInv.Backpack[2] = grenade;

            bool ok = StashSystem.TryDeposit(_playerInv, _stash,
                InventorySlotRef.BackpackSlot(2));

            Assert.IsTrue(ok);
            Assert.AreEqual(2, _stash.Count);
            Assert.AreEqual("Bandage", _stash[0].DefinitionId);
            Assert.AreSame(grenade, _stash[1]);
        }

        // ── TryWithdraw — empty target ───────────────────────

        [Test]
        public void TryWithdraw_EmptyTarget_MovesItemFromStashToBackpack()
        {
            var medkit = MakeItem("Medkit");
            _stash.Add(medkit);

            bool ok = StashSystem.TryWithdraw(_stash, 0, _playerInv,
                InventorySlotRef.BackpackSlot(5));

            Assert.IsTrue(ok);
            Assert.AreSame(medkit, _playerInv.Backpack[5]);
            Assert.AreEqual(0, _stash.Count, "Stash should shrink by 1 on empty-target withdraw");
        }

        [Test]
        public void TryWithdraw_AllowedSlotsMismatch_ReturnsFalse_NoMutation()
        {
            // Medkit can't be placed in helmet slot (AllowedSlots = Backpack only).
            var medkit = MakeItem("Medkit");
            _stash.Add(medkit);

            bool ok = StashSystem.TryWithdraw(_stash, 0, _playerInv,
                InventorySlotRef.Helmet());

            Assert.IsFalse(ok);
            Assert.AreEqual(1, _stash.Count, "Stash must not change on rejection");
            Assert.IsNull(_playerInv.HelmetSlot);
        }

        [Test]
        public void TryWithdraw_IndexOutOfRange_ReturnsFalse()
        {
            _stash.Add(MakeItem("Medkit"));
            Assert.IsFalse(StashSystem.TryWithdraw(_stash, -1, _playerInv,
                InventorySlotRef.BackpackSlot(0)));
            Assert.IsFalse(StashSystem.TryWithdraw(_stash, 5, _playerInv,
                InventorySlotRef.BackpackSlot(0)));
        }

        // ── TryWithdraw — swap behavior ──────────────────────

        [Test]
        public void TryWithdraw_OccupiedTarget_SwapsWithExisting()
        {
            var stashItem = MakeItem("Medkit");
            var playerItem = MakeItem("Bandage");
            _stash.Add(stashItem);
            _playerInv.Backpack[7] = playerItem;

            bool ok = StashSystem.TryWithdraw(_stash, 0, _playerInv,
                InventorySlotRef.BackpackSlot(7));

            Assert.IsTrue(ok);
            Assert.AreSame(stashItem, _playerInv.Backpack[7]);
            Assert.AreEqual(1, _stash.Count, "Stash size unchanged on swap");
            Assert.AreSame(playerItem, _stash[0], "Player item should land у the same stash index");
        }

        [Test]
        public void TryWithdraw_SwapPreservesStashIndex()
        {
            _stash.Add(MakeItem("Bandage"));    // index 0
            var target = MakeItem("Medkit");
            _stash.Add(target);                 // index 1 — we'll withdraw this
            _stash.Add(MakeItem("Grenade"));    // index 2

            var existing = MakeItem("Ammo_Rifle", 5);
            _playerInv.Backpack[10] = existing;

            bool ok = StashSystem.TryWithdraw(_stash, 1, _playerInv,
                InventorySlotRef.BackpackSlot(10));

            Assert.IsTrue(ok);
            Assert.AreEqual(3, _stash.Count);
            Assert.AreEqual("Bandage", _stash[0].DefinitionId);
            Assert.AreSame(existing, _stash[1], "Swapped item lands у the original target's stash index");
            Assert.AreEqual("Grenade", _stash[2].DefinitionId);
            Assert.AreSame(target, _playerInv.Backpack[10]);
        }

        [Test]
        public void TryWithdraw_NullArgs_ReturnsFalse()
        {
            Assert.IsFalse(StashSystem.TryWithdraw(null, 0, _playerInv, InventorySlotRef.BackpackSlot(0)));
            Assert.IsFalse(StashSystem.TryWithdraw(_stash, 0, null, InventorySlotRef.BackpackSlot(0)));
        }
    }
}
