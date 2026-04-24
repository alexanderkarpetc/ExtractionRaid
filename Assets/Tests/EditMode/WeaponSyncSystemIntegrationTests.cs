using Adapters;
using NUnit.Framework;
using State;
using Systems;
using Tests.EditMode.Fakes;

namespace Tests.EditMode
{
    /// <summary>
    /// End-to-end tests for the weapon-builder pipeline:
    /// ItemState (with WeaponConfiguration) → WeaponAssemblySystem → WeaponEntityState.
    ///
    /// Covers:
    ///   - Rifle / Pistol starter-grade items produce runtime WeaponEntityState with
    ///     the expected composition, Stats pulled from SO assets, and AmmoType from Payload.
    ///   - Ghost-weapon path per D7: invalid / missing config → null + WeaponAssemblyFailed event.
    ///   - Ground ↔ inventory round-trip preserves composition.
    /// </summary>
    [TestFixture]
    public class WeaponSyncSystemIntegrationTests
    {
        CoreDefinitionDatabase     _db;
        BallisticPayloadDefinition _ballistic;
        DeliveryCoreDefinition     _singleAction;
        DeliveryCoreDefinition     _auto;
        ICoreDefinitionRegistry    _registry;
        FakeRaidEvents             _events;

        [SetUp]
        public void SetUp()
        {
            _ballistic = WeaponBuilderTestFactory.MakeBallistic(
                "BallisticRound", ammoType: "Ammo_Rifle",
                commonStats: new CommonPayloadStats
                {
                    Damage                   = 15f,
                    ProjectileSpeed          = 25f,
                    ProjectileLifetime       = 2.5f,
                    HeadshotDamageMultiplier = 2.0f,
                    BasePenetration          = 15f,
                    BaseArmorDamage          = 5f,
                });

            _singleAction = WeaponBuilderTestFactory.MakeDelivery("SingleAction",
                pattern: FiringPattern.Single,
                commonStats: new DeliveryStats
                {
                    FireInterval = 0.4f,
                    ProjectilesPerShot = 1,
                    MagazineSize = 12,
                    ReloadTime = 1.5f,
                    ConeHalfAngle = 35f,
                });

            _auto = WeaponBuilderTestFactory.MakeDelivery("Auto",
                pattern: FiringPattern.Auto,
                commonStats: new DeliveryStats
                {
                    FireInterval = 0.2f,
                    ProjectilesPerShot = 1,
                    MagazineSize = 30,
                    ReloadTime = 2.0f,
                    ConeHalfAngle = 45f,
                });

            _db = WeaponBuilderTestFactory.MakeDatabase(
                payloads:   new PayloadCoreDefinition[]  { _ballistic },
                deliveries: new DeliveryCoreDefinition[] { _singleAction, _auto });
            _registry = WeaponBuilderTestFactory.MakeRegistry(_db);
            _events = new FakeRaidEvents();
        }

        [TearDown]
        public void TearDown() =>
            WeaponBuilderTestFactory.DestroyAll(_ballistic, _singleAction, _auto, _db);

        // ── Rifle parity ──────────────────────────────────────

        [Test]
        public void BuildWeaponForItem_RifleStarterItem_AssemblesExpectedState()
        {
            var item = WeaponItemFactory.SpawnItem(new EId(1), "Rifle");

            var weapon = WeaponSyncSystem.BuildWeaponForItem(item, _registry, _events);

            Assert.IsNotNull(weapon);
            // Composition identity
            Assert.AreEqual("BallisticRound", weapon.PayloadCore.DefinitionId);
            Assert.AreEqual("Auto",           weapon.DeliveryCore.DefinitionId);
            Assert.AreEqual(RarityTier.Common, weapon.PayloadCore.Rarity);
            Assert.AreEqual(RarityTier.Common, weapon.DeliveryCore.Rarity);
            Assert.IsFalse(weapon.HasExotic);
            // Resolved definition refs
            Assert.AreSame(_ballistic, weapon.PayloadDefinition);
            Assert.AreSame(_auto,      weapon.DeliveryDefinition);
            // Stats from assets (not hardcoded)
            Assert.AreEqual(15f,  weapon.Stats.Damage,       "Damage comes from Ballistic Common");
            Assert.AreEqual(0.2f, weapon.Stats.FireInterval, "FireInterval comes from Auto Common");
            Assert.AreEqual(30,   weapon.Stats.MagazineSize, "Magazine comes from Auto Common");
            // AmmoType from Payload
            Assert.AreEqual("Ammo_Rifle", weapon.AmmoType);
            // Initial runtime
            Assert.AreEqual(30, weapon.AmmoInMagazine);
            Assert.AreEqual(WeaponPhase.Ready, weapon.Phase);
            // No failed events
            Assert.IsEmpty(_events.WeaponAssemblyFailures);
        }

