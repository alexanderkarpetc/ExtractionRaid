using NUnit.Framework;
using State;
using Systems;
using UnityEngine;

namespace Tests.EditMode
{
    /// <summary>
    /// Covers <see cref="HotbarWeaponSystem.SwapWeaponSlots"/> (Battle HUD Stage 6 hotbar
    /// drag-swap). Key invariants: inventory refs AND live weapon entities swap together
    /// (so WeaponSyncSystem won't rebuild → magazines preserved), and selection follows the
    /// equipped weapon to its new slot.
    /// </summary>
    [TestFixture]
    public class HotbarWeaponSystemTests
    {
        static (RaidState state, InventoryState inv, WeaponEntityState wA, WeaponEntityState wB) Setup()
        {
            var state = RaidState.Create(EditModeTestsUtils.NewAllocator());
            var pid = state.AllocateEId();
            state.PlayerEntity = PlayerEntityState.Create(pid, Vector3.zero);

            var inv = new InventoryState();
            var idA = state.AllocateEId();
            var idB = state.AllocateEId();
            inv.WeaponSlots[0] = ItemState.Create(idA, "Weapon");
            inv.WeaponSlots[1] = ItemState.Create(idB, "Weapon");

            // Distinct AmmoInMagazine (rifle 30 / pistol 12) → lets us assert no rebuild/reset.
            var wA = EditModeTestsUtils.NewRifleLikeWeapon(idA);
            var wB = EditModeTestsUtils.NewPistolLikeWeapon(idB);
            // Simulate combat — partially-spent mag that a rebuild would reset.
            wA.AmmoInMagazine = 7;
            state.PlayerEntity.Hotbar[0] = wA;
            state.PlayerEntity.Hotbar[1] = wB;

            return (state, inv, wA, wB);
        }

        [Test]
        public void Swap_ExchangesInventoryRefs()
        {
            var (state, inv, _, _) = Setup();
            var itemA = inv.WeaponSlots[0];
            var itemB = inv.WeaponSlots[1];

            HotbarWeaponSystem.SwapWeaponSlots(state, inv, 0, 1);

            Assert.AreSame(itemB, inv.WeaponSlots[0]);
            Assert.AreSame(itemA, inv.WeaponSlots[1]);
        }

        [Test]
        public void Swap_ExchangesLiveHotbarEntities_PreservingState()
        {
            var (state, inv, wA, wB) = Setup();

            HotbarWeaponSystem.SwapWeaponSlots(state, inv, 0, 1);

            // Same entity objects moved (not rebuilt) → partially-spent mag preserved.
            Assert.AreSame(wB, state.PlayerEntity.Hotbar[0]);
            Assert.AreSame(wA, state.PlayerEntity.Hotbar[1]);
            Assert.AreEqual(7, state.PlayerEntity.Hotbar[1].AmmoInMagazine, "mag must not reset");
            // Id alignment with the swapped inventory item → WeaponSyncSystem won't rebuild.
            Assert.AreEqual(inv.WeaponSlots[0].Id, state.PlayerEntity.Hotbar[0].Id);
            Assert.AreEqual(inv.WeaponSlots[1].Id, state.PlayerEntity.Hotbar[1].Id);
        }

        [Test]
        public void Swap_SelectionFollowsEquippedWeapon()
        {
            var (state, inv, wA, _) = Setup();
            state.PlayerEntity.SelectedHotbarSlot = 0;
            state.PlayerEntity.EquippedWeapon = wA;

            HotbarWeaponSystem.SwapWeaponSlots(state, inv, 0, 1);

            Assert.AreEqual(1, state.PlayerEntity.SelectedHotbarSlot, "selection follows weapon to new slot");
            Assert.AreSame(wA, state.PlayerEntity.Hotbar[state.PlayerEntity.SelectedHotbarSlot],
                "equipped entity still at the selected index");
            Assert.AreSame(wA, state.PlayerEntity.EquippedWeapon, "EquippedWeapon reference unchanged");
        }

        [Test]
        public void Swap_HolsteredSelection_StaysMinusOne()
        {
            var (state, inv, _, _) = Setup();
            state.PlayerEntity.SelectedHotbarSlot = -1; // holstered

            HotbarWeaponSystem.SwapWeaponSlots(state, inv, 0, 1);

            Assert.AreEqual(-1, state.PlayerEntity.SelectedHotbarSlot, "holstered stays holstered");
        }

        [Test]
        public void Swap_RemapsPendingSlot()
        {
            var (state, inv, _, _) = Setup();
            state.PlayerEntity.PendingHotbarSlot = 1;

            HotbarWeaponSystem.SwapWeaponSlots(state, inv, 0, 1);

            Assert.AreEqual(0, state.PlayerEntity.PendingHotbarSlot);
        }

        [Test]
        public void Swap_SameIndex_IsNoOp()
        {
            var (state, inv, wA, wB) = Setup();

            HotbarWeaponSystem.SwapWeaponSlots(state, inv, 0, 0);

            Assert.AreSame(wA, state.PlayerEntity.Hotbar[0]);
            Assert.AreSame(wB, state.PlayerEntity.Hotbar[1]);
        }

        [Test]
        public void Swap_OutOfRange_IsNoOp()
        {
            var (state, inv, wA, wB) = Setup();

            HotbarWeaponSystem.SwapWeaponSlots(state, inv, 0, 5);

            Assert.AreSame(wA, state.PlayerEntity.Hotbar[0]);
            Assert.AreSame(wB, state.PlayerEntity.Hotbar[1]);
        }

        [Test]
        public void Swap_BumpsInventoryVersion_SoUiRefreshes()
        {
            var (state, inv, _, _) = Setup();
            int before = inv.Version;

            HotbarWeaponSystem.SwapWeaponSlots(state, inv, 0, 1);

            Assert.Greater(inv.Version, before, "Version must bump so InventoryWindow.RefreshAll re-renders");
        }
    }
}
