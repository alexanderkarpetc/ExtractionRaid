using NUnit.Framework;
using State;
using Systems;

namespace Tests.EditMode
{
    /// <summary>
    /// M1.1 gear-loss (KIA risk loop) + baseline-floor gate. Covers the pure pieces:
    ///  - <see cref="InventoryState.ClearAll"/> — the wipe primitive App.EndRaid runs on KIA.
    ///  - <see cref="PlayerSpawnSystem.IsInventoryEmpty"/> — gates the baseline-floor grant
    ///    (fresh save or post-death-wipe → floor; re-geared from stash → keep gear).
    /// The full death→save→redeploy loop + loadout content are integration-tested manually.
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

        // ── Baseline-floor gate (grant when empty; keep gear otherwise) ──

        [Test]
        public void IsInventoryEmpty_FreshInventory_True()
        {
            Assert.IsTrue(PlayerSpawnSystem.IsInventoryEmpty(new InventoryState()));
        }

        [Test]
        public void IsInventoryEmpty_AfterWipe_True()
        {
            var inv = new InventoryState();
            inv.WeaponSlots[0] = MakeItem("Weapon");
            inv.Backpack[3] = MakeItem("Medkit");
            inv.ClearAll();
            Assert.IsTrue(PlayerSpawnSystem.IsInventoryEmpty(inv));
        }

        [Test]
        public void IsInventoryEmpty_WithAnyItem_False()
        {
            var backpackOnly = new InventoryState();
            backpackOnly.Backpack[5] = MakeItem("Bandage");
            Assert.IsFalse(PlayerSpawnSystem.IsInventoryEmpty(backpackOnly));

            var weaponOnly = new InventoryState();
            weaponOnly.WeaponSlots[0] = MakeItem("Weapon");
            Assert.IsFalse(PlayerSpawnSystem.IsInventoryEmpty(weaponOnly));

            var armorOnly = new InventoryState();
            armorOnly.HelmetSlot = MakeItem("Helmet_Basic");
            Assert.IsFalse(PlayerSpawnSystem.IsInventoryEmpty(armorOnly));
        }

        [Test]
        public void IsTestRange_ClassifiesRangesVsRealLevels()
        {
            Assert.IsTrue(PlayerSpawnSystem.IsTestRange("shooting_range"));
            Assert.IsTrue(PlayerSpawnSystem.IsTestRange("horde_range"));
            Assert.IsFalse(PlayerSpawnSystem.IsTestRange("main_map"));
            Assert.IsFalse(PlayerSpawnSystem.IsTestRange("hideout"));
        }
    }
}
