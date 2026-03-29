using Constants;
using NUnit.Framework;
using State;
using Systems;
using Tests.EditMode.Fakes;
using UnityEngine;

namespace Tests.EditMode
{
    [TestFixture]
    public class LootSystemTests
    {
        RaidState _state;
        FakeRaidEvents _events;

        [SetUp]
        public void SetUp()
        {
            _state = RaidState.Create();
            _events = new FakeRaidEvents();
        }

        BotEntityState CreateBot(string typeId, Vector3 position, int medkits = 0, int grenades = 0)
        {
            var bot = new BotEntityState
            {
                Id = _state.AllocateEId(),
                TypeId = typeId,
                Position = position,
                Blackboard = new BotBlackboard(),
            };
            bot.Blackboard.Reset();
            bot.Blackboard.MedkitsRemaining = medkits;
            bot.Blackboard.GrenadesRemaining = grenades;
            return bot;
        }

        [Test]
        public void CreateLootable_FromScav_ContainsWeaponAndAmmo()
        {
            BotConstants.TryGetConfig("Scav", out var config);
            var bot = CreateBot("Scav", new Vector3(5f, 0f, 5f));

            LootSystem.CreateLootable(_state, bot, in config, _events);

            Assert.AreEqual(1, _state.Lootables.Count);
            var lootable = _state.Lootables[0];
            Assert.AreEqual("Scav", lootable.TypeId);
            Assert.AreEqual(bot.Position, lootable.Position);

            Assert.IsNotNull(lootable.Inventory.WeaponSlots[0], "Scav loot should contain a weapon");

            bool hasAmmo = false;
            for (int i = 0; i < InventoryState.BackpackSize; i++)
            {
                if (lootable.Inventory.Backpack[i] != null &&
                    lootable.Inventory.Backpack[i].DefinitionId.StartsWith("Ammo_"))
                {
                    hasAmmo = true;
                    break;
                }
            }
            Assert.IsTrue(hasAmmo, "Scav loot should contain ammo");
            Assert.IsTrue(_events.LootableSpawnedCalled);
        }

        [Test]
        public void CreateLootable_FromPMC_ContainsMedkitsAndGrenades()
        {
            BotConstants.TryGetConfig("PMC", out var config);
            var bot = CreateBot("PMC", Vector3.zero, medkits: 2, grenades: 1);

            LootSystem.CreateLootable(_state, bot, in config, _events);

            Assert.AreEqual(1, _state.Lootables.Count);
            var inv = _state.Lootables[0].Inventory;

            int medkitCount = 0;
            int grenadeCount = 0;
            for (int i = 0; i < InventoryState.BackpackSize; i++)
            {
                if (inv.Backpack[i]?.DefinitionId == "Medkit") medkitCount++;
                if (inv.Backpack[i]?.DefinitionId == "Grenade") grenadeCount++;
            }
            Assert.AreEqual(2, medkitCount);
            Assert.AreEqual(1, grenadeCount);
        }

        [Test]
        public void TryTransfer_MovesItemBetweenInventories()
        {
            var from = new InventoryState();
            var to = new InventoryState();

            from.Backpack[0] = ItemState.Create(_state.AllocateEId(), "Medkit", 1);

            bool result = LootSystem.TryTransfer(
                from, InventorySlotRef.BackpackSlot(0),
                to, InventorySlotRef.BackpackSlot(0));

            Assert.IsTrue(result);
            Assert.IsNull(from.Backpack[0]);
            Assert.IsNotNull(to.Backpack[0]);
            Assert.AreEqual("Medkit", to.Backpack[0].DefinitionId);
        }

        [Test]
        public void TryTransfer_SwapsItems()
        {
            var from = new InventoryState();
            var to = new InventoryState();

            from.Backpack[0] = ItemState.Create(_state.AllocateEId(), "Medkit", 1);
            to.Backpack[0] = ItemState.Create(_state.AllocateEId(), "Grenade");

            bool result = LootSystem.TryTransfer(
                from, InventorySlotRef.BackpackSlot(0),
                to, InventorySlotRef.BackpackSlot(0));

            Assert.IsTrue(result);
            Assert.AreEqual("Grenade", from.Backpack[0].DefinitionId);
            Assert.AreEqual("Medkit", to.Backpack[0].DefinitionId);
        }

