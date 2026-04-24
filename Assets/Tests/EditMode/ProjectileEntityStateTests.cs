using NUnit.Framework;
using State;

namespace Tests.EditMode
{
    [TestFixture]
    public class ProjectileEntityStateTests
    {
        [Test]
        public void Create_WithPenetration_CarriesValues()
        {
            var proj = ProjectileEntityState.Create(
                new EId(1), new EId(2), default, default,
                10f, 0f, 3f, 25f,
                penetration: 45f, armorDamage: 12f);

            Assert.AreEqual(45f, proj.Penetration, 0.001f);
            Assert.AreEqual(12f, proj.ArmorDamage, 0.001f);
        }
    }
}
