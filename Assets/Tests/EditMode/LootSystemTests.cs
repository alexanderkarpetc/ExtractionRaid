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
            EditModeTestsUtils.EnsureAppForTests();
            _state = RaidState.Create(EditModeTestsUtils.NewAllocator());
            _events = new FakeRaidEvents();
        }

        [TearDown]
        public void TearDown() => EditModeTestsUtils.ResetApp();

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

            // Tier 4a: bot weapon must be composed через Builder pipeline so loot drop
            // (which reads bot.Weapon) has Payload + Delivery refs to derive ammo + config.
            if (BotConstants.TryGetConfig(typeId, out var cfg))
            {
                var weaponItem = ItemState.CreateWeapon(_state.AllocateEId(), "Weapon", cfg.WeaponConfig);
                bot.Weapon = Systems.WeaponSyncSystem.BuildWeaponForItem(
                    weaponItem, ApplicationCore.App.Instance.CoreDefinitions, _events);
            }

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

            bool hasWeapon = false;
            for (int i = 0; i < InventoryState.BackpackSize; i++)
            {
                if (lootable.Inventory.Backpack[i]?.DefinitionId == "Weapon")
                {
                    hasWeapon = true;
                    break;
                }
            }
            Assert.IsTrue(hasWeapon, "Scav loot should contain a weapon in the backpack");

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
            Assert.IsNotNull(FindInBackpack(loot.Inventory, "Helmet_Basic"), "Loot should contain helmet");
            Assert.IsNotNull(FindInBackpack(loot.Inventory, "Armor_Basic"), "Loot should contain body armor");
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

            var lootedHelmet = FindInBackpack(_state.Lootables[0].Inventory, "Helmet_Basic");
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

            Assert.IsNull(FindInBackpack(_state.Lootables[0].Inventory, "Helmet_Basic"),
                "Broken armor should not appear in loot");
        }

        static ItemState FindInBackpack(InventoryState inv, string definitionId)
        {
            for (int i = 0; i < InventoryState.BackpackSize; i++)
                if (inv.Backpack[i]?.DefinitionId == definitionId)
                    return inv.Backpack[i];
            return null;
        }

        static int CountInBackpack(InventoryState inv, string definitionId)
        {
            int n = 0;
            for (int i = 0; i < InventoryState.BackpackSize; i++)
                if (inv.Backpack[i]?.DefinitionId == definitionId) n++;
            return n;
        }

        // ── Loot table (BotLootConfigAsset → BotTypeConfig loot rules) ──────────

        [Test]
        public void CreateLootable_AmmoLootTable_DropsTheGunsOwnCaliber()
        {
            BotConstants.TryGetConfig("Scav", out var scav);   // Scav = BallisticRound → Ammo_Rifle
            var bot = CreateBot("Scav", Vector3.zero);

            // The caliber is never authored in the rule — it comes from the equipped payload,
            // so a bot can only drop rounds its own weapon could fire.
            var config = new BotTypeConfig(
                typeId: "Scav", prefabId: "BotShell", weaponConfig: scav.WeaponConfig,
                ammoLoot: new AmmoLootRule(minRounds: 10, maxRounds: 10));

            LootSystem.CreateLootable(_state, bot, in config, _events);

            var inv = _state.Lootables[0].Inventory;
            var dropped = FindInBackpack(inv, "Ammo_Rifle");
            Assert.IsNotNull(dropped, "Scav fires BallisticRound — the drop must be rifle ammo");
            Assert.AreEqual(10, dropped.StackCount);
        }

        [Test]
        public void CreateLootable_GuaranteedItems_DropExactCount()
        {
            BotConstants.TryGetConfig("Scav", out var scav);
            var bot = CreateBot("Scav", Vector3.zero);

            var config = new BotTypeConfig(
                typeId: "Scav", prefabId: "BotShell", weaponConfig: scav.WeaponConfig,
                guaranteedItems: new[] { new ItemCountRule("Medkit", 2, 2) });

            LootSystem.CreateLootable(_state, bot, in config, _events);

            // Medkit is non-stackable → 2 units occupy 2 slots.
            Assert.AreEqual(2, CountInBackpack(_state.Lootables[0].Inventory, "Medkit"));
        }

        [Test]
        public void CreateLootable_CategoryLoot_PicksFromCategory()
        {
            BotConstants.TryGetConfig("Scav", out var scav);
            var bot = CreateBot("Scav", Vector3.zero);

            var config = new BotTypeConfig(
                typeId: "Scav", prefabId: "BotShell", weaponConfig: scav.WeaponConfig,
                categoryLoot: new[] { new CategoryLootRule(LootCategory.Materials, 2, 2) });

            LootSystem.CreateLootable(_state, bot, in config, _events);

            int materials = 0;
            var inv = _state.Lootables[0].Inventory;
            for (int i = 0; i < InventoryState.BackpackSize; i++)
            {
                var item = inv.Backpack[i];
                if (item != null && ItemDefinition.Get(item.DefinitionId)?.Category == ItemCategory.Material)
                    materials++;
            }
            Assert.AreEqual(2, materials, "Should pick exactly 2 distinct Material-category items");
        }

        // ── Tier 6 G2: Module Loot Economy ────────────────────

        static readonly System.Collections.Generic.HashSet<string> ExpectedModuleIds =
            new() { "BallisticRound", "LaserCharge", "SingleAction", "Auto", "Scatter" };

        [Test]
        public void ModuleCache_RegistryLookup_Succeeds()
        {
            Assert.IsTrue(ContainerConstants.TryGetConfig("ModuleCache", out var config));
            Assert.AreEqual("ModuleCache",  config.TypeId);
            Assert.AreEqual("Module Cache", config.DisplayName);
            Assert.AreEqual(1, config.MinDrops);
            Assert.AreEqual(3, config.MaxDrops);

            // Build-parts cache = the cores bucket + the attachments bucket. WHICH part comes
            // out of each is ItemBalance's call, so the config only names the two buckets.
            Assert.AreEqual(2, config.RandomPool.Length);
            foreach (var entry in config.RandomPool)
            {
                Assert.IsTrue(entry.IsCategory, "ModuleCache rolls whole buckets, not fixed items.");
                Assert.IsTrue(entry.Category == LootCategory.WeaponCores
                              || entry.Category == LootCategory.Attachments,
                    $"Unexpected bucket '{entry.Category}' in ModuleCache.");

                foreach (var def in LootConstants.CandidatesFor(entry.Category))
                    Assert.AreEqual(ItemCategory.WeaponMod, def.Category,
                        $"'{def.Id}' resolved from {entry.Category} is not a WeaponMod item.");
            }
        }

        [Test]
        public void RandomLootBox_IncludesAllWeaponModules()
        {
            Assert.IsTrue(ContainerConstants.TryGetConfig(ContainerType.RandomLootBox, out var config));

            bool hasCores = false;
            foreach (var entry in config.RandomPool)
                if (entry.IsCategory && entry.Category == LootCategory.WeaponCores) hasCores = true;
            Assert.IsTrue(hasCores, "RandomLootBox should be able to roll weapon cores.");

            var coreIds = new System.Collections.Generic.HashSet<string>();
            foreach (var def in LootConstants.CandidatesFor(LootCategory.WeaponCores))
                coreIds.Add(def.Id);

            foreach (var moduleId in ExpectedModuleIds)
                Assert.IsTrue(coreIds.Contains(moduleId),
                    $"WeaponCores bucket missing weapon module '{moduleId}'.");
        }

        [Test]
        public void CreateContainer_ModuleCache_DropsOnlyBuildParts()
        {
            // Deterministic RNG so the 10 runs cover both buckets (cores + attachments).
            UnityEngine.Random.InitState(0xC0FFEE);

            ContainerConstants.TryGetConfig(ContainerType.ModuleCache, out var config);

            int totalSpawned = 0;
            for (int run = 0; run < 10; run++)
            {
                int beforeCount = _state.Lootables.Count;
                LootSystem.CreateContainer(_state, in config, Vector3.zero, _events);
                Assert.AreEqual(beforeCount + 1, _state.Lootables.Count);

                var inv = _state.Lootables[_state.Lootables.Count - 1].Inventory;
                int spawnedThisRun = 0;
                for (int slot = 0; slot < InventoryState.BackpackSize; slot++)
                {
                    var item = inv.Backpack[slot];
                    if (item == null) continue;
                    Assert.AreEqual(ItemCategory.WeaponMod, item.Definition.Category,
                        $"ModuleCache produced non-build-part item '{item.DefinitionId}'.");
                    Assert.IsFalse(item.HasWeaponConfiguration,
                        "Loose cores/mods ride as plain items, not assembled weapons.");
                    spawnedThisRun++;
                }
                Assert.GreaterOrEqual(spawnedThisRun, config.MinDrops);
                Assert.LessOrEqual   (spawnedThisRun, config.MaxDrops);
                totalSpawned += spawnedThisRun;
            }
            Assert.Greater(totalSpawned, 0, "ModuleCache should spawn at least one module across 10 runs.");
        }

        // ── Containers: hardcoded drops + balance-driven rolls ──────────

        [Test]
        public void CreateContainer_GuaranteedDrops_AlwaysSpawn()
        {
            // The "starting chest always holds a pistol" shape: hardcoded contents, no rolls.
            var config = new ContainerTypeConfig(
                typeId: "TestChest", displayName: "Test Chest",
                minDrops: 0, maxDrops: 0,
                randomPool: System.Array.Empty<LootPoolEntry>(),
                guaranteedDrops: new[]
                {
                    new LootDrop("Medkit", 1, 1),
                    new LootDrop("Bandage", 3, 3),
                });

            for (int run = 0; run < 5; run++)
            {
                LootSystem.CreateContainer(_state, in config, Vector3.zero, _events);
                var inv = _state.Lootables[_state.Lootables.Count - 1].Inventory;

                Assert.AreEqual(1, CountInBackpack(inv, "Medkit"), "Guaranteed medkit missing.");
                var bandages = FindInBackpack(inv, "Bandage");
                Assert.IsNotNull(bandages, "Guaranteed bandages missing.");
                Assert.AreEqual(3, bandages.StackCount, "Guaranteed count must be exact, not rolled.");
            }
        }

        [Test]
        public void CreateContainer_GuaranteedDrop_CountFromBalance_UsesBalanceRange()
        {
            var config = new ContainerTypeConfig(
                typeId: "TestChest", displayName: "Test Chest",
                minDrops: 0, maxDrops: 0,
                randomPool: System.Array.Empty<LootPoolEntry>(),
                guaranteedDrops: new[] { LootDrop.FromBalance("Bandage") });

            ItemBalanceAsset.DropCountRangeOf("Bandage", out int min, out int max);

            for (int run = 0; run < 20; run++)
            {
                LootSystem.CreateContainer(_state, in config, Vector3.zero, _events);
                var bandages = FindInBackpack(_state.Lootables[_state.Lootables.Count - 1].Inventory, "Bandage");
                Assert.IsNotNull(bandages);
                Assert.GreaterOrEqual(bandages.StackCount, min);
                Assert.LessOrEqual(bandages.StackCount, max);
            }
        }

        [Test]
        public void CreateContainer_CategoryPool_RollsOnlyThatCategory()
        {
            UnityEngine.Random.InitState(1234);

            var config = new ContainerTypeConfig(
                typeId: "TestMeds", displayName: "Test Meds",
                minDrops: 3, maxDrops: 3,
                randomPool: new[] { LootPoolEntry.FromCategory(LootCategory.Meds) });

            int seen = 0;
            for (int run = 0; run < 10; run++)
            {
                LootSystem.CreateContainer(_state, in config, Vector3.zero, _events);
                var inv = _state.Lootables[_state.Lootables.Count - 1].Inventory;
                for (int slot = 0; slot < InventoryState.BackpackSize; slot++)
                {
                    var item = inv.Backpack[slot];
                    if (item == null) continue;
                    Assert.AreEqual(ItemCategory.Meds, item.Definition.Category,
                        $"Meds-only pool produced '{item.DefinitionId}'.");
                    seen++;
                }
            }
            Assert.Greater(seen, 0, "A Meds pool should produce items.");
        }

        [Test]
        public void CreateContainer_RolledStackSize_ComesFromBalance()
        {
            UnityEngine.Random.InitState(99);

            // A named-item pool entry still defers its stack size to the balance table.
            var config = new ContainerTypeConfig(
                typeId: "TestAmmo", displayName: "Test Ammo",
                minDrops: 1, maxDrops: 1,
                randomPool: new[] { LootPoolEntry.FromItem("Ammo_Rifle") });

            ItemBalanceAsset.DropCountRangeOf("Ammo_Rifle", out int min, out int max);
            Assert.Greater(max, 1, "Ammo_Rifle is expected to drop in stacks, not singles.");

            for (int run = 0; run < 20; run++)
            {
                LootSystem.CreateContainer(_state, in config, Vector3.zero, _events);
                var ammo = FindInBackpack(_state.Lootables[_state.Lootables.Count - 1].Inventory, "Ammo_Rifle");
                Assert.IsNotNull(ammo);
                Assert.GreaterOrEqual(ammo.StackCount, min);
                Assert.LessOrEqual(ammo.StackCount, max);
            }
        }

        [Test]
        public void CreateContainer_SlotCount_ClampsLoot()
        {
            var config = new ContainerTypeConfig(
                typeId: "TestTiny", displayName: "Tiny",
                minDrops: 10, maxDrops: 10,
                randomPool: new[] { LootPoolEntry.FromCategory(LootCategory.Meds) },
                guaranteedDrops: null,
                slotCount: 2);

            LootSystem.CreateContainer(_state, in config, Vector3.zero, _events);

            var inv = _state.Lootables[0].Inventory;
            int used = 0;
            for (int slot = 0; slot < InventoryState.BackpackSize; slot++)
                if (inv.Backpack[slot] != null) used++;
            Assert.AreEqual(2, used, "A 2-slot container must never hold more than 2 stacks.");
        }
    }
}
