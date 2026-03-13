using NUnit.Framework;
using State;
using Systems;

namespace Tests.EditMode
{
    [TestFixture]
    public class AmmoSystemTests
    {
        // ── CountReserve ────────────────────────────────────────

        [Test]
        public void CountReserve_EmptyInventory_ReturnsZero()
        {
            var inventory = new InventoryState();
            Assert.AreEqual(0, AmmoSystem.CountReserve(inventory, "Ammo_Rifle"));
        }

        [Test]
        public void CountReserve_SingleStack_ReturnsStackCount()
        {
            var inventory = new InventoryState();
            inventory.Backpack[0] = ItemState.Create(new EId(1), "Ammo_Rifle", 30);
            Assert.AreEqual(30, AmmoSystem.CountReserve(inventory, "Ammo_Rifle"));
        }

        [Test]
        public void CountReserve_MultipleStacks_ReturnsSummedCount()
        {
            var inventory = new InventoryState();
            inventory.Backpack[0] = ItemState.Create(new EId(1), "Ammo_Rifle", 30);
            inventory.Backpack[5] = ItemState.Create(new EId(2), "Ammo_Rifle", 20);
            Assert.AreEqual(50, AmmoSystem.CountReserve(inventory, "Ammo_Rifle"));
        }

        [Test]
        public void CountReserve_MixedAmmoTypes_CountsOnlyMatchingType()
        {
            var inventory = new InventoryState();
            inventory.Backpack[0] = ItemState.Create(new EId(1), "Ammo_Rifle", 30);
            inventory.Backpack[1] = ItemState.Create(new EId(2), "Ammo_Shotgun", 10);
            Assert.AreEqual(30, AmmoSystem.CountReserve(inventory, "Ammo_Rifle"));
            Assert.AreEqual(10, AmmoSystem.CountReserve(inventory, "Ammo_Shotgun"));
        }

        [Test]
        public void CountReserve_NullAmmoType_ReturnsZero()
        {
            var inventory = new InventoryState();
            inventory.Backpack[0] = ItemState.Create(new EId(1), "Ammo_Rifle", 30);
            Assert.AreEqual(0, AmmoSystem.CountReserve(inventory, null));
        }

        // ── ConsumeAmmo ─────────────────────────────────────────

        [Test]
        public void ConsumeAmmo_PartialStack_ConsumesCorrectAmount()
        {
            var inventory = new InventoryState();
            inventory.Backpack[0] = ItemState.Create(new EId(1), "Ammo_Rifle", 30);

            int consumed = AmmoSystem.ConsumeAmmo(inventory, "Ammo_Rifle", 10);

            Assert.AreEqual(10, consumed);
            Assert.AreEqual(20, inventory.Backpack[0].StackCount);
        }

        [Test]
        public void ConsumeAmmo_ExceedsAvailable_ConsumesAllAvailable()
        {
            var inventory = new InventoryState();
            inventory.Backpack[0] = ItemState.Create(new EId(1), "Ammo_Rifle", 20);

            int consumed = AmmoSystem.ConsumeAmmo(inventory, "Ammo_Rifle", 50);

            Assert.AreEqual(20, consumed);
            Assert.IsNull(inventory.Backpack[0], "Empty slot should be nulled");
        }

        [Test]
        public void ConsumeAmmo_EntireStack_SetsSlotToNull()
        {
            var inventory = new InventoryState();
            inventory.Backpack[0] = ItemState.Create(new EId(1), "Ammo_Rifle", 30);

            AmmoSystem.ConsumeAmmo(inventory, "Ammo_Rifle", 30);

            Assert.IsNull(inventory.Backpack[0]);
        }

        [Test]
        public void ConsumeAmmo_SpansMultipleStacks()
        {
            var inventory = new InventoryState();
            inventory.Backpack[0] = ItemState.Create(new EId(1), "Ammo_Rifle", 15);
            inventory.Backpack[5] = ItemState.Create(new EId(2), "Ammo_Rifle", 15);

            int consumed = AmmoSystem.ConsumeAmmo(inventory, "Ammo_Rifle", 25);

            Assert.AreEqual(25, consumed);
            Assert.IsNull(inventory.Backpack[0], "First stack fully consumed");
            Assert.AreEqual(5, inventory.Backpack[5].StackCount, "Second stack partially consumed");
        }

