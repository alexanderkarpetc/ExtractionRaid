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
        LaserPayloadDefinition     _laser;
        DeliveryCoreDefinition     _singleAction;
        DeliveryCoreDefinition     _auto;
        ICoreDefinitionRegistry    _registry;
        InventoryState             _inventory;
        int                        _nextEId;
        const float                LaserChargeTime = 1.25f;

        WeaponBuilderPresenter MakePresenter()
            => new WeaponBuilderPresenter(_registry, _inventory, AllocateEId);

        EId AllocateEId() => new EId(++_nextEId);

        [SetUp]
        public void SetUp()
        {
            _ballistic = WeaponBuilderTestFactory.MakeBallistic(
                "BallisticRound", displayName: "Ballistic", ammoType: "Ammo_Rifle",
                commonStats: new CommonPayloadStats { Damage = 15f, ProjectileSpeed = 25f, BasePenetration = 15f });
            _laser = WeaponBuilderTestFactory.MakeLaser(
                "LaserCharge", displayName: "Laser", ammoType: "Ammo_EnergyCell",
                commonStats: new CommonPayloadStats { Damage = 25f, ProjectileSpeed = 60f, BasePenetration = 25f },
                chargeTime: LaserChargeTime);
            _singleAction = WeaponBuilderTestFactory.MakeDelivery(
                "SingleAction", formFactor: "Pistol", pattern: FiringPattern.Single,
                commonStats: new DeliveryStats { FireInterval = 0.4f, MagazineSize = 12, ProjectilesPerShot = 1 });
            _auto = WeaponBuilderTestFactory.MakeDelivery(
                "Auto", formFactor: "Rifle", pattern: FiringPattern.Auto,
                commonStats: new DeliveryStats { FireInterval = 0.2f, MagazineSize = 30, ProjectilesPerShot = 1 });

            _db = WeaponBuilderTestFactory.MakeDatabase(
                payloads:   new PayloadCoreDefinition[]  { _ballistic, _laser },
                deliveries: new DeliveryCoreDefinition[] { _singleAction, _auto });
            _registry  = WeaponBuilderTestFactory.MakeRegistry(_db);
            _inventory = new InventoryState();
            _nextEId   = 0;
        }

        [TearDown]
        public void TearDown() =>
            WeaponBuilderTestFactory.DestroyAll(_ballistic, _laser, _singleAction, _auto, _db);

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
            Assert.AreEqual(2, p.AllPayloads.Count);
            Assert.Contains(_ballistic, (System.Collections.ICollection)p.AllPayloads);
            Assert.Contains(_laser,     (System.Collections.ICollection)p.AllPayloads);

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
            PlaceModulesInBackpack("BallisticRound", "SingleAction");

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
            PlaceModulesInBackpack("BallisticRound", "SingleAction");

            var p = MakePresenter();
            p.SelectPayload("BallisticRound");
            p.SelectDelivery("SingleAction");

            bool built = p.TryBuild(out var reason);

            Assert.IsTrue(built);
            Assert.IsNull(reason);

            // After consume + weapon placement, first free slot (slot 0, just freed
            // by payload module consumption) holds the new weapon item.
            var item = _inventory.Backpack[0];
            Assert.IsNotNull(item);
            Assert.IsTrue(item.HasWeaponConfiguration);
            Assert.AreEqual("BallisticRound", item.WeaponConfiguration.Payload.DefinitionId);
            Assert.AreEqual("SingleAction",   item.WeaponConfiguration.Delivery.DefinitionId);
            Assert.AreEqual(12, item.WeaponConfiguration.AmmoInMagazine,
                "Magazine should start full (= MagazineSize)");
            Assert.IsFalse(item.WeaponConfiguration.Exotic.HasValue);
        }

        // ── Auto-grant ammo on Build (Pass 2 / T3) ────────────

        [Test]
        public void TryBuild_BallisticPistol_GrantsRifleAmmoReserve()
        {
            PlaceModulesInBackpack("BallisticRound", "SingleAction");

            var p = MakePresenter();
            p.SelectPayload("BallisticRound");   // ammoType = "Ammo_Rifle"
            p.SelectDelivery("SingleAction");    // MagazineSize = 12

            Assert.IsTrue(p.TryBuild(out _));

            int totalAmmo = SumAmmoInBackpack("Ammo_Rifle");
            Assert.AreEqual(24, totalAmmo, "Should grant 2× MagazineSize of matching ammo");
        }

        [Test]
        public void TryBuild_LaserPistol_GrantsEnergyCellAmmoReserve()
        {
            PlaceModulesInBackpack("LaserCharge", "SingleAction");

            var p = MakePresenter();
            p.SelectPayload("LaserCharge");      // ammoType = "Ammo_EnergyCell"
            p.SelectDelivery("SingleAction");    // MagazineSize = 12

            Assert.IsTrue(p.TryBuild(out _));

            int totalAmmo = SumAmmoInBackpack("Ammo_EnergyCell");
            Assert.AreEqual(24, totalAmmo);
            Assert.AreEqual(0, SumAmmoInBackpack("Ammo_Rifle"),
                "Laser build must not grant rifle ammo");
        }

        [Test]
        public void TryBuild_StacksAmmoIntoExistingPartialStack()
        {
            PlaceModulesInBackpack("BallisticRound", "SingleAction");
            // Pre-place a partial Ammo_Rifle stack so the grant should top it up.
            _inventory.Backpack[5] = ItemState.Create(new EId(99), "Ammo_Rifle", 10);

            var p = MakePresenter();
            p.SelectPayload("BallisticRound");
            p.SelectDelivery("SingleAction");

            Assert.IsTrue(p.TryBuild(out _));

            // 10 pre-existing + 24 granted = 34. Ammo_Rifle MaxStackSize is 60, so
            // it all fits in the original stack — no overflow.
            Assert.AreEqual(34, _inventory.Backpack[5].StackCount);
            Assert.AreEqual(34, SumAmmoInBackpack("Ammo_Rifle"));
        }

        [Test]
        public void TryBuild_AmmoOverflowSpillsIntoFreeSlots()
        {
            PlaceModulesInBackpack("BallisticRound", "SingleAction");
            // Pre-place a near-full stack so overflow forces a second slot.
            _inventory.Backpack[5] = ItemState.Create(new EId(99), "Ammo_Rifle", 55);

            var p = MakePresenter();
            p.SelectPayload("BallisticRound");
            p.SelectDelivery("SingleAction");

            Assert.IsTrue(p.TryBuild(out _));

            // 55 + space = 5 → top-up to 60. Remaining 19 spill into a new slot.
            Assert.AreEqual(60, _inventory.Backpack[5].StackCount);
            Assert.AreEqual(55 + 24, SumAmmoInBackpack("Ammo_Rifle"));
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

        // ── Module consumption (Tier 6 G6) ────────────────────

        [Test]
        public void TryBuild_ConsumesPayloadAndDeliveryModules()
        {
            PlaceModulesInBackpack("BallisticRound", "SingleAction");
            Assert.AreEqual(1, CountInBackpack("BallisticRound"));
            Assert.AreEqual(1, CountInBackpack("SingleAction"));

            var p = MakePresenter();
            p.SelectPayload("BallisticRound");
            p.SelectDelivery("SingleAction");

            Assert.IsTrue(p.TryBuild(out _));

            Assert.AreEqual(0, CountInBackpack("BallisticRound"),
                "Payload module should be consumed on Build");
            Assert.AreEqual(0, CountInBackpack("SingleAction"),
                "Delivery module should be consumed on Build");
        }

        [Test]
        public void TryBuild_NoPayloadModuleInBackpack_Fails()
        {
            // Only delivery present.
            _inventory.Backpack[0] = ItemState.Create(new EId(500), "SingleAction");

            var p = MakePresenter();
            p.SelectPayload("BallisticRound");
            p.SelectDelivery("SingleAction");

            Assert.IsFalse(p.CanBuild);
            Assert.IsTrue(p.TryBuild(out var reason) == false && reason != null);
            StringAssert.Contains("payload", reason.ToLowerInvariant());
        }

        [Test]
        public void TryBuild_NoDeliveryModuleInBackpack_Fails()
        {
            // Only payload present.
            _inventory.Backpack[0] = ItemState.Create(new EId(500), "BallisticRound");

            var p = MakePresenter();
            p.SelectPayload("BallisticRound");
            p.SelectDelivery("SingleAction");

            Assert.IsFalse(p.CanBuild);
            Assert.IsTrue(p.TryBuild(out var reason) == false && reason != null);
            StringAssert.Contains("delivery", reason.ToLowerInvariant());
        }

        [Test]
        public void TryBuild_BackpackFullWithModules_StillSucceeds()
        {
            // Modules + 18 medkits = 20 items, backpack at capacity. Module
            // consumption frees 2 slots, weapon + ammo land у freed slots — Build
            // succeeds even though pre-Build the inventory was "full".
            FillBackpack(_inventory);
            // Replace 2 medkits with modules
            _inventory.Backpack[0] = ItemState.Create(new EId(500), "BallisticRound");
            _inventory.Backpack[1] = ItemState.Create(new EId(501), "SingleAction");

            var p = MakePresenter();
            p.SelectPayload("BallisticRound");
            p.SelectDelivery("SingleAction");

            Assert.IsTrue(p.CanBuild);
            Assert.IsTrue(p.TryBuild(out _));

            // Modules consumed; weapon now occupies one of the freed slots.
            Assert.AreEqual(0, CountInBackpack("BallisticRound"));
            Assert.AreEqual(0, CountInBackpack("SingleAction"));
            int weaponCount = 0;
            for (int i = 0; i < InventoryState.BackpackSize; i++)
                if (_inventory.Backpack[i] != null && _inventory.Backpack[i].HasWeaponConfiguration) weaponCount++;
            Assert.AreEqual(1, weaponCount, "Built weapon should be у backpack");
        }

        // ── Charge-up preview (Pass 1 / T1) ───────────────────

        [Test]
        public void PreviewRequiresCharge_NoPayload_False()
        {
            var p = MakePresenter();
            Assert.IsFalse(p.PreviewRequiresCharge);
            Assert.AreEqual(0f, p.PreviewChargeTime);
        }

        [Test]
        public void PreviewRequiresCharge_BallisticPayload_False()
        {
            var p = MakePresenter();
            p.SelectPayload("BallisticRound");
            Assert.IsFalse(p.PreviewRequiresCharge);
            Assert.AreEqual(0f, p.PreviewChargeTime);
        }

        [Test]
        public void PreviewRequiresCharge_LaserPayload_TrueWithChargeTime()
        {
            var p = MakePresenter();
            p.SelectPayload("LaserCharge");
            Assert.IsTrue(p.PreviewRequiresCharge);
            Assert.AreEqual(LaserChargeTime, p.PreviewChargeTime, 1e-4f);
        }

        [Test]
        public void PreviewRequiresCharge_SwitchingPayload_UpdatesAccordingly()
        {
            var p = MakePresenter();
            p.SelectPayload("LaserCharge");
            Assert.IsTrue(p.PreviewRequiresCharge);

            p.SelectPayload("BallisticRound");
            Assert.IsFalse(p.PreviewRequiresCharge);
            Assert.AreEqual(0f, p.PreviewChargeTime);
        }

        // ── Archetype flavor (UX-A.04) ────────────────────────

        [Test]
        public void PreviewArchetypeFlavor_NoSelection_Empty()
        {
            var p = MakePresenter();
            Assert.AreEqual(string.Empty, p.PreviewArchetypeFlavor);
        }

        [Test]
        public void PreviewArchetypeFlavor_OnlyPayload_Empty()
        {
            var p = MakePresenter();
            p.SelectPayload("BallisticRound");
            Assert.AreEqual(string.Empty, p.PreviewArchetypeFlavor);
        }

        [Test]
        public void PreviewArchetypeFlavor_BallisticPistol_Reliable()
        {
            var p = MakePresenter();
            p.SelectPayload("BallisticRound");
            p.SelectDelivery("SingleAction");
            Assert.AreEqual("Reliable single-shot sidearm", p.PreviewArchetypeFlavor);
        }

        [Test]
        public void PreviewArchetypeFlavor_LaserRifle_MentionsCharge()
        {
            var p = MakePresenter();
            p.SelectPayload("LaserCharge");
            p.SelectDelivery("Auto");
            StringAssert.Contains("charged", p.PreviewArchetypeFlavor.ToLowerInvariant());
        }

        // ── Disabled reason (Pass 1 / T4) ─────────────────────

        [Test]
        public void DisabledReason_NothingSelected_SaysSelectPayload()
        {
            var p = MakePresenter();
            StringAssert.Contains("payload", p.DisabledReason.ToLowerInvariant());
        }

        [Test]
        public void DisabledReason_OnlyDeliverySelected_SaysSelectPayload()
        {
            var p = MakePresenter();
            p.SelectDelivery("SingleAction");
            StringAssert.Contains("payload", p.DisabledReason.ToLowerInvariant());
        }

        [Test]
        public void DisabledReason_OnlyPayloadSelected_SaysSelectDelivery()
        {
            var p = MakePresenter();
            p.SelectPayload("BallisticRound");
            StringAssert.Contains("delivery", p.DisabledReason.ToLowerInvariant());
        }

        [Test]
        public void DisabledReason_NoPayloadModule_SaysNoPayload()
        {
            // Delivery present, payload missing.
            _inventory.Backpack[0] = ItemState.Create(new EId(500), "SingleAction");
            var p = MakePresenter();
            p.SelectPayload("BallisticRound");
            p.SelectDelivery("SingleAction");
            StringAssert.Contains("payload", p.DisabledReason.ToLowerInvariant());
        }

        [Test]
        public void DisabledReason_NoDeliveryModule_SaysNoDelivery()
        {
            // Payload present, delivery missing.
            _inventory.Backpack[0] = ItemState.Create(new EId(500), "BallisticRound");
            var p = MakePresenter();
            p.SelectPayload("BallisticRound");
            p.SelectDelivery("SingleAction");
            StringAssert.Contains("delivery", p.DisabledReason.ToLowerInvariant());
        }

        [Test]
        public void DisabledReason_AllOk_Empty()
        {
            PlaceModulesInBackpack("BallisticRound", "SingleAction");
            var p = MakePresenter();
            p.SelectPayload("BallisticRound");
            p.SelectDelivery("SingleAction");
            Assert.IsTrue(p.CanBuild);
            Assert.AreEqual(string.Empty, p.DisabledReason);
        }

        // ── Helpers ───────────────────────────────────────────

        static void FillBackpack(InventoryState inv)
        {
            for (int i = 0; i < InventoryState.BackpackSize; i++)
                inv.Backpack[i] = ItemState.Create(new EId(1000 + i), "Medkit");
        }

        /// <summary>Places payload + delivery module items into the first two free
        /// backpack slots so TryBuild's module-consumption check passes (Tier 6 G6).
        /// Tests that assert <c>CanBuild==true</c> or expect <c>TryBuild</c> to
        /// succeed must call this first.</summary>
        void PlaceModulesInBackpack(string payloadId, string deliveryId)
        {
            int slot = 0;
            while (slot < InventoryState.BackpackSize && _inventory.Backpack[slot] != null) slot++;
            if (slot < InventoryState.BackpackSize)
                _inventory.Backpack[slot++] = ItemState.Create(new EId(500 + slot), payloadId);
            while (slot < InventoryState.BackpackSize && _inventory.Backpack[slot] != null) slot++;
            if (slot < InventoryState.BackpackSize)
                _inventory.Backpack[slot] = ItemState.Create(new EId(500 + slot), deliveryId);
        }

        int CountInBackpack(string definitionId)
        {
            int n = 0;
            for (int i = 0; i < InventoryState.BackpackSize; i++)
                if (_inventory.Backpack[i]?.DefinitionId == definitionId) n++;
            return n;
        }

        int SumAmmoInBackpack(string ammoDefinitionId)
        {
            int total = 0;
            for (int i = 0; i < InventoryState.BackpackSize; i++)
            {
                var slot = _inventory.Backpack[i];
                if (slot != null && slot.DefinitionId == ammoDefinitionId)
                    total += slot.StackCount;
            }
            return total;
        }
    }
}
