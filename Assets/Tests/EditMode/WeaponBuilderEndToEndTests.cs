using Adapters;
using NUnit.Framework;
using State;
using Systems;
using Tests.EditMode.Fakes;
using UnityEngine;
using View.UI.WeaponBuilder;

namespace Tests.EditMode
{
    /// <summary>
    /// Full vertical-slice test: Weapon Builder UX → runtime weapon state.
    ///
    /// Simulates the Tier 1 player flow without actually running the UI:
    ///   1. Presenter (= what the UI drives) selects Payload + Delivery
    ///   2. Build commits an <see cref="ItemState"/> into the inventory
    ///   3. <see cref="WeaponSyncSystem.BuildWeaponForItem"/> promotes that item
    ///      into a runtime <see cref="WeaponEntityState"/>
    ///
    /// Every boundary is exercised: stats compose from real SO assets, config travels
    /// with the item, and the runtime weapon has working stats matching the Builder preview.
    /// </summary>
    [TestFixture]
    public class WeaponBuilderEndToEndTests
    {
        CoreDefinitionDatabase     _db;
        BallisticPayloadDefinition _ballistic;
        DeliveryCoreDefinition     _singleAction;
        DeliveryCoreDefinition     _auto;
        // Tier 8.x*: stub base prefab (payload) drives runtime PrefabId; barrel stubs are
        // visual references only (delivered through Delivery._barrelPrefab).
        GameObject                 _ballisticBaseStub;
        GameObject                 _pistolPrefabStub;
        GameObject                 _riflePrefabStub;
        ICoreDefinitionRegistry    _registry;
        InventoryState             _inventory;
        FakeRaidEvents             _events;
        int                        _nextEId;

        EId AllocateEId() => new EId(++_nextEId);

        [SetUp]
        public void SetUp()
        {
            _ballisticBaseStub = WeaponBuilderTestFactory.MakeStubBasePrefab("Module_Payload_Ballistic");
            _ballistic = WeaponBuilderTestFactory.MakeBallistic(
                "BallisticRound", displayName: "Ballistic", ammoType: "Ammo_Rifle",
                commonStats: new CommonPayloadStats
                {
                    Damage                   = 15f,
                    ProjectileSpeed          = 25f,
                    ProjectileLifetime       = 2.5f,
                    HeadshotDamageMultiplier = 2.0f,
                    BasePenetration          = 15f,
                    BaseArmorDamage          = 5f,
                },
                basePrefab: _ballisticBaseStub);
            _pistolPrefabStub = WeaponBuilderTestFactory.MakeStubBarrelPrefab("Module_Delivery_Pistol");
            _riflePrefabStub  = WeaponBuilderTestFactory.MakeStubBarrelPrefab("Module_Delivery_Rifle");

            _singleAction = WeaponBuilderTestFactory.MakeDelivery(
                "SingleAction", formFactor: "Pistol", pattern: FiringPattern.Single,
                commonStats: new DeliveryStats
                {
                    FireInterval       = 0.4f,
                    ProjectilesPerShot = 1,
                    ConeHalfAngle      = 35f,
                    MagazineSize       = 12,
                    ReloadTime         = 1.5f,
                    EquipTime          = 0.2f,
                },
                barrelPrefab: _pistolPrefabStub);
            _auto = WeaponBuilderTestFactory.MakeDelivery(
                "Auto", formFactor: "Rifle", pattern: FiringPattern.Auto,
                commonStats: new DeliveryStats
                {
                    FireInterval       = 0.2f,
                    ProjectilesPerShot = 1,
                    ConeHalfAngle      = 45f,
                    MagazineSize       = 30,
                    ReloadTime         = 2.0f,
                    EquipTime          = 0.3f,
                },
                barrelPrefab: _riflePrefabStub);

            _db = WeaponBuilderTestFactory.MakeDatabase(
                payloads:   new PayloadCoreDefinition[]  { _ballistic },
                deliveries: new DeliveryCoreDefinition[] { _singleAction, _auto });
            _registry  = WeaponBuilderTestFactory.MakeRegistry(_db);
            _inventory = new InventoryState();
            _events    = new FakeRaidEvents();
            _nextEId   = 0;
        }

