using Adapters;
using NUnit.Framework;
using State;
using Tests.EditMode.Fakes;
using UnityEngine;
using View.UI.WeaponBuilder;

namespace Tests.EditMode
{
    [TestFixture]
    public class WeaponBuilderPresenterTests
    {
        CoreDefinitionDatabase     _db;
        BallisticPayloadDefinition _ballistic;
        DeliveryCoreDefinition     _singleAction;
        DeliveryCoreDefinition     _auto;
        ICoreDefinitionRegistry    _registry;
        InventoryState             _inventory;
        int                        _nextEId;

        WeaponBuilderPresenter MakePresenter()
            => new WeaponBuilderPresenter(_registry, _inventory, AllocateEId);

        EId AllocateEId() => new EId(++_nextEId);

        [SetUp]
        public void SetUp()
        {
            _ballistic = WeaponBuilderTestFactory.MakeBallistic(
                "BallisticRound", displayName: "Ballistic", ammoType: "Ammo_Rifle",
                commonStats: new CommonPayloadStats { Damage = 15f, ProjectileSpeed = 25f, BasePenetration = 15f });
            _singleAction = WeaponBuilderTestFactory.MakeDelivery(
                "SingleAction", formFactor: "Pistol", pattern: FiringPattern.Single,
                commonStats: new DeliveryStats { FireInterval = 0.4f, MagazineSize = 12, ProjectilesPerShot = 1 });
            _auto = WeaponBuilderTestFactory.MakeDelivery(
                "Auto", formFactor: "Rifle", pattern: FiringPattern.Auto,
                commonStats: new DeliveryStats { FireInterval = 0.2f, MagazineSize = 30, ProjectilesPerShot = 1 });

            _db = WeaponBuilderTestFactory.MakeDatabase(
                payloads:   new PayloadCoreDefinition[]  { _ballistic },
                deliveries: new DeliveryCoreDefinition[] { _singleAction, _auto });
            _registry  = WeaponBuilderTestFactory.MakeRegistry(_db);
            _inventory = new InventoryState();
            _nextEId   = 0;
        }

        [TearDown]
        public void TearDown() =>
            WeaponBuilderTestFactory.DestroyAll(_ballistic, _singleAction, _auto, _db);

        // ── Initial / default state ───────────────────────────

        [Test]
        public void DefaultState_NothingSelected_CanBuildFalse()
        {
            var p = MakePresenter();

            Assert.IsFalse(p.State.HasPayload);
            Assert.IsFalse(p.State.HasDelivery);
            Assert.IsFalse(p.CanBuild);
            Assert.IsNull(p.PreviewStats);
            Assert.AreEqual(string.Empty, p.PreviewArchetype);
        }

        [Test]
        public void AllPayloads_ExposesRegistryList()
        {
            var p = MakePresenter();
            Assert.AreEqual(1, p.AllPayloads.Count);
            Assert.AreSame(_ballistic, p.AllPayloads[0]);

            Assert.AreEqual(2, p.AllDeliveries.Count);
            Assert.Contains(_singleAction, (System.Collections.ICollection)p.AllDeliveries);
            Assert.Contains(_auto,         (System.Collections.ICollection)p.AllDeliveries);
        }

        // ── Partial selection ─────────────────────────────────

        [Test]
        public void SelectPayloadOnly_PreviewStatsNull_CanBuildFalse()
        {
            var p = MakePresenter();
            p.SelectPayload("BallisticRound");

            Assert.IsTrue(p.State.HasPayload);
            Assert.IsFalse(p.State.HasDelivery);
            Assert.IsFalse(p.CanBuild);
            Assert.IsNull(p.PreviewStats);
            Assert.AreEqual("Ballistic", p.PreviewArchetype);
        }

        [Test]
        public void SelectDeliveryOnly_PreviewStatsNull_ArchetypeShowsFormFactor()
        {
            var p = MakePresenter();
            p.SelectDelivery("SingleAction");

            Assert.IsFalse(p.CanBuild);
            Assert.IsNull(p.PreviewStats);
            Assert.AreEqual("Pistol", p.PreviewArchetype);
        }

        // ── Full selection → preview + CanBuild ───────────────

