using NUnit.Framework;
using Systems;

namespace Tests.EditMode
{
    [TestFixture]
    public class WeaponArchetypeFlavorTests
    {
        [Test]
        public void For_BallisticPistol_ReturnsReliableSidearm()
        {
            Assert.AreEqual(
                "Reliable single-shot sidearm",
                WeaponArchetypeFlavor.For("BallisticRound", "SingleAction"));
        }

        [Test]
        public void For_BallisticRifle_ReturnsVersatile()
        {
            Assert.AreEqual(
                "Versatile sustained-fire rifle",
                WeaponArchetypeFlavor.For("BallisticRound", "Auto"));
        }

        [Test]
        public void For_BallisticShotgun_ReturnsCloseRange()
        {
            Assert.AreEqual(
                "Close-range pellet burst",
                WeaponArchetypeFlavor.For("BallisticRound", "Scatter"));
        }

        [Test]
        public void For_LaserPistol_MentionsCharge()
        {
            var flavor = WeaponArchetypeFlavor.For("LaserCharge", "SingleAction");
            StringAssert.Contains("charged", flavor.ToLowerInvariant());
        }

        [Test]
        public void For_LaserRifle_MentionsCharge()
        {
            var flavor = WeaponArchetypeFlavor.For("LaserCharge", "Auto");
            StringAssert.Contains("charged", flavor.ToLowerInvariant());
        }

        [Test]
        public void For_LaserShotgun_MentionsCharge()
        {
            var flavor = WeaponArchetypeFlavor.For("LaserCharge", "Scatter");
            StringAssert.Contains("charged", flavor.ToLowerInvariant());
        }

        [Test]
        public void For_UnmappedCombination_ReturnsEmpty()
        {
            Assert.AreEqual(string.Empty,
                WeaponArchetypeFlavor.For("Foam", "Rotary"));
        }

        [Test]
        public void For_NullOrEmptyId_ReturnsEmpty()
        {
            Assert.AreEqual(string.Empty, WeaponArchetypeFlavor.For(null, "SingleAction"));
            Assert.AreEqual(string.Empty, WeaponArchetypeFlavor.For("BallisticRound", null));
            Assert.AreEqual(string.Empty, WeaponArchetypeFlavor.For("", ""));
        }
    }
}
