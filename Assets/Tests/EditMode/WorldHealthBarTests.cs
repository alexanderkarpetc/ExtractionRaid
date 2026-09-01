using NUnit.Framework;
using View;

namespace Tests.EditMode
{
    public class WorldHealthBarTests
    {
        [TestCase(73.2f, 118f, "74 / 118")]
        [TestCase(0.1f, 100f, "1 / 100")]
        [TestCase(-5f, 100f, "0 / 100")]
        [TestCase(120f, 100f, "100 / 100")]
        [TestCase(10f, 0f, "0 / 0")]
        public void FormatHpText_ClampsAndRoundsUp(float current, float max, string expected)
        {
            Assert.That(WorldHealthBar.FormatHpText(current, max), Is.EqualTo(expected));
        }
    }
}