        [TearDown]
        public void TearDown() =>
            WeaponBuilderTestFactory.DestroyAll(
                _ballistic, _singleAction, _auto, _db,
                _ballisticBaseStub, _pistolPrefabStub, _riflePrefabStub);

        /// <summary>Places payload + delivery module items into free backpack slots
        /// so TryBuild's module-consumption check passes (Tier 6 G6).</summary>
        void PutModulesInBackpack(string payloadId, string deliveryId)
        {
            int slot = 0;
            while (slot < InventoryState.BackpackSize && _inventory.Backpack[slot] != null) slot++;
            if (slot < InventoryState.BackpackSize)
                _inventory.Backpack[slot++] = ItemState.Create(AllocateEId(), payloadId);
            while (slot < InventoryState.BackpackSize && _inventory.Backpack[slot] != null) slot++;
            if (slot < InventoryState.BackpackSize)
                _inventory.Backpack[slot] = ItemState.Create(AllocateEId(), deliveryId);
        }

        // ── Core vertical slice: build → equip → runtime state ─

        [Test]
        public void FullFlow_BuildBallisticPistol_ProducesRuntimeWeaponWithMatchingStats()
        {
            PutModulesInBackpack("BallisticRound", "SingleAction");

            var presenter = new WeaponBuilderPresenter(_registry, _inventory, AllocateEId);

            // === 1. UX — select both slots
            presenter.SelectPayload("BallisticRound");
            presenter.SelectDelivery("SingleAction");

            Assert.IsTrue(presenter.CanBuild);
            Assert.AreEqual("Ballistic Pistol", presenter.PreviewArchetype);
            var previewStats = presenter.PreviewStats.Value;

            // === 2. UX — Build → item in backpack (lands at slot 0 — payload module's
            // freed slot)
            Assert.IsTrue(presenter.TryBuild(out var reason), reason);
            var builtItem = _inventory.Backpack[0];
            Assert.IsNotNull(builtItem);
            Assert.IsTrue(builtItem.HasWeaponConfiguration);
            Assert.AreEqual("Weapon", builtItem.DefinitionId, "Generic DefinitionId — identity lives in WeaponConfiguration");
            Assert.AreEqual("BallisticRound", builtItem.WeaponConfiguration.Payload.DefinitionId);
            Assert.AreEqual("SingleAction",   builtItem.WeaponConfiguration.Delivery.DefinitionId);

            // === 3. Runtime — equivalent of equipping: run through assembly pipeline
            var runtime = WeaponSyncSystem.BuildWeaponForItem(builtItem, _registry, _events);
            Assert.IsNotNull(runtime, "Assembly should not fail for a freshly built item");
            Assert.IsEmpty(_events.WeaponAssemblyFailures);

            // Identity + resolved defs
            Assert.AreEqual("BallisticRound", runtime.PayloadCore.DefinitionId);
            Assert.AreEqual("SingleAction",   runtime.DeliveryCore.DefinitionId);
            Assert.AreSame(_ballistic,    runtime.PayloadDefinition);
            Assert.AreSame(_singleAction, runtime.DeliveryDefinition);
            Assert.IsFalse(runtime.HasExotic);

            // Runtime stats match preview exactly (asset-driven, single source of truth)
            Assert.AreEqual(previewStats.Damage,       runtime.Stats.Damage);
            Assert.AreEqual(previewStats.FireInterval, runtime.Stats.FireInterval);
            Assert.AreEqual(previewStats.MagazineSize, runtime.Stats.MagazineSize);
            Assert.AreEqual(previewStats.ReloadTime,   runtime.Stats.ReloadTime);

            // Initial runtime fields
            Assert.AreEqual("Ammo_Rifle",     runtime.AmmoType, "AmmoType from payload definition");
            Assert.AreEqual(12,               runtime.AmmoInMagazine, "Magazine starts full");
            Assert.AreEqual(WeaponPhase.Ready, runtime.Phase);
            Assert.AreEqual("Module_Payload_Ballistic", runtime.PrefabId, "PrefabId mirrors PayloadDefinition.BasePrefab.name");
        }

