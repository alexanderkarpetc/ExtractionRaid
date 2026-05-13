using NUnit.Framework;
using Session;
using State;
using Systems;
using Tests.EditMode.Fakes;
using UnityEngine;

namespace Tests.EditMode
{
    /// <summary>
    /// Coverage for B1 Ballistic Rifle heat-up spread:
    /// - WeaponHeatSystem decay tick
    /// - ShootingSystem heat increment (Ballistic+Auto only)
    /// - BarrelHeatConfig curve formula
    /// </summary>
    [TestFixture]
    public class WeaponHeatSystemTests
    {
        // ── BarrelHeatConfig.SpreadMultiplier curve ──

        [Test]
        public void SpreadMultiplier_AtZeroHeat_IsOne()
        {
            var cfg = BarrelHeatConfig.Default; // MaxSpread=3, Power=1.8
            Assert.AreEqual(1f, cfg.SpreadMultiplier(0f), 1e-4f);
        }

        [Test]
        public void SpreadMultiplier_AtFullHeat_IsMax()
        {
            var cfg = BarrelHeatConfig.Default;
            Assert.AreEqual(cfg.MaxSpreadMultiplier, cfg.SpreadMultiplier(1f), 1e-4f);
        }

        [Test]
        public void SpreadMultiplier_HalfHeat_IsBelowLinearMidpoint()
        {
            // Linear midpoint with max=3 would be 2.0. Parabolic curve power=1.8 →
            // pow(0.5, 1.8) ≈ 0.287 → 1 + 0.287 × 2 ≈ 1.575. Far below 2.0 → early forgiving.
            var cfg = BarrelHeatConfig.Default;
            float mid = cfg.SpreadMultiplier(0.5f);
            Assert.Less(mid, 2f, "Mid heat should be below linear midpoint (early forgiving)");
            Assert.AreEqual(1.575f, mid, 1e-2f);
        }

        // ── WeaponHeatSystem.Tick decay ──

        [Test]
        public void Tick_HeatLevel_DecaysOverTime()
        {
            var state = EditModeTestsUtils.CreateStateWithPlayer(Vector3.zero);
            var weapon = state.PlayerEntity.EquippedWeapon;
            weapon.HeatLevel = 1f;
            // 0.5/s decay × 0.1s dt = 0.05 expected drop per call
            var ctx = TestContextFactory.Create(
                barrelHeatConfig: new BarrelHeatConfig
                {
                    Enabled = true,
                    DecayPerSecond = 0.5f,
                    MaxHeatShots = 12,
                    HeatCurvePower = 1.8f,
                    MaxSpreadMultiplier = 3f,
                },
                deltaTime: 0.1f);

            WeaponHeatSystem.Tick(state, in ctx);

            Assert.AreEqual(0.95f, weapon.HeatLevel, 1e-3f);
        }

        [Test]
        public void Tick_HeatLevel_ClampedToZero()
        {
            var state = EditModeTestsUtils.CreateStateWithPlayer(Vector3.zero);
            var weapon = state.PlayerEntity.EquippedWeapon;
            weapon.HeatLevel = 0.01f;
            var ctx = TestContextFactory.Create(
                barrelHeatConfig: new BarrelHeatConfig
                {
                    Enabled = true,
                    DecayPerSecond = 10f, // big decay to test clamp
                    MaxHeatShots = 12,
                    HeatCurvePower = 1.8f,
                    MaxSpreadMultiplier = 3f,
                },
                deltaTime: 1f);

            WeaponHeatSystem.Tick(state, in ctx);

            Assert.AreEqual(0f, weapon.HeatLevel, "Heat clamps to 0, doesn't go negative");
        }

        [Test]
        public void Tick_Disabled_HeatLevelUnchanged()
        {
            var state = EditModeTestsUtils.CreateStateWithPlayer(Vector3.zero);
            var weapon = state.PlayerEntity.EquippedWeapon;
            weapon.HeatLevel = 0.5f;
            var ctx = TestContextFactory.Create(
                barrelHeatConfig: new BarrelHeatConfig { Enabled = false, DecayPerSecond = 0.5f },
                deltaTime: 1f);

            WeaponHeatSystem.Tick(state, in ctx);

            Assert.AreEqual(0.5f, weapon.HeatLevel, "Decay skipped while gate disabled");
        }

        // ── ShootingSystem heat increment ──

