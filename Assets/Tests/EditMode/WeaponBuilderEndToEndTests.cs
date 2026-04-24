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
        ICoreDefinitionRegistry    _registry;
        InventoryState             _inventory;
        FakeRaidEvents             _events;
        int                        _nextEId;

        EId AllocateEId() => new EId(++_nextEId);

        [SetUp]
        public void SetUp()
        {
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
                });
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
                });
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
                });

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
            WeaponBuilderTestFactory.DestroyAll(_ballistic, _singleAction, _auto, _db);

        // ── Core vertical slice: build → equip → runtime state ─

        [Test]
        public void FullFlow_BuildBallisticPistol_ProducesRuntimeWeaponWithMatchingStats()
        {
            var presenter = new WeaponBuilderPresenter(_registry, _inventory, AllocateEId);

            // === 1. UX — select both slots
            presenter.SelectPayload("BallisticRound");
            presenter.SelectDelivery("SingleAction");

            Assert.IsTrue(presenter.CanBuild);
            Assert.AreEqual("Ballistic Pistol", presenter.PreviewArchetype);
            var previewStats = presenter.PreviewStats.Value;

            // === 2. UX — Build → item in backpack
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
            Assert.AreEqual("Weapon_Pistol",  runtime.PrefabId, "Prefab resolved from Delivery FormFactor");
        }

        [Test]
        public void FullFlow_BuildBallisticRifle_ProducesRifleVariant()
        {
            var presenter = new WeaponBuilderPresenter(_registry, _inventory, AllocateEId);

            presenter.SelectPayload("BallisticRound");
            presenter.SelectDelivery("Auto");
            Assert.IsTrue(presenter.TryBuild(out _));

            var runtime = WeaponSyncSystem.BuildWeaponForItem(_inventory.Backpack[0], _registry, _events);

            Assert.AreEqual("Ballistic Rifle", WeaponArchetypeLabel.Compose(_ballistic, _auto));
            Assert.AreEqual(30,  runtime.Stats.MagazineSize);
            Assert.AreEqual(0.2f, runtime.Stats.FireInterval);
            Assert.AreEqual("Weapon_Rifle", runtime.PrefabId);
        }

        // ── Multiple builds coexist ───────────────────────────

        [Test]
        public void FullFlow_TwoBuilds_BothLandInBackpack_EachIsUniqueInstance()
        {
            var presenter = new WeaponBuilderPresenter(_registry, _inventory, AllocateEId);

            // Build 1: pistol
            presenter.SelectPayload("BallisticRound");
            presenter.SelectDelivery("SingleAction");
            Assert.IsTrue(presenter.TryBuild(out _));

            // Build 2: rifle — should go to slot 1
            presenter.SelectDelivery("Auto");
            Assert.IsTrue(presenter.TryBuild(out _));

            Assert.IsNotNull(_inventory.Backpack[0]);
            Assert.IsNotNull(_inventory.Backpack[1]);
            Assert.AreNotEqual(_inventory.Backpack[0].Id, _inventory.Backpack[1].Id, "Each build gets a fresh EId");

            Assert.AreEqual("SingleAction", _inventory.Backpack[0].WeaponConfiguration.Delivery.DefinitionId);
            Assert.AreEqual("Auto",         _inventory.Backpack[1].WeaponConfiguration.Delivery.DefinitionId);
        }

        // ── Round-trip through ground item ────────────────────

        [Test]
        public void FullFlow_BuildThenDropToGroundThenPickUp_ConfigSurvives()
        {
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
            Assert.AreEqual("Weapon_Rifle",   runtime.PrefabId);
        }

        // ── Presenter preview drives stats that match runtime ─

        [Test]
        public void FullFlow_PreviewChanges_AsSelectionChanges()
        {
            var presenter = new WeaponBuilderPresenter(_registry, _inventory, AllocateEId);

            presenter.SelectPayload("BallisticRound");
            presenter.SelectDelivery("SingleAction");
            Assert.AreEqual(12, presenter.PreviewStats.Value.MagazineSize);
            Assert.AreEqual("Ballistic Pistol", presenter.PreviewArchetype);

            // User changes mind — switch to Auto
            presenter.SelectDelivery("Auto");
            Assert.AreEqual(30, presenter.PreviewStats.Value.MagazineSize);
            Assert.AreEqual("Ballistic Rifle", presenter.PreviewArchetype);

            // Build after switch — the final item reflects the LAST selection, not the first
            Assert.IsTrue(presenter.TryBuild(out _));
            var runtime = WeaponSyncSystem.BuildWeaponForItem(_inventory.Backpack[0], _registry, _events);
            Assert.AreEqual(30, runtime.Stats.MagazineSize);
        }

    }
}
