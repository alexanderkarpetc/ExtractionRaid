using System.Collections.Generic;
using System.Reflection;
using Adapters;
using NUnit.Framework;
using State;
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
            _ballistic = MakeBallistic("BallisticRound", "Ballistic", "Ammo_Rifle", new CommonPayloadStats
            {
                Damage = 15f, ProjectileSpeed = 25f, BasePenetration = 15f,
            });
            _singleAction = MakeDelivery("SingleAction", "Pistol", FiringPattern.Single, new DeliveryStats
            {
                FireInterval = 0.4f, MagazineSize = 12, ProjectilesPerShot = 1,
            });
            _auto = MakeDelivery("Auto", "Rifle", FiringPattern.Auto, new DeliveryStats
            {
                FireInterval = 0.2f, MagazineSize = 30, ProjectilesPerShot = 1,
            });

            _db = ScriptableObject.CreateInstance<CoreDefinitionDatabase>();
            _db.SetEntries(
                new List<PayloadCoreDefinition>  { _ballistic },
                new List<DeliveryCoreDefinition> { _singleAction, _auto },
                new List<ExoticModDefinition>());

            _registry = new DatabaseCoreDefinitionRegistry(_db);
            _inventory = new InventoryState();
            _nextEId = 0;
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_ballistic);
            Object.DestroyImmediate(_singleAction);
            Object.DestroyImmediate(_auto);
            Object.DestroyImmediate(_db);
        }

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

            Assert.AreEqual(3, callCount);
        }

        // ── CanBuild gating by inventory ──────────────────────

        [Test]
        public void FullBackpack_CanBuildFalseEvenWithValidSelection()
        {
            FillBackpack(_inventory);

            var p = MakePresenter();
            p.SelectPayload("BallisticRound");
            p.SelectDelivery("SingleAction");

            Assert.IsFalse(p.CanBuild, "Backpack full → can't build");
            Assert.IsNotNull(p.PreviewStats, "Preview should still compose");
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
        public void TryBuild_BackpackFull_FailsWithReason()
        {
            FillBackpack(_inventory);

            var p = MakePresenter();
            p.SelectPayload("BallisticRound");
            p.SelectDelivery("SingleAction");

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

        static BallisticPayloadDefinition MakeBallistic(string id, string displayName, string ammoType,
            CommonPayloadStats commonStats)
        {
            var def = ScriptableObject.CreateInstance<BallisticPayloadDefinition>();
            SetPrivateField(def, "_id",          id);
            SetPrivateField(def, "_displayName", displayName);
            SetPrivateField(def, "_ammoType",    ammoType);
            var array = new CommonPayloadStats[5];
            array[(int)RarityTier.Common] = commonStats;
            SetPrivateField(def, "_statsByTier", array);
            return def;
        }

        static DeliveryCoreDefinition MakeDelivery(string id, string formFactor, FiringPattern pattern,
            DeliveryStats commonStats)
        {
            var def = ScriptableObject.CreateInstance<DeliveryCoreDefinition>();
            SetPrivateField(def, "_id",         id);
            SetPrivateField(def, "_formFactor", formFactor);
            SetPrivateField(def, "_pattern",    pattern);
            var array = new DeliveryStats[5];
            array[(int)RarityTier.Common] = commonStats;
            SetPrivateField(def, "_statsByTier", array);
            return def;
        }

        static void SetPrivateField(object target, string fieldName, object value)
        {
            var type = target.GetType();
            while (type != null)
            {
                var field = type.GetField(
                    fieldName,
                    BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                if (field != null) { field.SetValue(target, value); return; }
                type = type.BaseType;
            }
            Assert.Fail($"Field '{fieldName}' not found on {target.GetType()}.");
        }
    }
}
