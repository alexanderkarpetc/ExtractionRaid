using System.Linq;
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
    /// End-to-end charge-up flow tests (Tier 2). Exercise the full pipeline:
    /// Builder creates a Laser weapon → WeaponSyncSystem assembles it → ShootingSystem
    /// drives the charge gate → StateMachineSystem ticks Charging → shot fires after
    /// ChargeTime elapses. Verifies Variant B behaviour: every shot from a Laser
    /// payload goes through Charging regardless of Delivery pattern.
    ///
    /// See docs/ai/weapon-builder/plan/tasks.md T-2.13.
    /// </summary>
    [TestFixture]
    public class WeaponChargeFlowEndToEndTests
    {
        CoreDefinitionDatabase     _db;
        BallisticPayloadDefinition _ballistic;
        LaserPayloadDefinition     _laser;
        DeliveryCoreDefinition     _singleAction;
        DeliveryCoreDefinition     _auto;
        DeliveryCoreDefinition     _scatter;
        ICoreDefinitionRegistry    _registry;
        InventoryState             _inventory;
        FakeRaidEvents             _events;
        int                        _nextEId;

        const float LaserChargeTime = 1.0f;

        EId AllocateEId() => new EId(++_nextEId);

        [SetUp]
        public void SetUp()
        {
            var payloadStats = new CommonPayloadStats
            {
                Damage = 15f, ProjectileSpeed = 25f, ProjectileLifetime = 2.5f,
                HeadshotDamageMultiplier = 2f, BasePenetration = 15f, BaseArmorDamage = 5f,
            };
            _ballistic = WeaponBuilderTestFactory.MakeBallistic(
                "BallisticRound", displayName: "Ballistic", ammoType: "Ammo_Rifle",
                commonStats: payloadStats);
            _laser = WeaponBuilderTestFactory.MakeLaser(
                "LaserCharge", displayName: "Laser", ammoType: "Ammo_EnergyCell",
                commonStats: new CommonPayloadStats
                {
                    Damage = 25f, ProjectileSpeed = 50f, ProjectileLifetime = 3f,
                    HeadshotDamageMultiplier = 2f, BasePenetration = 25f, BaseArmorDamage = 8f,
                },
                chargeTime: LaserChargeTime);
            _singleAction = MakeDelivery("SingleAction", "Pistol", FiringPattern.Single, magazine: 12);
            _auto         = MakeDelivery("Auto",         "Rifle",  FiringPattern.Auto,   magazine: 30);
            _scatter      = MakeDelivery("Scatter",      "Shotgun", FiringPattern.Scatter, magazine: 5,
                projectilesPerShot: 7);

            _db = WeaponBuilderTestFactory.MakeDatabase(
                payloads:   new PayloadCoreDefinition[]  { _ballistic, _laser },
                deliveries: new DeliveryCoreDefinition[] { _singleAction, _auto, _scatter });
            _registry  = WeaponBuilderTestFactory.MakeRegistry(_db);
            _inventory = new InventoryState();
            _events    = new FakeRaidEvents();
            _nextEId   = 0;
        }

        [TearDown]
        public void TearDown() =>
            WeaponBuilderTestFactory.DestroyAll(_ballistic, _laser, _singleAction, _auto, _scatter, _db);

        // ── Build → resolver detects charge-up requirement ───

        [Test]
        public void BuildLaserPistol_RuntimeWeapon_RequiresChargeUp()
        {
            var runtime = BuildAndAssemble("LaserCharge", "SingleAction");

            Assert.IsTrue(WeaponChargeResolver.RequiresChargeUp(runtime));
            Assert.AreEqual(LaserChargeTime, WeaponChargeResolver.GetChargeTime(runtime));
        }

        [Test]
        public void BuildLaserRifle_RuntimeWeapon_StillRequiresChargeUp()
        {
            // Variant B: charge applies regardless of Delivery.
            var runtime = BuildAndAssemble("LaserCharge", "Auto");

            Assert.IsTrue(WeaponChargeResolver.RequiresChargeUp(runtime));
            Assert.AreEqual(LaserChargeTime, WeaponChargeResolver.GetChargeTime(runtime));
        }

        [Test]
        public void BuildLaserShotgun_RuntimeWeapon_StillRequiresChargeUp()
        {
            var runtime = BuildAndAssemble("LaserCharge", "Scatter");

            Assert.IsTrue(WeaponChargeResolver.RequiresChargeUp(runtime));
        }

        [Test]
        public void BuildBallisticPistol_RuntimeWeapon_NoChargeUp()
        {
            var runtime = BuildAndAssemble("BallisticRound", "SingleAction");

            Assert.IsFalse(WeaponChargeResolver.RequiresChargeUp(runtime));
            Assert.AreEqual(0f, WeaponChargeResolver.GetChargeTime(runtime));
        }

        // ── Scatter parity — new content, no charge ──────────

        [Test]
        public void BuildBallisticShotgun_RuntimeWeapon_UsesScatterDelivery()
        {
            var runtime = BuildAndAssemble("BallisticRound", "Scatter");

            Assert.AreEqual(FiringPattern.Scatter, runtime.DeliveryDefinition.Pattern);
            Assert.AreEqual(7,  runtime.Stats.ProjectilesPerShot);
            Assert.AreEqual(5,  runtime.Stats.MagazineSize);
            Assert.IsFalse(WeaponChargeResolver.RequiresChargeUp(runtime));
        }

        // ── Full pipeline: ShootingSystem honours charge gate ─

        [Test]
        public void ShootingSystem_LaserPistolBuild_FirstTick_EntersChargingNoProjectile()
        {
            var state = BootstrapRaidWithBuiltLaser("SingleAction");

            var input = new FakeInputAdapter { AttackPressed = true };
            var events = new RaidEventBuffer();
            var context = TestContextFactory.Create(input, events);

            ShootingSystem.Tick(state, in context);

            Assert.AreEqual(WeaponPhase.Charging, state.PlayerEntity.EquippedWeapon.Phase);
            Assert.AreEqual(0, state.Projectiles.Count);
            Assert.IsTrue(events.All.Any(e => e.Type == RaidEventType.WeaponChargeStarted));
        }

        [Test]
        public void ShootingSystem_LaserPistolBuild_FiresOnReleaseAfterFullCharge()
        {
            // Tau-cannon mechanic (2026-05-06): full charge no longer auto-fires —
            // user releases fire button to discharge. Holding past ChargeTime keeps
            // weapon у Charging at chargeRatio = 1.0.
            var state = BootstrapRaidWithBuiltLaser("SingleAction");
            var weapon = state.PlayerEntity.EquippedWeapon;

            // Tick 1: enter Charging via press
            var pressInput  = new FakeInputAdapter { AttackPressed = true };
            var events = new RaidEventBuffer();
            var pressCtx = TestContextFactory.Create(pressInput, events);
            state.ElapsedTime = 0f;
            ShootingSystem.Tick(state, in pressCtx);
            Assert.AreEqual(WeaponPhase.Charging, weapon.Phase);

            // Tick 2: still holding past charge time — STAYS Charging.
            state.ElapsedTime = LaserChargeTime + 0.01f;
            ShootingSystem.Tick(state, in pressCtx);
            Assert.AreEqual(WeaponPhase.Charging, weapon.Phase, "Holds at full charge");
            Assert.AreEqual(0, state.Projectiles.Count);

            // Tick 3: release fires the shot.
            var releaseInput = new FakeInputAdapter { AttackPressed = false, AttackJustReleased = true };
            var releaseCtx   = TestContextFactory.Create(releaseInput, events);
            ShootingSystem.Tick(state, in releaseCtx);

            Assert.AreEqual(WeaponPhase.Firing, weapon.Phase, "Release fires charged shot");
            Assert.AreEqual(1, state.Projectiles.Count);
            Assert.IsTrue(events.All.Any(e => e.Type == RaidEventType.WeaponChargeCompleted));
        }

        [Test]
        public void ShootingSystem_LaserShotgunBuild_FiresOnReleaseSpawnsSevenPellets()
        {
            var state = BootstrapRaidWithBuiltLaser("Scatter");
            var weapon = state.PlayerEntity.EquippedWeapon;

            var pressInput = new FakeInputAdapter { AttackPressed = true };
            var pressCtx = TestContextFactory.Create(pressInput);
            state.ElapsedTime = 0f;
            ShootingSystem.Tick(state, in pressCtx);

            state.ElapsedTime = LaserChargeTime + 0.01f;
            var releaseInput = new FakeInputAdapter { AttackPressed = false, AttackJustReleased = true };
            var releaseCtx   = TestContextFactory.Create(releaseInput);
            ShootingSystem.Tick(state, in releaseCtx);

            Assert.AreEqual(WeaponPhase.Firing, weapon.Phase);
            Assert.AreEqual(7, state.Projectiles.Count, "Scatter payload fires 7 pellets on release");
        }

        [Test]
        public void StateMachine_LaserCharging_EarlyReleaseFiresPartialChargeNotCancel()
        {
            // Tau-cannon mechanic: early release no longer cancels — it fires a
            // partial-charge shot з reduced damage. Verifies WeaponStateMachineSystem
            // не cancels (its old behaviour); ShootingSystem owns the fire path.
            var state = BootstrapRaidWithBuiltLaser("SingleAction");
            var weapon = state.PlayerEntity.EquippedWeapon;

            // Start charge
            var startInput = new FakeInputAdapter { AttackPressed = true };
            var startCtx   = TestContextFactory.Create(startInput);
            ShootingSystem.Tick(state, in startCtx);
            Assert.AreEqual(WeaponPhase.Charging, weapon.Phase);

            // Release at 40% charge
            state.ElapsedTime = 0.4f;
            var releaseInput = new FakeInputAdapter { AttackPressed = false, AttackJustReleased = true };
            var events = new RaidEventBuffer();
            var releaseCtx = TestContextFactory.Create(releaseInput, events);
            WeaponStateMachineSystem.Tick(state, in releaseCtx);

            Assert.AreEqual(WeaponPhase.Charging, weapon.Phase,
                "WSMS leaves charging — ShootingSystem fires the partial-charge shot");
            Assert.IsFalse(events.All.Any(e => e.Type == RaidEventType.WeaponChargeCancelled),
                "No cancel event — release is now a fire trigger");
        }

        // ── Helpers ───────────────────────────────────────────

        WeaponEntityState BuildAndAssemble(string payloadId, string deliveryId)
        {
            // Tier 6 G6: Build consumes module items from backpack. Place them first.
            int slot = 0;
            while (slot < InventoryState.BackpackSize && _inventory.Backpack[slot] != null) slot++;
            if (slot < InventoryState.BackpackSize)
                _inventory.Backpack[slot++] = ItemState.Create(AllocateEId(), payloadId);
            while (slot < InventoryState.BackpackSize && _inventory.Backpack[slot] != null) slot++;
            if (slot < InventoryState.BackpackSize)
                _inventory.Backpack[slot] = ItemState.Create(AllocateEId(), deliveryId);

            var presenter = new WeaponBuilderPresenter(_registry, _inventory, AllocateEId);
            presenter.SelectPayload(payloadId);
            presenter.SelectDelivery(deliveryId);
            Assert.IsTrue(presenter.TryBuild(out _));

            // After consume + weapon placement, weapon lands у freed module slot (slot 0).
            var item = _inventory.Backpack[0];
            var runtime = WeaponSyncSystem.BuildWeaponForItem(item, _registry, _events);
            Assert.IsNotNull(runtime);
            return runtime;
        }

        /// <summary>
        /// Builds a Laser+delivery item, equips it, and returns a <see cref="RaidState"/>
        /// with PlayerEntity + EquippedWeapon ready for ShootingSystem.Tick.
        /// </summary>
        RaidState BootstrapRaidWithBuiltLaser(string deliveryId)
        {
            var state = EditModeTestsUtils.CreateStateWithPlayer(Vector3.zero);
            state.PlayerEntity.FacingDirection = Vector3.forward;

            // Replace the default (rifle-like) equipped weapon with a real Laser assembly.
            var weapon = BuildAndAssemble("LaserCharge", deliveryId);
            state.PlayerEntity.Hotbar[0] = weapon;
            state.PlayerEntity.SelectedHotbarSlot = 0;
            state.PlayerEntity.EquippedWeapon = weapon;
            return state;
        }

        // ── Delivery-specific helper (keeps fixture-local defaults for magazine etc.) ─

        static DeliveryCoreDefinition MakeDelivery(
            string id, string formFactor, FiringPattern pattern, int magazine, int projectilesPerShot = 1)
            => WeaponBuilderTestFactory.MakeDelivery(id, formFactor: formFactor, pattern: pattern,
                commonStats: new DeliveryStats
                {
                    FireInterval       = 0.2f,
                    ProjectilesPerShot = projectilesPerShot,
                    SpreadAngle        = projectilesPerShot > 1 ? 30f : 0f,
                    ConeHalfAngle      = 45f,
                    MagazineSize       = magazine,
                    ReloadTime         = 2f,
                });
    }
}
