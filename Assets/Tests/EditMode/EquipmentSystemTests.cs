using NUnit.Framework;
using State;
using Systems;

namespace Tests.EditMode
{
    [TestFixture]
    public class EquipmentSystemTests
    {
        [Test]
        public void SyncArmor_HelmetEquipped_PopulatesArmorMap()
        {
            var state = RaidState.Create(EditModeTestsUtils.NewAllocator());
            var entityId = state.AllocateEId();
            var inventory = new InventoryState();
            inventory.HelmetSlot = ItemState.Create(new EId(100), "Helmet_Basic");

            EquipmentSystem.SyncArmorFromInventory(state, entityId, inventory);

            Assert.IsTrue(state.ArmorMap.ContainsKey(entityId));
            Assert.IsNotNull(state.ArmorMap[entityId].Helmet);
            Assert.AreEqual(30f, state.ArmorMap[entityId].Helmet.ArmorPoints, 0.001f);
            Assert.IsNull(state.ArmorMap[entityId].BodyArmor);
        }

        [Test]
        public void SyncArmor_BothSlots_PopulatesBoth()
        {
            var state = RaidState.Create(EditModeTestsUtils.NewAllocator());
            var entityId = state.AllocateEId();
            var inventory = new InventoryState();
            inventory.HelmetSlot = ItemState.Create(new EId(100), "Helmet_Basic");
            inventory.BodyArmorSlot = ItemState.Create(new EId(101), "Armor_Basic");

            EquipmentSystem.SyncArmorFromInventory(state, entityId, inventory);

            Assert.IsNotNull(state.ArmorMap[entityId].Helmet);
            Assert.IsNotNull(state.ArmorMap[entityId].BodyArmor);
            Assert.AreEqual(30f, state.ArmorMap[entityId].Helmet.ArmorPoints, 0.001f);
            Assert.AreEqual(40f, state.ArmorMap[entityId].BodyArmor.ArmorPoints, 0.001f);
        }

        [Test]
        public void SyncArmor_NoEquipment_NoArmorMapEntry()
        {
            var state = RaidState.Create(EditModeTestsUtils.NewAllocator());
            var entityId = state.AllocateEId();
            var inventory = new InventoryState();

            EquipmentSystem.SyncArmorFromInventory(state, entityId, inventory);

            Assert.IsFalse(state.ArmorMap.ContainsKey(entityId));
        }

        [Test]
        public void SyncArmor_RemovesPreviousEntry_WhenUnequipped()
        {
            var state = RaidState.Create(EditModeTestsUtils.NewAllocator());
            var entityId = state.AllocateEId();
            var inventory = new InventoryState();
            inventory.HelmetSlot = ItemState.Create(new EId(100), "Helmet_Basic");

            EquipmentSystem.SyncArmorFromInventory(state, entityId, inventory);
            Assert.IsTrue(state.ArmorMap.ContainsKey(entityId));

            // Unequip
            inventory.HelmetSlot = null;
            EquipmentSystem.SyncArmorFromInventory(state, entityId, inventory);
            Assert.IsFalse(state.ArmorMap.ContainsKey(entityId));
        }

        [Test]
        public void SyncArmor_ItemWithCustomDurability_PreservesDurability()
        {
            var state = RaidState.Create(EditModeTestsUtils.NewAllocator());
            var entityId = state.AllocateEId();
            var inventory = new InventoryState();

            var item = ItemState.Create(new EId(100), "Helmet_Basic");
            item.CurrentDurability = 55f;
            item.MaxDurability = 80f; // repaired, max reduced from 100
            inventory.HelmetSlot = item;

            EquipmentSystem.SyncArmorFromInventory(state, entityId, inventory);

            var helmet = state.ArmorMap[entityId].Helmet;
            Assert.AreEqual(55f, helmet.CurrentDurability, 0.001f);
            Assert.AreEqual(80f, helmet.MaxDurability, 0.001f);
        }

        [Test]
        public void SyncArmor_ItemWithDefaultDurability_UsesDefinition()
        {
            var state = RaidState.Create(EditModeTestsUtils.NewAllocator());
            var entityId = state.AllocateEId();
            var inventory = new InventoryState();
            inventory.HelmetSlot = ItemState.Create(new EId(100), "Helmet_Basic");
            // No custom durability set (defaults: -1)

            EquipmentSystem.SyncArmorFromInventory(state, entityId, inventory);

            var helmet = state.ArmorMap[entityId].Helmet;
            Assert.AreEqual(100f, helmet.MaxDurability, 0.001f); // from ItemDefinition
            Assert.AreEqual(100f, helmet.CurrentDurability, 0.001f);
        }

        [Test]
        public void SyncArmor_NonArmorItem_Ignored()
        {
            var state = RaidState.Create(EditModeTestsUtils.NewAllocator());
            var entityId = state.AllocateEId();
            var inventory = new InventoryState();
            // Put a non-armor item in helmet slot (shouldn't happen, but edge case)
            inventory.HelmetSlot = ItemState.Create(new EId(100), "Ammo_Rifle");

            EquipmentSystem.SyncArmorFromInventory(state, entityId, inventory);

            Assert.IsFalse(state.ArmorMap.ContainsKey(entityId));
        }
    }
}