        [Test]
        public void TryTransfer_RespectsAllowedSlots()
        {
            var from = new InventoryState();
            var to = new InventoryState();

            from.Backpack[0] = ItemState.Create(_state.AllocateEId(), "Medkit", 1);

            bool result = LootSystem.TryTransfer(
                from, InventorySlotRef.BackpackSlot(0),
                to, InventorySlotRef.Weapon(0));

            Assert.IsFalse(result, "Medkit should not go into a weapon slot");
            Assert.IsNotNull(from.Backpack[0]);
        }

        [Test]
        public void FindNearestLootable_WithinRange_ReturnsId()
        {
            BotConstants.TryGetConfig("Scav", out var config);
            var bot = CreateBot("Scav", new Vector3(2f, 0f, 0f));
            LootSystem.CreateLootable(_state, bot, in config, _events);

            var result = LootSystem.FindNearestLootable(_state, Vector3.zero);

            Assert.IsTrue(result.IsValid);
            Assert.AreEqual(_state.Lootables[0].Id, result);
        }

        [Test]
        public void FindNearestLootable_OutOfRange_ReturnsNone()
        {
            BotConstants.TryGetConfig("Scav", out var config);
            var bot = CreateBot("Scav", new Vector3(100f, 0f, 0f));
            LootSystem.CreateLootable(_state, bot, in config, _events);

            var result = LootSystem.FindNearestLootable(_state, Vector3.zero);

            Assert.IsFalse(result.IsValid);
        }

        // ── Armor Loot ────────────────────────────────────────

        [Test]
        public void CreateLootable_BotWithArmor_LootContainsArmor()
        {
            BotConstants.TryGetConfig("PMC", out var config);
            var bot = CreateBot("PMC", Vector3.zero);

            // Simulate armor in ArmorMap (as BotSpawnSystem would do)
            _state.ArmorMap[bot.Id] = new ArmorSlotState
            {
                Helmet = ArmorState.Create(30f, 100f),
                BodyArmor = ArmorState.Create(40f, 120f),
            };

            LootSystem.CreateLootable(_state, bot, in config, _events);

            var loot = _state.Lootables[0];
            Assert.IsNotNull(loot.Inventory.HelmetSlot, "Loot should contain helmet");
            Assert.IsNotNull(loot.Inventory.BodyArmorSlot, "Loot should contain body armor");
        }

        [Test]
        public void CreateLootable_BotArmorDamaged_LootPreservesDurability()
        {
            BotConstants.TryGetConfig("Scav", out var config);
            var bot = CreateBot("Scav", Vector3.zero);

            var helmet = ArmorState.Create(30f, 100f);
            helmet.CurrentDurability = 45f; // combat damaged
            _state.ArmorMap[bot.Id] = new ArmorSlotState { Helmet = helmet };

            LootSystem.CreateLootable(_state, bot, in config, _events);

            var lootedHelmet = _state.Lootables[0].Inventory.HelmetSlot;
            Assert.IsNotNull(lootedHelmet);
            Assert.AreEqual(45f, lootedHelmet.CurrentDurability, 0.001f);
            Assert.AreEqual(100f, lootedHelmet.MaxDurability, 0.001f);
        }

        [Test]
        public void CreateLootable_BotBrokenArmor_NotInLoot()
        {
            BotConstants.TryGetConfig("Scav", out var config);
            var bot = CreateBot("Scav", Vector3.zero);

            var helmet = ArmorState.Create(30f, 100f);
            helmet.CurrentDurability = 0f; // broken!
            _state.ArmorMap[bot.Id] = new ArmorSlotState { Helmet = helmet };

            LootSystem.CreateLootable(_state, bot, in config, _events);

            Assert.IsNull(_state.Lootables[0].Inventory.HelmetSlot,
                "Broken armor should not appear in loot");
        }
    }
}