        [Test]
        public void FullFlow_BuildBallisticRifle_ProducesRifleVariant()
        {
            PutModulesInBackpack("BallisticRound", "Auto");

            var presenter = new WeaponBuilderPresenter(_registry, _inventory, AllocateEId);

            presenter.SelectPayload("BallisticRound");
            presenter.SelectDelivery("Auto");
            Assert.IsTrue(presenter.TryBuild(out _));

            var runtime = WeaponSyncSystem.BuildWeaponForItem(_inventory.Backpack[0], _registry, _events);

            Assert.AreEqual("Ballistic Rifle", WeaponArchetypeLabel.Compose(_ballistic, _auto));
            Assert.AreEqual(30,  runtime.Stats.MagazineSize);
            Assert.AreEqual(0.2f, runtime.Stats.FireInterval);
            Assert.AreEqual("Module_Payload_Ballistic", runtime.PrefabId);
        }

        // ── Tier 8 Wave B: payload prefab attachment ──────────────────

        [Test]
        public void Assembly_PropagatesPayloadBasePrefab_ToRuntimeState()
        {
            // Tier 8.x*: payload SO carries _basePrefab (weapon root). WeaponSyncSystem
            // reads PayloadDefinition.BasePrefab → WeaponEntityState.BasePrefab for view.
            var baseStub = WeaponBuilderTestFactory.MakeStubBasePrefab("Module_Payload_Ballistic");
            var ballisticWithBase = WeaponBuilderTestFactory.MakeBallistic(
                id: "BallisticWithBase",
                ammoType: "Ammo_Rifle",
                commonStats: new CommonPayloadStats { Damage = 10f },
                basePrefab: baseStub);
            var db2 = WeaponBuilderTestFactory.MakeDatabase(
                payloads:   new PayloadCoreDefinition[]  { ballisticWithBase },
                deliveries: new[] { _auto });
            var registry2 = new DatabaseCoreDefinitionRegistry(db2);

            var item = ItemState.CreateWeapon(
                AllocateEId(),
                "Weapon",
                new WeaponConfiguration(
                    payload:  new PayloadCoreInstance("BallisticWithBase", RarityTier.Common),
                    delivery: new DeliveryCoreInstance("Auto", RarityTier.Common),
                    exotic:   null,
                    ammoInMagazine: 30));

            var runtime = WeaponSyncSystem.BuildWeaponForItem(item, registry2, _events);

            Assert.IsNotNull(runtime);
            Assert.IsNotNull(runtime.BasePrefab, "BasePrefab must propagate from PayloadDefinition.BasePrefab");
            Assert.AreSame(baseStub, runtime.BasePrefab);

            WeaponBuilderTestFactory.DestroyAll(ballisticWithBase, db2, baseStub);
        }

        // Test "Assembly_LeavesBasePrefabNull_WhenPayloadHasNoBase" removed (Tier 8.x*) —
        // production payloads MUST carry BasePrefab; null path was defensive-only.

        // ── Multiple builds coexist ───────────────────────────

        [Test]
        public void FullFlow_TwoBuilds_BothLandInBackpack_EachIsUniqueInstance()
        {
            // Two payload + two delivery modules — one set per Build.
            PutModulesInBackpack("BallisticRound", "SingleAction");
            PutModulesInBackpack("BallisticRound", "Auto");

            var presenter = new WeaponBuilderPresenter(_registry, _inventory, AllocateEId);

            // Build 1: pistol
            presenter.SelectPayload("BallisticRound");
            presenter.SelectDelivery("SingleAction");
            Assert.IsTrue(presenter.TryBuild(out _));

            // Build 2: rifle. Modules are consumed each Build — both weapons exist
            // у backpack alongside ammo grants from each Build.
            presenter.SelectDelivery("Auto");
            Assert.IsTrue(presenter.TryBuild(out _));

            ItemState pistolItem = null;
            ItemState rifleItem  = null;
            for (int i = 0; i < InventoryState.BackpackSize; i++)
            {
                var slot = _inventory.Backpack[i];
                if (slot == null || !slot.HasWeaponConfiguration) continue;
                var deliveryId = slot.WeaponConfiguration.Delivery.DefinitionId;
                if (deliveryId == "SingleAction") pistolItem = slot;
                else if (deliveryId == "Auto")    rifleItem  = slot;
            }

            Assert.IsNotNull(pistolItem, "Pistol build should land in backpack");
            Assert.IsNotNull(rifleItem,  "Rifle build should land in backpack");
            Assert.AreNotEqual(pistolItem.Id, rifleItem.Id, "Each build gets a fresh EId");
        }