        // ── Pistol parity ─────────────────────────────────────

        [Test]
        public void BuildWeaponForItem_PistolStarterItem_AssemblesExpectedState()
        {
            var item = WeaponItemFactory.SpawnItem(new EId(2), "Pistol");

            var weapon = WeaponSyncSystem.BuildWeaponForItem(item, _registry, _events);

            Assert.IsNotNull(weapon);
            Assert.AreEqual("SingleAction", weapon.DeliveryCore.DefinitionId);
            Assert.AreSame(_singleAction, weapon.DeliveryDefinition);
            Assert.AreEqual(0.4f, weapon.Stats.FireInterval);
            Assert.AreEqual(12,   weapon.Stats.MagazineSize);
            Assert.AreEqual(35f,  weapon.Stats.ConeHalfAngle);
            Assert.AreEqual("Ammo_Rifle", weapon.AmmoType,
                "Ballistic payload → Ammo_Rifle (shared between Rifle and Pistol)");
            Assert.AreEqual(12, weapon.AmmoInMagazine);
            Assert.IsEmpty(_events.WeaponAssemblyFailures);
        }

        // ── Ghost-weapon paths (D7) ───────────────────────────

        [Test]
        public void BuildWeaponForItem_ItemWithoutConfiguration_ReturnsNullAndEmitsEvent()
        {
            // An item that's a weapon by DefinitionId but was created without config
            // (e.g. by legacy ItemState.Create). This shouldn't normally happen in
            // production, but we verify the safety net.
            var item = ItemState.Create(new EId(3), "Rifle");

            var weapon = WeaponSyncSystem.BuildWeaponForItem(item, _registry, _events);

            Assert.IsNull(weapon);
            Assert.AreEqual(1, _events.WeaponAssemblyFailures.Count);
            Assert.AreEqual("Rifle", _events.WeaponAssemblyFailures[0].weaponId);
            StringAssert.Contains("no WeaponConfiguration", _events.WeaponAssemblyFailures[0].reason);
        }

        [Test]
        public void BuildWeaponForItem_NullRegistry_ReturnsNullAndEmitsEvent()
        {
            var item = WeaponItemFactory.SpawnItem(new EId(4), "Rifle");

            var weapon = WeaponSyncSystem.BuildWeaponForItem(item, registry: null, _events);

            Assert.IsNull(weapon);
            Assert.AreEqual(1, _events.WeaponAssemblyFailures.Count);
            StringAssert.Contains("registry", _events.WeaponAssemblyFailures[0].reason.ToLowerInvariant());
        }

        [Test]
        public void BuildWeaponForItem_ConfigWithUnknownPayload_ReturnsNullAndEmitsEvent()
        {
            // Craft a weapon item that references a payload missing from the registry —
            // strict failure per D7, no auto-repair.
            var config = new WeaponConfiguration(
                new PayloadCoreInstance("NonExistentPayload", RarityTier.Common),
                new DeliveryCoreInstance("Auto",              RarityTier.Common),
                exotic: null,
                ammoInMagazine: 30);
            var item = ItemState.CreateWeapon(new EId(5), "Rifle", config);

            var weapon = WeaponSyncSystem.BuildWeaponForItem(item, _registry, _events);

            Assert.IsNull(weapon);
            Assert.AreEqual(1, _events.WeaponAssemblyFailures.Count);
            StringAssert.Contains("NonExistentPayload", _events.WeaponAssemblyFailures[0].reason);
        }

        [Test]
        public void BuildWeaponForItem_ConfigWithUnknownDelivery_ReturnsNullAndEmitsEvent()
        {
            var config = new WeaponConfiguration(
                new PayloadCoreInstance("BallisticRound", RarityTier.Common),
                new DeliveryCoreInstance("Rotary",        RarityTier.Common), // not in DB
                exotic: null,
                ammoInMagazine: 30);
            var item = ItemState.CreateWeapon(new EId(6), "Rifle", config);

            var weapon = WeaponSyncSystem.BuildWeaponForItem(item, _registry, _events);

            Assert.IsNull(weapon);
            Assert.AreEqual(1, _events.WeaponAssemblyFailures.Count);
            StringAssert.Contains("Rotary", _events.WeaponAssemblyFailures[0].reason);
        }

        // (Round-trip scenario covered by WeaponBuilderEndToEndTests.FullFlow_BuildThenDropToGroundThenPickUp_ConfigSurvives)

    }
}
