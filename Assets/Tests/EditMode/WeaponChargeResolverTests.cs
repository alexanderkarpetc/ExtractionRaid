using NUnit.Framework;
using Session;
using State;
using Systems;
using Tests.EditMode.Fakes;
using UnityEngine;

namespace Tests.EditMode
{
    /// <summary>
    /// A4 — per-delivery charge time multiplier. Verifies pure-logic composition
    /// у <see cref="WeaponChargeResolver"/> + <see cref="LaserConfig"/>.
    /// </summary>
    [TestFixture]
    public class WeaponChargeResolverTests
    {
        [Test]
        public void GetChargeTime_NonLaserPayload_ReturnsZero()
        {
            var state = EditModeTestsUtils.CreateStateWithPlayer(Vector3.zero);
            var weapon = state.PlayerEntity.EquippedWeapon;
            Assert.IsNull(weapon.PayloadDefinition, "precondition: default = no payload");

            Assert.AreEqual(0f, WeaponChargeResolver.GetChargeTime(weapon));
            Assert.AreEqual(0f, WeaponChargeResolver.GetChargeTime(weapon, 5f),
                "Multiplier irrelevant без laser payload");
        }

        [Test]
        public void GetChargeTime_LaserPayload_AppliesMultiplier()
        {
            // ChargeTime base = 1.0s. Multiplier 0.6 → effective 0.6s (pistol identity).
            var state = EditModeTestsUtils.CreateStateWithPlayer(Vector3.zero);
            var weapon = state.PlayerEntity.EquippedWeapon;
            var laser = WeaponBuilderTestFactory.MakeLaser(chargeTime: 1f);
            try
            {
                weapon.PayloadDefinition = laser;
                Assert.AreEqual(1f, WeaponChargeResolver.GetChargeTime(weapon), 1e-4f,
                    "Raw payload time stays unchanged");
                Assert.AreEqual(0.6f, WeaponChargeResolver.GetChargeTime(weapon, 0.6f), 1e-4f,
                    "Pistol multiplier shortens charge");
                Assert.AreEqual(1.5f, WeaponChargeResolver.GetChargeTime(weapon, 1.5f), 1e-4f,
                    "Shotgun multiplier lengthens charge");
            }
            finally { Object.DestroyImmediate(laser); }
        }

        // ── LaserConfig.ChargeTimeMultiplierFor mapping ──

        [Test]
        public void LaserConfig_ChargeTimeMultiplierFor_RoutesPatternToField()
        {
            var cfg = LaserConfig.Default;
            // Defaults: 0.6 / 1.0 / 1.5
            Assert.AreEqual(0.6f, cfg.ChargeTimeMultiplierFor(FiringPattern.Single), 1e-4f);
            Assert.AreEqual(1.0f, cfg.ChargeTimeMultiplierFor(FiringPattern.Auto), 1e-4f);
            Assert.AreEqual(1.5f, cfg.ChargeTimeMultiplierFor(FiringPattern.Scatter), 1e-4f);
        }

        [Test]
        public void LaserConfig_ChargeTimeMultiplierFor_CustomValues()
        {
            // Confirm field-routing, not constants.
            var cfg = new LaserConfig
            {
                SingleActionChargeMult = 0.4f,
                AutoChargeMult         = 0.9f,
                ScatterChargeMult      = 2f,
            };
            Assert.AreEqual(0.4f, cfg.ChargeTimeMultiplierFor(FiringPattern.Single), 1e-4f);
            Assert.AreEqual(0.9f, cfg.ChargeTimeMultiplierFor(FiringPattern.Auto), 1e-4f);
            Assert.AreEqual(2f,   cfg.ChargeTimeMultiplierFor(FiringPattern.Scatter), 1e-4f);
        }
    }
}