        [Test]
        public void SelectBoth_PreviewStatsComposed_ArchetypeFormed_CanBuildTrue()
        {
            var p = MakePresenter();
            p.SelectPayload("BallisticRound");
            p.SelectDelivery("SingleAction");

            Assert.IsTrue(p.CanBuild);
            Assert.AreEqual("Ballistic Pistol", p.PreviewArchetype);

            Assert.IsNotNull(p.PreviewStats);
            var stats = p.PreviewStats.Value;
            Assert.AreEqual(15f,  stats.Damage);             // from Ballistic Common
            Assert.AreEqual(0.4f, stats.FireInterval);       // from SingleAction Common
            Assert.AreEqual(12,   stats.MagazineSize);       // from SingleAction Common
        }

        [Test]
        public void SwitchingDelivery_UpdatesPreview()
        {
            var p = MakePresenter();
            p.SelectPayload("BallisticRound");
            p.SelectDelivery("SingleAction");
            Assert.AreEqual(12, p.PreviewStats.Value.MagazineSize);

            p.SelectDelivery("Auto");
            Assert.AreEqual(30, p.PreviewStats.Value.MagazineSize);
            Assert.AreEqual("Ballistic Rifle", p.PreviewArchetype);
        }

        // ── StateChanged event ────────────────────────────────

        [Test]
        public void SelectionChanges_FireStateChangedEvent()
        {
            var p = MakePresenter();
            int callCount = 0;
            p.StateChanged += () => callCount++;

            p.SelectPayload("BallisticRound");
            p.SelectDelivery("SingleAction");
            p.ClearSelection();

            // At least one event per mutating call; exact count not asserted so internal
            // refactors (e.g. a debounced preview recompute) don't break the test.
            Assert.GreaterOrEqual(callCount, 3, "StateChanged should fire for each selection mutation");
            Assert.IsFalse(p.State.HasPayload,  "ClearSelection should leave no payload");
            Assert.IsFalse(p.State.HasDelivery, "ClearSelection should leave no delivery");
            Assert.IsFalse(p.CanBuild);
        }

        // ── TryBuild commits to inventory ─────────────────────

        [Test]
        public void TryBuild_Success_ItemLandsInBackpack_WithWeaponConfiguration()
        {
            var p = MakePresenter();
            p.SelectPayload("BallisticRound");
            p.SelectDelivery("SingleAction");

            bool built = p.TryBuild(out var reason);

            Assert.IsTrue(built);
            Assert.IsNull(reason);

            // First free slot should have the new weapon item
            var item = _inventory.Backpack[0];
            Assert.IsNotNull(item);
            Assert.IsTrue(item.HasWeaponConfiguration);
            Assert.AreEqual("BallisticRound", item.WeaponConfiguration.Payload.DefinitionId);
            Assert.AreEqual("SingleAction",   item.WeaponConfiguration.Delivery.DefinitionId);
            Assert.AreEqual(12, item.WeaponConfiguration.AmmoInMagazine,
                "Magazine should start full (= MagazineSize)");
            Assert.IsFalse(item.WeaponConfiguration.Exotic.HasValue);
        }

        [Test]
        public void TryBuild_NoPayload_FailsWithReason()
        {
            var p = MakePresenter();
            p.SelectDelivery("SingleAction");

            bool built = p.TryBuild(out var reason);

            Assert.IsFalse(built);
            StringAssert.Contains("payload", reason.ToLowerInvariant());
            Assert.IsNull(_inventory.Backpack[0]);
        }

        [Test]
        public void TryBuild_NoDelivery_FailsWithReason()
        {
            var p = MakePresenter();
            p.SelectPayload("BallisticRound");

            bool built = p.TryBuild(out var reason);

            Assert.IsFalse(built);
            StringAssert.Contains("delivery", reason.ToLowerInvariant());
        }

        [Test]
        public void TryBuild_BackpackFull_FailsAndCanBuildFalse()
        {
            FillBackpack(_inventory);

            var p = MakePresenter();
            p.SelectPayload("BallisticRound");
            p.SelectDelivery("SingleAction");

            Assert.IsFalse(p.CanBuild, "Backpack full → CanBuild is false");
            Assert.IsNotNull(p.PreviewStats, "Preview should still compose");

            bool built = p.TryBuild(out var reason);
            Assert.IsFalse(built);
            StringAssert.Contains("full", reason.ToLowerInvariant());
        }

        // ── Helpers ───────────────────────────────────────────

        static void FillBackpack(InventoryState inv)
        {
            for (int i = 0; i < InventoryState.BackpackSize; i++)
                inv.Backpack[i] = ItemState.Create(new EId(1000 + i), "Medkit");
        }
    }
}