        // ── Round-trip through ground item ────────────────────

        [Test]
        public void FullFlow_BuildThenDropToGroundThenPickUp_ConfigSurvives()
        {
            PutModulesInBackpack("BallisticRound", "Auto");

            var presenter = new WeaponBuilderPresenter(_registry, _inventory, AllocateEId);
            presenter.SelectPayload("BallisticRound");
            presenter.SelectDelivery("Auto");
            Assert.IsTrue(presenter.TryBuild(out _));

            var built = _inventory.Backpack[0];
            var originalConfig = built.WeaponConfiguration;

            // Drop — mimic InventorySystem.TryDrop branch
            _inventory.Backpack[0] = null;
            var ground = GroundItemState.CreateWeapon(built.Id, built.DefinitionId, Vector3.zero, built.WeaponConfiguration);
            Assert.IsTrue(ground.HasWeaponConfiguration);

            // Pick up — mimic InventorySystem.TryPickUp branch
            var pickedUp = ground.HasWeaponConfiguration
                ? ItemState.CreateWeapon(ground.Id, ground.DefinitionId, ground.WeaponConfiguration)
                : ItemState.Create(ground.Id, ground.DefinitionId);
            _inventory.Backpack[0] = pickedUp;

            Assert.AreEqual(originalConfig.Payload,  pickedUp.WeaponConfiguration.Payload);
            Assert.AreEqual(originalConfig.Delivery, pickedUp.WeaponConfiguration.Delivery);

            // Still assembles into a valid runtime state
            var runtime = WeaponSyncSystem.BuildWeaponForItem(pickedUp, _registry, _events);
            Assert.IsNotNull(runtime);
            Assert.AreEqual("BallisticRound", runtime.PayloadCore.DefinitionId);
            Assert.AreEqual("Module_Payload_Ballistic", runtime.PrefabId);
        }

        // ── Presenter preview drives stats that match runtime ─

        [Test]
        public void FullFlow_PreviewChanges_AsSelectionChanges()
        {
            // Final selection is BallisticRound + Auto, so place those for Build.
            PutModulesInBackpack("BallisticRound", "Auto");

            var presenter = new WeaponBuilderPresenter(_registry, _inventory, AllocateEId);

            presenter.SelectPayload("BallisticRound");
            presenter.SelectDelivery("SingleAction");
            Assert.AreEqual(12, presenter.PreviewStats.Value.MagazineSize);
            Assert.AreEqual("Ballistic Pistol", presenter.PreviewArchetype);

            // User changes mind — switch to Auto
            presenter.SelectDelivery("Auto");
            Assert.AreEqual(30, presenter.PreviewStats.Value.MagazineSize);
            Assert.AreEqual("Ballistic Rifle", presenter.PreviewArchetype);

            // Build after switch — the final item reflects the LAST selection, not the first.
            // Note: Auto module (slot 1) is consumed; weapon lands у freed slot.
            Assert.IsTrue(presenter.TryBuild(out _));
            int weaponSlot = -1;
            for (int i = 0; i < InventoryState.BackpackSize; i++)
                if (_inventory.Backpack[i] != null && _inventory.Backpack[i].HasWeaponConfiguration)
                {
                    weaponSlot = i;
                    break;
                }
            Assert.GreaterOrEqual(weaponSlot, 0);
            var runtime = WeaponSyncSystem.BuildWeaponForItem(_inventory.Backpack[weaponSlot], _registry, _events);
            Assert.AreEqual(30, runtime.Stats.MagazineSize);
        }

    }
}
