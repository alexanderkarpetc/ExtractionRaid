using NUnit.Framework;
using State;
using Systems;

namespace Tests.EditMode
{
    /// <summary>
    /// M1.1 gear-loss (KIA risk loop). Covers the two pure pieces:
    ///  - <see cref="InventoryState.ClearAll"/> — the wipe primitive App.EndRaid runs on KIA.
    ///  - <see cref="PlayerSpawnSystem.ShouldGrantStartingKit"/> — the fresh-player kit gate
    ///    that must NOT re-grant a free kit after a death-wipe.
    /// The full death→save→redeploy loop is integration-tested manually (touches SaveManager
    /// file IO + scene loads).
    /// </summary>
    [TestFixture]
    public class GearLossTests
    {
        System.Func<EId> _alloc;

        [SetUp]
        public void SetUp()
        {
            EditModeTestsUtils.EnsureAppForTests();
            _alloc = EditModeTestsUtils.NewAllocator();
        }

        [TearDown]
        public void TearDown() => EditModeTestsUtils.ResetApp();

        ItemState MakeItem(string defId, int stack = 1) => ItemState.Create(_alloc(), defId, stack);

        // ── InventoryState.ClearAll (KIA wipe primitive) ─────────────────

        [Test]
        public void ClearAll_NullsEverySlotAndUnbindsQuickSlots()
        {
            var inv = new InventoryState();
            inv.WeaponSlots[0] = MakeItem("Weapon");
            inv.WeaponSlots[1] = MakeItem("Weapon");
            inv.HelmetSlot = MakeItem("Helmet_Basic");
            inv.BodyArmorSlot = MakeItem("Armor_Basic");
            inv.Backpack[0] = MakeItem("Medkit");
            inv.Backpack[19] = MakeItem("Bandage");
            inv.QuickSlotBindings[0] = 0;
            inv.QuickSlotBindings[6] = 19;

            inv.ClearAll();

            for (int i = 0; i < InventoryState.WeaponSlotCount; i++)
                Assert.IsNull(inv.WeaponSlots[i]);
            Assert.IsNull(inv.HelmetSlot);
            Assert.IsNull(inv.BodyArmorSlot);
            for (int i = 0; i < InventoryState.BackpackSize; i++)
                Assert.IsNull(inv.Backpack[i]);
            for (int i = 0; i < InventoryState.QuickSlotCount; i++)
                Assert.AreEqual(-1, inv.QuickSlotBindings[i]);
        }

        [Test]
        public void ClearAll_BumpsVersion()
        {
            var inv = new InventoryState();
            int before = inv.Version;
            inv.ClearAll();
            Assert.Greater(inv.Version, before);
        }

        // ── Starting-kit gate (grant once; re-gear from stash after death) ──

        [Test]
        public void ShouldGrantStartingKit_TestRange_AlwaysTrue_EvenAfterKit()
        {
            Assert.IsTrue(PlayerSpawnSystem.ShouldGrantStartingKit("shooting_range", hasReceivedStartingKit: true));
            Assert.IsTrue(PlayerSpawnSystem.ShouldGrantStartingKit("horde_range", hasReceivedStartingKit: true));
        }

        [Test]
        public void ShouldGrantStartingKit_FreshRealPlayer_True()
        {
            Assert.IsTrue(PlayerSpawnSystem.ShouldGrantStartingKit("main_map", hasReceivedStartingKit: false));
        }

        [Test]
        public void ShouldGrantStartingKit_AfterDeath_RealRaid_False()
        {
            // KIA wiped the inventory but the flag persists → no free re-kit; the player
            // must re-gear from stash at the hideout.
            Assert.IsFalse(PlayerSpawnSystem.ShouldGrantStartingKit("main_map", hasReceivedStartingKit: true));
        }
    }
}
