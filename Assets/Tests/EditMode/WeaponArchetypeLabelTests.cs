using NUnit.Framework;
using State;
using Tests.EditMode.Fakes;
using UnityEngine;

namespace Tests.EditMode
{
    [TestFixture]
    public class WeaponArchetypeLabelTests
    {
        // ── Happy path ────────────────────────────────────────

        // Template: "{payload.DisplayName} {delivery.FormFactor}" — D8.
        [TestCase("Ballistic", "Pistol",  ExpectedResult = "Ballistic Pistol")]
        [TestCase("Laser",     "Rifle",   ExpectedResult = "Laser Rifle")]
        [TestCase("Foam",      "Shotgun", ExpectedResult = "Foam Shotgun")]
        public string Compose_PayloadAndDelivery_ReturnsTemplate(string displayName, string formFactor)
        {
            var payload  = MakePayload(displayName: displayName);
            var delivery = MakeDelivery(formFactor: formFactor);
            try   { return WeaponArchetypeLabel.Compose(payload, delivery); }
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
            => WeaponBuilderTestFactory.MakeBallistic(displayName: displayName);

        static DeliveryCoreDefinition MakeDelivery(string formFactor)
            => WeaponBuilderTestFactory.MakeDelivery(formFactor: formFactor);

        static void Cleanup(Object a, Object b) => WeaponBuilderTestFactory.DestroyAll(a, b);
    }
}
