using Adapters;
using NUnit.Framework;
using State;
using Systems;
using Tests.EditMode.Fakes;

namespace Tests.EditMode
{
    [TestFixture]
    public class WeaponAssemblySystemTests
    {
        CoreDefinitionDatabase     _db;
        BallisticPayloadDefinition _ballistic;
        DeliveryCoreDefinition     _single;
        ExoticModDefinition        _ricochet;

        [SetUp]
        public void SetUp()
        {
            _ballistic = WeaponBuilderTestFactory.MakeBallistic("BallisticRound",
                commonStats: new CommonPayloadStats { Damage = 15f, ProjectileSpeed = 25f });
            _single = WeaponBuilderTestFactory.MakeDelivery("SingleAction",
                commonStats: new DeliveryStats { FireInterval = 0.4f, MagazineSize = 12 });
            _ricochet = WeaponBuilderTestFactory.MakeExotic("Ricochet");

            _db = WeaponBuilderTestFactory.MakeDatabase(
                payloads:   new PayloadCoreDefinition[]  { _ballistic },
                deliveries: new DeliveryCoreDefinition[] { _single },
                exotics:    new[]                         { _ricochet });
        }

        [TearDown]
        public void TearDown() =>
            WeaponBuilderTestFactory.DestroyAll(_ballistic, _single, _ricochet, _db);

        ICoreDefinitionRegistry Registry() => WeaponBuilderTestFactory.MakeRegistry(_db);

        // ── Success path ──────────────────────────────────────

        [Test]
        public void TryAssemble_ValidConfig_NoExotic_Succeeds()
        {
            var config = new WeaponConfiguration(
                new PayloadCoreInstance("BallisticRound", RarityTier.Common),
                new DeliveryCoreInstance("SingleAction",  RarityTier.Common),
                exotic: null,
                ammoInMagazine: 12);

            var ok = WeaponAssemblySystem.TryAssemble(config, Registry(), out var result, out var reason);

            Assert.IsTrue(ok);
            Assert.IsNull(reason);
            Assert.AreSame(_ballistic, result.PayloadDefinition);
            Assert.AreSame(_single,    result.DeliveryDefinition);
            Assert.IsNull(result.ExoticDefinition);
            Assert.AreEqual(15f, result.Stats.Damage);
            Assert.AreEqual(12,  result.Stats.MagazineSize);
        }

        [Test]
        public void TryAssemble_ValidConfig_WithExotic_Succeeds()
        {
            var config = new WeaponConfiguration(
                new PayloadCoreInstance("BallisticRound", RarityTier.Common),
                new DeliveryCoreInstance("SingleAction",  RarityTier.Common),
                exotic: new ExoticModInstance("Ricochet"),
                ammoInMagazine: 0);

            var ok = WeaponAssemblySystem.TryAssemble(config, Registry(), out var result, out var reason);

            Assert.IsTrue(ok);
            Assert.IsNull(reason);
            Assert.AreSame(_ricochet, result.ExoticDefinition);
        }

        // ── Fail paths (per D7 — strict, no auto-repair) ──────

        [Test]
        public void TryAssemble_MissingPayload_Fails()
        {
            var config = new WeaponConfiguration(
                new PayloadCoreInstance("MicroRocket",   RarityTier.Common), // not in DB
                new DeliveryCoreInstance("SingleAction", RarityTier.Common),
                exotic: null,
                ammoInMagazine: 0);

            var ok = WeaponAssemblySystem.TryAssemble(config, Registry(), out _, out var reason);

            Assert.IsFalse(ok);
            StringAssert.Contains("MicroRocket", reason);
            StringAssert.Contains("Payload",     reason);
        }

        [Test]
        public void TryAssemble_MissingDelivery_Fails()
        {
            var config = new WeaponConfiguration(
                new PayloadCoreInstance("BallisticRound", RarityTier.Common),
                new DeliveryCoreInstance("Rotary",        RarityTier.Common), // not in DB
                exotic: null,
                ammoInMagazine: 0);

            var ok = WeaponAssemblySystem.TryAssemble(config, Registry(), out _, out var reason);

            Assert.IsFalse(ok);
            StringAssert.Contains("Rotary",   reason);
            StringAssert.Contains("Delivery", reason);
        }

        [Test]
        public void TryAssemble_MissingExotic_FailsStrictly()
        {
            // Strict C per D7: missing exotic kills the whole assembly (no auto-repair).
            var config = new WeaponConfiguration(
                new PayloadCoreInstance("BallisticRound", RarityTier.Common),
                new DeliveryCoreInstance("SingleAction",  RarityTier.Common),
                exotic: new ExoticModInstance("SplitOnImpact"), // not in DB
                ammoInMagazine: 0);

            var ok = WeaponAssemblySystem.TryAssemble(config, Registry(), out _, out var reason);

            Assert.IsFalse(ok);
            StringAssert.Contains("SplitOnImpact", reason);
            StringAssert.Contains("Exotic",        reason);
        }

        // ── Registry null guard ───────────────────────────────

        [Test]
        public void TryAssemble_NullRegistry_FailsGracefully()
        {
            var config = new WeaponConfiguration(
                new PayloadCoreInstance("BallisticRound", RarityTier.Common),
                new DeliveryCoreInstance("SingleAction",  RarityTier.Common),
                exotic: null,
                ammoInMagazine: 0);

            var ok = WeaponAssemblySystem.TryAssemble(config, null, out _, out var reason);

            Assert.IsFalse(ok);
            Assert.IsNotNull(reason);
            StringAssert.Contains("registry", reason.ToLowerInvariant());
        }

    }
}
