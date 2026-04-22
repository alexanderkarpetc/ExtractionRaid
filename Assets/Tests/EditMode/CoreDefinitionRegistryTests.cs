using System.Collections.Generic;
using Adapters;
using NUnit.Framework;
using State;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.EditMode
{
    [TestFixture]
    public class CoreDefinitionRegistryTests
    {
        CoreDefinitionDatabase _db;
        BallisticPayloadDefinition _ballistic;
        LaserPayloadDefinition     _laser;
        DeliveryCoreDefinition     _single;
        DeliveryCoreDefinition     _auto;
        ExoticModDefinition        _ricochet;

        [SetUp]
        public void SetUp()
        {
            _ballistic = MakePayload<BallisticPayloadDefinition>("BallisticRound");
            _laser     = MakePayload<LaserPayloadDefinition>("LaserCharge");
            _single    = MakeDelivery("SingleAction");
            _auto      = MakeDelivery("Auto");
            _ricochet  = MakeExotic("Ricochet");

            _db = ScriptableObject.CreateInstance<CoreDefinitionDatabase>();
            _db.SetEntries(
                new List<PayloadCoreDefinition>  { _ballistic, _laser },
                new List<DeliveryCoreDefinition> { _single, _auto },
                new List<ExoticModDefinition>    { _ricochet });
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_ballistic);
            Object.DestroyImmediate(_laser);
            Object.DestroyImmediate(_single);
            Object.DestroyImmediate(_auto);
            Object.DestroyImmediate(_ricochet);
            Object.DestroyImmediate(_db);
        }

        // ── Payload lookups ───────────────────────────────────

        [Test]
        public void GetPayload_ExistingId_ReturnsDefinition()
        {
            var registry = new DatabaseCoreDefinitionRegistry(_db);
            Assert.AreSame(_ballistic, registry.GetPayload("BallisticRound"));
            Assert.AreSame(_laser,     registry.GetPayload("LaserCharge"));
        }

        [Test]
        public void GetPayload_MissingId_Throws()
        {
            var registry = new DatabaseCoreDefinitionRegistry(_db);
            Assert.Throws<KeyNotFoundException>(() => registry.GetPayload("MicroRocket"));
        }

        [Test]
        public void TryGetPayload_ExistingId_ReturnsTrue()
        {
            var registry = new DatabaseCoreDefinitionRegistry(_db);
            Assert.IsTrue(registry.TryGetPayload("BallisticRound", out var def));
            Assert.AreSame(_ballistic, def);
        }

        [Test]
        public void TryGetPayload_MissingId_ReturnsFalseAndNull()
        {
            var registry = new DatabaseCoreDefinitionRegistry(_db);
            Assert.IsFalse(registry.TryGetPayload("Nonexistent", out var def));
            Assert.IsNull(def);
        }

        // ── Delivery lookups ──────────────────────────────────

        [Test]
        public void GetDelivery_ExistingId_ReturnsDefinition()
        {
            var registry = new DatabaseCoreDefinitionRegistry(_db);
            Assert.AreSame(_single, registry.GetDelivery("SingleAction"));
            Assert.AreSame(_auto,   registry.GetDelivery("Auto"));
        }

        [Test]
        public void GetDelivery_MissingId_Throws()
        {
            var registry = new DatabaseCoreDefinitionRegistry(_db);
            Assert.Throws<KeyNotFoundException>(() => registry.GetDelivery("Scatter"));
        }

        // ── Exotic lookups ────────────────────────────────────

        [Test]
        public void GetExotic_ExistingId_ReturnsDefinition()
        {
            var registry = new DatabaseCoreDefinitionRegistry(_db);
            Assert.AreSame(_ricochet, registry.GetExotic("Ricochet"));
        }

        [Test]
        public void TryGetExotic_MissingId_ReturnsFalse()
        {
            var registry = new DatabaseCoreDefinitionRegistry(_db);
            Assert.IsFalse(registry.TryGetExotic("SplitOnImpact", out var def));
            Assert.IsNull(def);
        }

        // ── List accessors (for Weapon Builder UI) ────────────

        [Test]
        public void AllPayloads_ReturnsAllRegisteredDefinitions()
        {
            var registry = new DatabaseCoreDefinitionRegistry(_db);
            var all = registry.AllPayloads;
            Assert.AreEqual(2, all.Count);
            Assert.Contains(_ballistic, (System.Collections.ICollection)all);
            Assert.Contains(_laser,     (System.Collections.ICollection)all);
        }

        [Test]
        public void AllDeliveries_ReturnsAllRegisteredDefinitions()
        {
            var registry = new DatabaseCoreDefinitionRegistry(_db);
            var all = registry.AllDeliveries;
            Assert.AreEqual(2, all.Count);
            Assert.Contains(_single, (System.Collections.ICollection)all);
            Assert.Contains(_auto,   (System.Collections.ICollection)all);
        }

        [Test]
        public void AllExotics_ReturnsAllRegisteredDefinitions()
        {
            var registry = new DatabaseCoreDefinitionRegistry(_db);
            var all = registry.AllExotics;
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

        // ── Duplicate handling ────────────────────────────────

        [Test]
        public void DuplicatePayloadIds_LogWarningAndLastWins()
        {
            var duplicate = MakePayload<BallisticPayloadDefinition>("BallisticRound");
            try
            {
                _db.SetEntries(
                    new List<PayloadCoreDefinition>  { _ballistic, duplicate },
                    new List<DeliveryCoreDefinition> { _single },
                    new List<ExoticModDefinition>());

                LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("Duplicate payload id"));
                var registry = new DatabaseCoreDefinitionRegistry(_db);
                Assert.AreSame(duplicate, registry.GetPayload("BallisticRound"));
            }
            finally
            {
                Object.DestroyImmediate(duplicate);
            }
        }

        // ── Helpers ───────────────────────────────────────────

        static T MakePayload<T>(string id) where T : PayloadCoreDefinition
        {
            var def = ScriptableObject.CreateInstance<T>();
            SetPrivateField(def, "_id", id);
            return def;
        }

        static DeliveryCoreDefinition MakeDelivery(string id)
        {
            var def = ScriptableObject.CreateInstance<DeliveryCoreDefinition>();
            SetPrivateField(def, "_id", id);
            return def;
        }

        static ExoticModDefinition MakeExotic(string id)
        {
            var def = ScriptableObject.CreateInstance<ExoticModDefinition>();
            SetPrivateField(def, "_id", id);
            return def;
        }

        static void SetPrivateField(object obj, string fieldName, object value)
        {
            var field = obj.GetType().GetField(
                fieldName,
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            if (field == null && obj.GetType().BaseType != null)
                field = obj.GetType().BaseType.GetField(
                    fieldName,
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.NotNull(field, $"Field '{fieldName}' not found on {obj.GetType()}");
            field.SetValue(obj, value);
        }
    }
}
