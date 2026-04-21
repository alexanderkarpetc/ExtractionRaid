using System.Reflection;
using NUnit.Framework;
using State;
using UnityEngine;

namespace Tests.EditMode
{
    [TestFixture]
    public class WeaponArchetypeLabelTests
    {
        // ── Happy path ────────────────────────────────────────

        [Test]
        public void Compose_BallisticPistol_ReturnsTemplate()
        {
            var payload  = MakePayload(displayName: "Ballistic");
            var delivery = MakeDelivery(formFactor: "Pistol");
            try
            {
                Assert.AreEqual("Ballistic Pistol", WeaponArchetypeLabel.Compose(payload, delivery));
            }
            finally { Cleanup(payload, delivery); }
        }

        [Test]
        public void Compose_LaserRifle_ReturnsTemplate()
        {
            var payload  = MakePayload(displayName: "Laser");
            var delivery = MakeDelivery(formFactor: "Rifle");
            try
            {
                Assert.AreEqual("Laser Rifle", WeaponArchetypeLabel.Compose(payload, delivery));
            }
            finally { Cleanup(payload, delivery); }
        }

        [Test]
        public void Compose_FoamShotgun_ReturnsTemplate()
        {
            var payload  = MakePayload(displayName: "Foam");
            var delivery = MakeDelivery(formFactor: "Shotgun");
            try
            {
                Assert.AreEqual("Foam Shotgun", WeaponArchetypeLabel.Compose(payload, delivery));
            }
            finally { Cleanup(payload, delivery); }
        }

        // ── Null / empty guards ───────────────────────────────

        [Test]
        public void Compose_NullPayload_ReturnsFormFactorOnly()
        {
            var delivery = MakeDelivery(formFactor: "Pistol");
            try
            {
                Assert.AreEqual("Pistol", WeaponArchetypeLabel.Compose(null, delivery));
            }
            finally { Object.DestroyImmediate(delivery); }
        }

        [Test]
        public void Compose_NullDelivery_ReturnsDisplayNameOnly()
        {
            var payload = MakePayload(displayName: "Ballistic");
            try
            {
                Assert.AreEqual("Ballistic", WeaponArchetypeLabel.Compose(payload, null));
            }
            finally { Object.DestroyImmediate(payload); }
        }

        [Test]
        public void Compose_BothNull_ReturnsEmpty()
        {
            Assert.AreEqual(string.Empty, WeaponArchetypeLabel.Compose(null, null));
        }

        [Test]
        public void Compose_EmptyDisplayName_ReturnsFormFactorOnly()
        {
            var payload  = MakePayload(displayName: "");
            var delivery = MakeDelivery(formFactor: "Pistol");
            try
            {
                Assert.AreEqual("Pistol", WeaponArchetypeLabel.Compose(payload, delivery));
            }
            finally { Cleanup(payload, delivery); }
        }

        [Test]
        public void Compose_EmptyFormFactor_ReturnsDisplayNameOnly()
        {
            var payload  = MakePayload(displayName: "Ballistic");
            var delivery = MakeDelivery(formFactor: "");
            try
            {
                Assert.AreEqual("Ballistic", WeaponArchetypeLabel.Compose(payload, delivery));
            }
            finally { Cleanup(payload, delivery); }
        }

        [Test]
        public void Compose_BothEmpty_ReturnsEmpty()
        {
            var payload  = MakePayload(displayName: "");
            var delivery = MakeDelivery(formFactor: "");
            try
            {
                Assert.AreEqual(string.Empty, WeaponArchetypeLabel.Compose(payload, delivery));
            }
            finally { Cleanup(payload, delivery); }
        }

        // ── Helpers ───────────────────────────────────────────

        static BallisticPayloadDefinition MakePayload(string displayName)
        {
            var def = ScriptableObject.CreateInstance<BallisticPayloadDefinition>();
            SetPrivateField(def, "_displayName", displayName);
            return def;
        }

        static DeliveryCoreDefinition MakeDelivery(string formFactor)
        {
            var def = ScriptableObject.CreateInstance<DeliveryCoreDefinition>();
            SetPrivateField(def, "_formFactor", formFactor);
            return def;
        }

        static void Cleanup(Object a, Object b)
        {
            Object.DestroyImmediate(a);
            Object.DestroyImmediate(b);
        }

        static void SetPrivateField(object target, string fieldName, object value)
        {
            var type = target.GetType();
            while (type != null)
            {
                var field = type.GetField(
                    fieldName,
                    BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                if (field != null)
                {
                    field.SetValue(target, value);
                    return;
                }
                type = type.BaseType;
            }
            Assert.Fail($"Field '{fieldName}' not found on {target.GetType()}.");
        }
    }
}
