using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Adapters;
using NUnit.Framework;
using Session;
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
            _ballistic = MakeBallistic("BallisticRound", "Ballistic", "Ammo_Rifle");
            _laser     = MakeLaser(chargeTime: LaserChargeTime);
            _singleAction = MakeDelivery("SingleAction", "Pistol", FiringPattern.Single, magazine: 12);
            _auto         = MakeDelivery("Auto",         "Rifle",  FiringPattern.Auto,   magazine: 30);
            _scatter      = MakeDelivery("Scatter",      "Shotgun", FiringPattern.Scatter, magazine: 5,
                projectilesPerShot: 7);

            _db = ScriptableObject.CreateInstance<CoreDefinitionDatabase>();
            _db.SetEntries(
                new List<PayloadCoreDefinition>  { _ballistic, _laser },
                new List<DeliveryCoreDefinition> { _singleAction, _auto, _scatter },
                new List<ExoticModDefinition>());

            _registry  = new DatabaseCoreDefinitionRegistry(_db);
            _inventory = new InventoryState();
            _events    = new FakeRaidEvents();
            _nextEId   = 0;
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_ballistic);
            Object.DestroyImmediate(_laser);
            Object.DestroyImmediate(_singleAction);
            Object.DestroyImmediate(_auto);
            Object.DestroyImmediate(_scatter);
            Object.DestroyImmediate(_db);
        }

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
            var context = MakeContext(input, events);

            ShootingSystem.Tick(state, in context);

            Assert.AreEqual(WeaponPhase.Charging, state.PlayerEntity.EquippedWeapon.Phase);
            Assert.AreEqual(0, state.Projectiles.Count);
            Assert.IsTrue(events.All.Any(e => e.Type == RaidEventType.WeaponChargeStarted));
        }

        [Test]
        public void ShootingSystem_LaserPistolBuild_AfterChargeElapsed_FiresSingleProjectile()
        {
            var state = BootstrapRaidWithBuiltLaser("SingleAction");
            var weapon = state.PlayerEntity.EquippedWeapon;

            // Tick 1: enter Charging
            var input  = new FakeInputAdapter { AttackPressed = true };
            var events = new RaidEventBuffer();
            var context = MakeContext(input, events);
            state.ElapsedTime = 0f;
            ShootingSystem.Tick(state, in context);
            Assert.AreEqual(WeaponPhase.Charging, weapon.Phase);

            // Tick 2: charge elapsed — same input, advance time past ChargeTime
            state.ElapsedTime = LaserChargeTime + 0.01f;
            ShootingSystem.Tick(state, in context);

            Assert.AreEqual(WeaponPhase.Firing, weapon.Phase, "Fire after charge");
            Assert.AreEqual(1, state.Projectiles.Count);
            Assert.IsTrue(events.All.Any(e => e.Type == RaidEventType.WeaponChargeCompleted));
        }

        [Test]
        public void ShootingSystem_LaserShotgunBuild_AfterChargeElapsed_SpawnsSevenPellets()
        {
            var state = BootstrapRaidWithBuiltLaser("Scatter");
            var weapon = state.PlayerEntity.EquippedWeapon;

            var input = new FakeInputAdapter { AttackPressed = true };
            var context = MakeContext(input);
            state.ElapsedTime = 0f;
            ShootingSystem.Tick(state, in context); // Charging

            state.ElapsedTime = LaserChargeTime + 0.01f;
            ShootingSystem.Tick(state, in context); // Fire

            Assert.AreEqual(WeaponPhase.Firing, weapon.Phase);
            Assert.AreEqual(7, state.Projectiles.Count, "Scatter payload fires 7 pellets after charge");
        }

        [Test]
        public void StateMachine_LaserCharging_CancelOnRelease_ReturnsToReady()
        {
            var state = BootstrapRaidWithBuiltLaser("SingleAction");
            var weapon = state.PlayerEntity.EquippedWeapon;

            // Start charge
            var startInput = new FakeInputAdapter { AttackPressed = true };
            var ctx  = MakeContext(startInput);
            ShootingSystem.Tick(state, in ctx);
            Assert.AreEqual(WeaponPhase.Charging, weapon.Phase);

            // Now release
            state.ElapsedTime = 0.4f; // half-way
            var releaseInput = new FakeInputAdapter { AttackPressed = false, AttackJustReleased = true };
            var events = new RaidEventBuffer();
            ctx = MakeContext(releaseInput, events);
            WeaponStateMachineSystem.Tick(state, in ctx);

            Assert.AreEqual(WeaponPhase.Ready, weapon.Phase);
            Assert.AreEqual(0, state.Projectiles.Count);
            Assert.IsTrue(events.All.Any(e => e.Type == RaidEventType.WeaponChargeCancelled));
        }

        // ── Helpers ───────────────────────────────────────────

        WeaponEntityState BuildAndAssemble(string payloadId, string deliveryId)
        {
            var presenter = new WeaponBuilderPresenter(_registry, _inventory, AllocateEId);
            presenter.SelectPayload(payloadId);
            presenter.SelectDelivery(deliveryId);
            Assert.IsTrue(presenter.TryBuild(out _));

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

        static RaidContext MakeContext(FakeInputAdapter input, IRaidEvents events = null)
        {
            return new RaidContext(
                deltaTime: 1f / 60f,
                events: events ?? new RaidEventBuffer(),
                time: new FakeTimeAdapter { DeltaTime = 1f / 60f },
                input: input,
                navMesh: new FakeNavMeshAdapter());
        }

        // ── SO factories ──────────────────────────────────────

        static BallisticPayloadDefinition MakeBallistic(string id, string displayName, string ammoType)
        {
            var def = ScriptableObject.CreateInstance<BallisticPayloadDefinition>();
            SetPrivateField(def, "_id",          id);
            SetPrivateField(def, "_displayName", displayName);
            SetPrivateField(def, "_ammoType",    ammoType);
            var array = new CommonPayloadStats[5];
            array[(int)RarityTier.Common] = new CommonPayloadStats
            {
                Damage = 15f, ProjectileSpeed = 25f, ProjectileLifetime = 2.5f,
                HeadshotDamageMultiplier = 2f, BasePenetration = 15f, BaseArmorDamage = 5f,
            };
            SetPrivateField(def, "_statsByTier", array);
            return def;
        }

        static LaserPayloadDefinition MakeLaser(float chargeTime)
        {
            var def = ScriptableObject.CreateInstance<LaserPayloadDefinition>();
            SetPrivateField(def, "_id",          "LaserCharge");
            SetPrivateField(def, "_displayName", "Laser");
            SetPrivateField(def, "_ammoType",    "Ammo_EnergyCell");
            var stats = new CommonPayloadStats[5];
            stats[(int)RarityTier.Common] = new CommonPayloadStats
            {
                Damage = 25f, ProjectileSpeed = 50f, ProjectileLifetime = 3f,
                HeadshotDamageMultiplier = 2f, BasePenetration = 25f, BaseArmorDamage = 8f,
            };
            SetPrivateField(def, "_statsByTier", stats);

            var specific = new LaserSpecificStats[5];
            specific[(int)RarityTier.Common] = new LaserSpecificStats { ChargeTime = chargeTime };
            SetPrivateField(def, "_specificByTier", specific);
            return def;
        }

        static DeliveryCoreDefinition MakeDelivery(
            string id, string formFactor, FiringPattern pattern, int magazine, int projectilesPerShot = 1)
        {
            var def = ScriptableObject.CreateInstance<DeliveryCoreDefinition>();
            SetPrivateField(def, "_id",         id);
            SetPrivateField(def, "_formFactor", formFactor);
            SetPrivateField(def, "_pattern",    pattern);
            var array = new DeliveryStats[5];
            array[(int)RarityTier.Common] = new DeliveryStats
            {
                FireInterval       = 0.2f,
                ProjectilesPerShot = projectilesPerShot,
                SpreadAngle        = projectilesPerShot > 1 ? 30f : 0f,
                ConeHalfAngle      = 45f,
                MagazineSize       = magazine,
                ReloadTime         = 2f,
            };
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