        [Test]
        public void Tick_BallisticAuto_FireIncrementsHeat()
        {
            // Explicit Ballistic+Auto setup — default weapon has no PayloadDefinition, so we set SOs.
            var state = EditModeTestsUtils.CreateStateWithPlayer(Vector3.zero);
            state.PlayerEntity.FacingDirection = Vector3.forward;
            var weapon = state.PlayerEntity.EquippedWeapon;
            var ballisticSO = WeaponBuilderTestFactory.MakeBallistic();
            var autoSO = WeaponBuilderTestFactory.MakeDelivery(id: "Auto", pattern: FiringPattern.Auto);
            try
            {
                weapon.PayloadDefinition = ballisticSO;
                weapon.DeliveryDefinition = autoSO;
                float startHeat = weapon.HeatLevel;
                var input = new FakeInputAdapter { AttackPressed = true };
                var ctx = TestContextFactory.Create(input, barrelHeatConfig: new BarrelHeatConfig
                {
                    Enabled = true,
                    MaxHeatShots = 10,
                    DecayPerSecond = 0f,
                    HeatCurvePower = 1.8f,
                    MaxSpreadMultiplier = 3f,
                });

                ShootingSystem.Tick(state, in ctx);

                Assert.Greater(weapon.HeatLevel, startHeat, "Heat should grow after Ballistic+Auto fire");
                Assert.AreEqual(0.1f, weapon.HeatLevel, 1e-4f, "1 shot at MaxHeatShots=10 → 0.1");
            }
            finally
            {
                Object.DestroyImmediate(ballisticSO);
                Object.DestroyImmediate(autoSO);
            }
        }

        [Test]
        public void Tick_BallisticAuto_Disabled_HeatStaysZero()
        {
            var state = EditModeTestsUtils.CreateStateWithPlayer(Vector3.zero);
            state.PlayerEntity.FacingDirection = Vector3.forward;
            var weapon = state.PlayerEntity.EquippedWeapon;
            var ballisticSO = WeaponBuilderTestFactory.MakeBallistic();
            var autoSO = WeaponBuilderTestFactory.MakeDelivery(id: "Auto", pattern: FiringPattern.Auto);
            try
            {
                weapon.PayloadDefinition = ballisticSO;
                weapon.DeliveryDefinition = autoSO;
                weapon.HeatLevel = 0f;
                var input = new FakeInputAdapter { AttackPressed = true };
                var ctx = TestContextFactory.Create(input, barrelHeatConfig: new BarrelHeatConfig
                {
                    Enabled = false,
                    MaxHeatShots = 10,
                });

                ShootingSystem.Tick(state, in ctx);

                Assert.AreEqual(0f, weapon.HeatLevel, "Heat increment gated off");
            }
            finally
            {
                Object.DestroyImmediate(ballisticSO);
                Object.DestroyImmediate(autoSO);
            }
        }

        [Test]
        public void Tick_LaserShotgun_DoesNotIncrementHeat()
        {
            // Laser+Scatter (Laser Shotgun) is not Ballistic+Auto → no increment.
            var state = EditModeTestsUtils.CreateStateWithPlayer(Vector3.zero);
            state.PlayerEntity.FacingDirection = Vector3.forward;
            var weapon = state.PlayerEntity.EquippedWeapon;
            var laserSO = WeaponBuilderTestFactory.MakeLaser(chargeTime: 1f);
            var scatterSO = WeaponBuilderTestFactory.MakeDelivery(id: "Scatter", pattern: FiringPattern.Scatter);
            try
            {
                weapon.PayloadDefinition = laserSO;
                weapon.DeliveryDefinition = scatterSO;
                weapon.Phase = WeaponPhase.Charging;
                weapon.ChargeStartTime = 0f;
                weapon.HeatLevel = 0f;
                state.ElapsedTime = 1.1f; // fully charged
                var input = new FakeInputAdapter { AttackJustReleased = true };
                var ctx = TestContextFactory.Create(input, barrelHeatConfig: new BarrelHeatConfig
                {
                    Enabled = true,
                    MaxHeatShots = 10,
                });

                ShootingSystem.Tick(state, in ctx);

                Assert.Greater(state.Projectiles.Count, 0, "Laser shotgun should fire");
                Assert.AreEqual(0f, weapon.HeatLevel, "Heat не повинен зростати для non-Ballistic+Auto");
            }
            finally
            {
                Object.DestroyImmediate(laserSO);
                Object.DestroyImmediate(scatterSO);
            }
        }

        [Test]
        public void Tick_BallisticAuto_HeatClampedToOne()
        {
            var state = EditModeTestsUtils.CreateStateWithPlayer(Vector3.zero);
            state.PlayerEntity.FacingDirection = Vector3.forward;
            var weapon = state.PlayerEntity.EquippedWeapon;
            var ballisticSO = WeaponBuilderTestFactory.MakeBallistic();
            var autoSO = WeaponBuilderTestFactory.MakeDelivery(id: "Auto", pattern: FiringPattern.Auto);
            try
            {
                weapon.PayloadDefinition = ballisticSO;
                weapon.DeliveryDefinition = autoSO;
                weapon.HeatLevel = 0.95f;
                var input = new FakeInputAdapter { AttackPressed = true };
                var ctx = TestContextFactory.Create(input, barrelHeatConfig: new BarrelHeatConfig
                {
                    Enabled = true,
                    MaxHeatShots = 5, // +0.2 increment would push over 1
                    DecayPerSecond = 0f,
                    HeatCurvePower = 1.8f,
                    MaxSpreadMultiplier = 3f,
                });

                ShootingSystem.Tick(state, in ctx);

                Assert.AreEqual(1f, weapon.HeatLevel, "Heat clamps to 1.0 ceiling");
            }
            finally
            {
                Object.DestroyImmediate(ballisticSO);
                Object.DestroyImmediate(autoSO);
            }
        }
    }
}
