using System.Collections.Generic;
using Adapters;
using NUnit.Framework;
using State;
using Tests.EditMode.Fakes;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.EditMode
{
    [TestFixture]
    public class CoreDefinitionRegistryTests
    {
        CoreDefinitionDatabase      _db;
        BallisticPayloadDefinition  _ballistic;
        LaserPayloadDefinition      _laser;
        DeliveryCoreDefinition      _single;
        DeliveryCoreDefinition      _auto;
        ExoticModDefinition         _ricochet;
        ICoreDefinitionRegistry     _registry;

        [SetUp]
        public void SetUp()
        {
            _ballistic = WeaponBuilderTestFactory.MakeBallistic("BallisticRound");
            _laser     = WeaponBuilderTestFactory.MakeLaser("LaserCharge");
            _single    = WeaponBuilderTestFactory.MakeDelivery("SingleAction");
            _auto      = WeaponBuilderTestFactory.MakeDelivery("Auto");
            _ricochet  = WeaponBuilderTestFactory.MakeExotic("Ricochet");

            _db = WeaponBuilderTestFactory.MakeDatabase(
                payloads:   new PayloadCoreDefinition[]  { _ballistic, _laser },
                deliveries: new DeliveryCoreDefinition[] { _single, _auto },
                exotics:    new[]                         { _ricochet });
            _registry = WeaponBuilderTestFactory.MakeRegistry(_db);
        }

        [TearDown]
        public void TearDown() =>
            WeaponBuilderTestFactory.DestroyAll(_ballistic, _laser, _single, _auto, _ricochet, _db);

        // ── Payload lookups ───────────────────────────────────

        [Test]
        public void GetPayload_ExistingId_ReturnsDefinition()
        {
            Assert.AreSame(_ballistic, _registry.GetPayload("BallisticRound"));
            Assert.AreSame(_laser,     _registry.GetPayload("LaserCharge"));
        }

        [Test]
        public void GetPayload_MissingId_Throws()
        {
            Assert.Throws<KeyNotFoundException>(() => _registry.GetPayload("MicroRocket"));
        }

        [Test]
        public void TryGetPayload_ExistingId_ReturnsTrue()
        {
            Assert.IsTrue(_registry.TryGetPayload("BallisticRound", out var def));
            Assert.AreSame(_ballistic, def);
        }

        [Test]
        public void TryGetPayload_MissingId_ReturnsFalseAndNull()
        {
            Assert.IsFalse(_registry.TryGetPayload("Nonexistent", out var def));
            Assert.IsNull(def);
        }

        // ── Delivery lookups ──────────────────────────────────

        [Test]
        public void GetDelivery_ExistingId_ReturnsDefinition()
        {
            Assert.AreSame(_single, _registry.GetDelivery("SingleAction"));
            Assert.AreSame(_auto,   _registry.GetDelivery("Auto"));
        }

        [Test]
        public void GetDelivery_MissingId_Throws()
        {
            Assert.Throws<KeyNotFoundException>(() => _registry.GetDelivery("Scatter"));
        }

        // ── Exotic lookups ────────────────────────────────────

        [Test]
        public void GetExotic_ExistingId_ReturnsDefinition()
        {
            Assert.AreSame(_ricochet, _registry.GetExotic("Ricochet"));
        }

        [Test]
        public void TryGetExotic_MissingId_ReturnsFalse()
        {
            Assert.IsFalse(_registry.TryGetExotic("SplitOnImpact", out var def));
            Assert.IsNull(def);
        }

        // ── List accessors (for Weapon Builder UI) ────────────

        [Test]
        public void AllPayloads_ReturnsAllRegisteredDefinitions()
        {
            var all = _registry.AllPayloads;
            Assert.AreEqual(2, all.Count);
            Assert.Contains(_ballistic, (System.Collections.ICollection)all);
            Assert.Contains(_laser,     (System.Collections.ICollection)all);
        }

        [Test]
        public void AllDeliveries_ReturnsAllRegisteredDefinitions()
        {
            var all = _registry.AllDeliveries;
            Assert.AreEqual(2, all.Count);
            Assert.Contains(_single, (System.Collections.ICollection)all);
            Assert.Contains(_auto,   (System.Collections.ICollection)all);
        }

        [Test]
        public void AllExotics_ReturnsAllRegisteredDefinitions()
        {
            var all = _registry.AllExotics;
            Assert.AreEqual(1, all.Count);
            Assert.AreSame(_ricochet, all[0]);
        }

        // ── Null safety ───────────────────────────────────────

        [Test]
        public void Constructor_NullDatabase_Throws()
        {
            Assert.Throws<System.ArgumentNullException>(
                () => new DatabaseCoreDefinitionRegistry(null));
        }

        // ── Duplicate handling (parameterized across all 3 categories) ──

        public enum DupKind { Payload, Delivery, Exotic }

        [TestCase(DupKind.Payload,  "Duplicate payload id")]
        [TestCase(DupKind.Delivery, "Duplicate delivery id")]
        [TestCase(DupKind.Exotic,   "Duplicate exotic id")]
        public void DuplicateIds_LogWarningAndLastWins(DupKind kind, string expectedWarning)
        {
            // Build a fresh DB per case — register one original + one duplicate with the
            // same id, then assert: warning logged, index resolves to the last occurrence.
            ScriptableObject dup = null;
            var payloads   = new List<PayloadCoreDefinition>();
            var deliveries = new List<DeliveryCoreDefinition>();
            var exotics    = new List<ExoticModDefinition>();
            try
            {
                switch (kind)
                {
                    case DupKind.Payload:
                        var dupPayload = WeaponBuilderTestFactory.MakeBallistic("BallisticRound");
                        dup = dupPayload;
                        payloads.Add(_ballistic);
                        payloads.Add(dupPayload);
                        break;
                    case DupKind.Delivery:
                        var dupDelivery = WeaponBuilderTestFactory.MakeDelivery("SingleAction");
                        dup = dupDelivery;
                        deliveries.Add(_single);
                        deliveries.Add(dupDelivery);
                        break;
                    case DupKind.Exotic:
                        var dupExotic = WeaponBuilderTestFactory.MakeExotic("Ricochet");
                        dup = dupExotic;
                        exotics.Add(_ricochet);
                        exotics.Add(dupExotic);
                        break;
                }

                _db.SetEntries(payloads, deliveries, exotics);

                LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex(expectedWarning));
                var registry = new DatabaseCoreDefinitionRegistry(_db);

                switch (kind)
                {
                    case DupKind.Payload:
                        Assert.AreSame(dup, registry.GetPayload("BallisticRound"));
                        break;
                    case DupKind.Delivery:
                        Assert.AreSame(dup, registry.GetDelivery("SingleAction"));
                        break;
                    case DupKind.Exotic:
                        Assert.AreSame(dup, registry.GetExotic("Ricochet"));
                        break;
                }
            }
            finally
            {
                if (dup != null) Object.DestroyImmediate(dup);
            }
        }
    }
}