        [Test]
        public void ConsumeAmmo_ZeroAmount_ReturnsZero()
        {
            var inventory = new InventoryState();
            inventory.Backpack[0] = ItemState.Create(new EId(1), "Ammo_Rifle", 30);

            int consumed = AmmoSystem.ConsumeAmmo(inventory, "Ammo_Rifle", 0);

            Assert.AreEqual(0, consumed);
            Assert.AreEqual(30, inventory.Backpack[0].StackCount);
        }

        // ── CompleteReload ──────────────────────────────────────

        [Test]
        public void CompleteReload_FillsMagazineFromReserve()
        {
            var weapon = WeaponEntityState.CreateRifle(new EId(1));
            weapon.AmmoInMagazine = 5;
            var inventory = new InventoryState();
            inventory.Backpack[0] = ItemState.Create(new EId(2), "Ammo_Rifle", 60);

            AmmoSystem.CompleteReload(weapon, inventory);

            Assert.AreEqual(30, weapon.AmmoInMagazine);
            Assert.AreEqual(35, inventory.Backpack[0].StackCount);
        }

        [Test]
        public void CompleteReload_PartialReload_InsufficientReserve()
        {
            var weapon = WeaponEntityState.CreateRifle(new EId(1));
            weapon.AmmoInMagazine = 0;
            var inventory = new InventoryState();
            inventory.Backpack[0] = ItemState.Create(new EId(2), "Ammo_Rifle", 10);

            AmmoSystem.CompleteReload(weapon, inventory);

            Assert.AreEqual(10, weapon.AmmoInMagazine);
            Assert.IsNull(inventory.Backpack[0]);
        }

        [Test]
        public void CompleteReload_FullMagazine_DoesNothing()
        {
            var weapon = WeaponEntityState.CreateRifle(new EId(1));
            weapon.AmmoInMagazine = 30; // already full
            var inventory = new InventoryState();
            inventory.Backpack[0] = ItemState.Create(new EId(2), "Ammo_Rifle", 60);

            AmmoSystem.CompleteReload(weapon, inventory);

            Assert.AreEqual(30, weapon.AmmoInMagazine);
            Assert.AreEqual(60, inventory.Backpack[0].StackCount);
        }

        [Test]
        public void CompleteReload_NullAmmoType_DoesNothing()
        {
            var weapon = WeaponEntityState.CreateRifle(new EId(1));
            weapon.AmmoType = null;
            weapon.AmmoInMagazine = 0;
            var inventory = new InventoryState();
            inventory.Backpack[0] = ItemState.Create(new EId(2), "Ammo_Rifle", 60);

            AmmoSystem.CompleteReload(weapon, inventory);

            Assert.AreEqual(0, weapon.AmmoInMagazine);
        }

        // ── CanReload ───────────────────────────────────────────

        [Test]
        public void CanReload_EmptyMagWithReserve_ReturnsTrue()
        {
            var weapon = WeaponEntityState.CreateRifle(new EId(1));
            weapon.AmmoInMagazine = 0;
            var inventory = new InventoryState();
            inventory.Backpack[0] = ItemState.Create(new EId(2), "Ammo_Rifle", 30);

            Assert.IsTrue(AmmoSystem.CanReload(weapon, inventory));
        }

        [Test]
        public void CanReload_FullMag_ReturnsFalse()
        {
            var weapon = WeaponEntityState.CreateRifle(new EId(1));
            weapon.AmmoInMagazine = 30; // full
            var inventory = new InventoryState();
            inventory.Backpack[0] = ItemState.Create(new EId(2), "Ammo_Rifle", 30);

            Assert.IsFalse(AmmoSystem.CanReload(weapon, inventory));
        }

        [Test]
        public void CanReload_EmptyMagNoReserve_ReturnsFalse()
        {
            var weapon = WeaponEntityState.CreateRifle(new EId(1));
            weapon.AmmoInMagazine = 0;
            var inventory = new InventoryState();

            Assert.IsFalse(AmmoSystem.CanReload(weapon, inventory));
        }

        [Test]
        public void CanReload_NullAmmoType_ReturnsFalse()
        {
            var weapon = WeaponEntityState.CreateRifle(new EId(1));
            weapon.AmmoType = null;
            weapon.AmmoInMagazine = 0;
            var inventory = new InventoryState();
            inventory.Backpack[0] = ItemState.Create(new EId(2), "Ammo_Rifle", 30);

            Assert.IsFalse(AmmoSystem.CanReload(weapon, inventory));
        }
    }
}
