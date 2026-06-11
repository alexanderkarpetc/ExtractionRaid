using NUnit.Framework;
using State;
using Tests.EditMode.Fakes;

namespace Tests.EditMode
{
    /// <summary>
    /// Unauthored higher rarity tiers (per-tier values are Tier 4b — not yet filled)
    /// fall back to Common in StatsByTier / SpecificByTier, so a non-Common rarity
    /// never yields a zero-stat weapon. Rarity is visual-only until per-tier values exist.
    /// </summary>
    [TestFixture]
    public class RarityTierFallbackTests
    {
        [Test]
        public void Delivery_UnauthoredTier_FallsBackToCommon()
        {
            var d = WeaponBuilderTestFactory.MakeDelivery(
                commonStats: new DeliveryStats { FireInterval = 0.4f, MagazineSize = 12 });
            try
            {
                var rare = d.StatsByTier(RarityTier.Rare);
                Assert.AreEqual(12,   rare.MagazineSize);
                Assert.AreEqual(0.4f, rare.FireInterval);
            }
            finally { WeaponBuilderTestFactory.DestroyAll(d); }
        }

        [Test]
        public void Payload_UnauthoredTier_FallsBackToCommon()
        {
            var p = WeaponBuilderTestFactory.MakeBallistic(
                commonStats: new CommonPayloadStats { Damage = 15f });
            try
            {
                Assert.AreEqual(15f, p.StatsByTier(RarityTier.Legendary).Damage);
            }
            finally { WeaponBuilderTestFactory.DestroyAll(p); }
        }

        [Test]
        public void Laser_UnauthoredTier_FallsBackToCommonChargeTime()
        {
            var laser = WeaponBuilderTestFactory.MakeLaser(chargeTime: 1.0f);
            try
            {
                Assert.AreEqual(1.0f, laser.SpecificByTier(RarityTier.Epic).ChargeTime);
            }
            finally { WeaponBuilderTestFactory.DestroyAll(laser); }
        }

        [Test]
        public void CommonTier_ReturnsOwnValues_NoFallback()
        {
            var d = WeaponBuilderTestFactory.MakeDelivery(
                commonStats: new DeliveryStats { MagazineSize = 30 });
            try
            {
                Assert.AreEqual(30, d.StatsByTier(RarityTier.Common).MagazineSize);
            }
            finally { WeaponBuilderTestFactory.DestroyAll(d); }
        }

        [Test]
        public void AuthoredHigherTier_IsNotOverriddenByFallback()
        {
            // A non-default higher tier must keep its own values (regression guard
            // for WeaponStatComposer rarity-scaling tests).
            var d = WeaponBuilderTestFactory.MakeDelivery(
                commonStats: new DeliveryStats { MagazineSize = 10 },
                statsTier:   RarityTier.Epic);
            try
            {
                Assert.AreEqual(10, d.StatsByTier(RarityTier.Epic).MagazineSize);
            }
            finally { WeaponBuilderTestFactory.DestroyAll(d); }
        }
    }
}
