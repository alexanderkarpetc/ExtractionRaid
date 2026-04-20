using NUnit.Framework;
using State;

namespace Tests.EditMode
{
    [TestFixture]
    public class CoreInstanceTests
    {
        // ── PayloadCoreInstance equality ──────────────────────

        [Test]
        public void PayloadCoreInstance_SameIdAndRarity_AreEqual()
        {
            var a = new PayloadCoreInstance("BallisticRound", RarityTier.Common);
            var b = new PayloadCoreInstance("BallisticRound", RarityTier.Common);
            Assert.IsTrue(a.Equals(b));
            Assert.IsTrue(a == b);
            Assert.IsFalse(a != b);
            Assert.AreEqual(a.GetHashCode(), b.GetHashCode());
        }

        [Test]
        public void PayloadCoreInstance_DifferentId_AreNotEqual()
        {
            var a = new PayloadCoreInstance("BallisticRound", RarityTier.Common);
            var b = new PayloadCoreInstance("LaserCharge", RarityTier.Common);
            Assert.IsFalse(a.Equals(b));
            Assert.IsTrue(a != b);
        }

        [Test]
        public void PayloadCoreInstance_DifferentRarity_AreNotEqual()
        {
            var a = new PayloadCoreInstance("BallisticRound", RarityTier.Common);
            var b = new PayloadCoreInstance("BallisticRound", RarityTier.Legendary);
            Assert.IsFalse(a.Equals(b));
            Assert.IsTrue(a != b);
        }

        [Test]
        public void PayloadCoreInstance_ConstructorStoresFields()
        {
            var inst = new PayloadCoreInstance("MicroRocket", RarityTier.Rare);
            Assert.AreEqual("MicroRocket", inst.DefinitionId);
            Assert.AreEqual(RarityTier.Rare, inst.Rarity);
        }

        // ── DeliveryCoreInstance equality ─────────────────────

        [Test]
        public void DeliveryCoreInstance_SameIdAndRarity_AreEqual()
        {
            var a = new DeliveryCoreInstance("SingleAction", RarityTier.Uncommon);
            var b = new DeliveryCoreInstance("SingleAction", RarityTier.Uncommon);
            Assert.IsTrue(a == b);
            Assert.AreEqual(a.GetHashCode(), b.GetHashCode());
        }

        [Test]
        public void DeliveryCoreInstance_DifferentFields_AreNotEqual()
        {
            var a = new DeliveryCoreInstance("SingleAction", RarityTier.Common);
            var b = new DeliveryCoreInstance("Auto",         RarityTier.Common);
            var c = new DeliveryCoreInstance("SingleAction", RarityTier.Epic);
            Assert.AreNotEqual(a, b);
            Assert.AreNotEqual(a, c);
        }

        // ── ExoticModInstance equality (no rarity) ────────────

        [Test]
        public void ExoticModInstance_SameId_AreEqual()
        {
            var a = new ExoticModInstance("Ricochet");
            var b = new ExoticModInstance("Ricochet");
            Assert.IsTrue(a == b);
            Assert.AreEqual(a.GetHashCode(), b.GetHashCode());
        }

        [Test]
        public void ExoticModInstance_DifferentId_AreNotEqual()
        {
            var a = new ExoticModInstance("Ricochet");
            var b = new ExoticModInstance("SplitOnImpact");
            Assert.IsFalse(a == b);
            Assert.IsTrue(a != b);
        }

        [Test]
        public void ExoticModInstance_ConstructorStoresId()
        {
            var inst = new ExoticModInstance("BoomerangFlight");
            Assert.AreEqual("BoomerangFlight", inst.DefinitionId);
        }

        // ── Enum sanity ───────────────────────────────────────

        [Test]
        public void RarityTier_IntValuesMatchOrder()
        {
            // Used as serialized array index — stable explicit values are required.
            Assert.AreEqual(0, (int)RarityTier.Common);
            Assert.AreEqual(1, (int)RarityTier.Uncommon);
            Assert.AreEqual(2, (int)RarityTier.Rare);
            Assert.AreEqual(3, (int)RarityTier.Epic);
            Assert.AreEqual(4, (int)RarityTier.Legendary);
        }

        // ── WeaponConfiguration nullable Exotic pattern ───────

        [Test]
        public void WeaponConfiguration_WithoutExotic_ExoticIsNull()
        {
            var cfg = new WeaponConfiguration(
                new PayloadCoreInstance("BallisticRound", RarityTier.Common),
                new DeliveryCoreInstance("SingleAction",  RarityTier.Common),
                exotic: null,
                ammoInMagazine: 12);
            Assert.IsFalse(cfg.Exotic.HasValue);
            Assert.AreEqual(12, cfg.AmmoInMagazine);
        }

        [Test]
        public void WeaponConfiguration_WithExotic_ExoticRoundtrips()
        {
            var exotic = new ExoticModInstance("Ricochet");
            var cfg = new WeaponConfiguration(
                new PayloadCoreInstance("BallisticRound", RarityTier.Common),
                new DeliveryCoreInstance("Auto",          RarityTier.Common),
                exotic: exotic,
                ammoInMagazine: 30);
            Assert.IsTrue(cfg.Exotic.HasValue);
            Assert.AreEqual(exotic, cfg.Exotic.Value);
        }

        [Test]
        public void WeaponConfiguration_ExoticSetterTogglesFlag()
        {
            var cfg = new WeaponConfiguration(
                new PayloadCoreInstance("BallisticRound", RarityTier.Common),
                new DeliveryCoreInstance("SingleAction",  RarityTier.Common),
                exotic: null,
                ammoInMagazine: 0);

            cfg.Exotic = new ExoticModInstance("SplitOnImpact");
            Assert.IsTrue(cfg.Exotic.HasValue);
            Assert.AreEqual("SplitOnImpact", cfg.Exotic.Value.DefinitionId);

            cfg.Exotic = null;
            Assert.IsFalse(cfg.Exotic.HasValue);
        }
    }
}
