using NUnit.Framework;
using State;

namespace Tests.EditMode
{
    /// <summary>
    /// A2 — string → typed key mapping для payload archetype. Verifies single-source-of-truth
    /// consistency з <c>PayloadCoreDefinition.Archetype</c> contract.
    /// </summary>
    [TestFixture]
    public class PayloadArchetypeKeyTests
    {
        [Test]
        public void FromArchetypeString_Laser_MapsToLaser()
            => Assert.AreEqual(PayloadArchetypeKey.Laser,
                PayloadArchetypeKeyExt.FromArchetypeString("Laser"));

        [Test]
        public void FromArchetypeString_Ballistic_MapsToBallistic()
            => Assert.AreEqual(PayloadArchetypeKey.Ballistic,
                PayloadArchetypeKeyExt.FromArchetypeString("Ballistic"));

        [Test]
        public void FromArchetypeString_Null_MapsToBallistic()
            => Assert.AreEqual(PayloadArchetypeKey.Ballistic,
                PayloadArchetypeKeyExt.FromArchetypeString(null));

        [Test]
        public void FromArchetypeString_Unknown_FallsBackToBallistic()
            => Assert.AreEqual(PayloadArchetypeKey.Ballistic,
                PayloadArchetypeKeyExt.FromArchetypeString("Plasma"));
    }
}
