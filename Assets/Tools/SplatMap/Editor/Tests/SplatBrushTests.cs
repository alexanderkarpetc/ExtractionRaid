using NUnit.Framework;
using UnityEngine;

namespace ExtractionRaid.Editor.SplatMap.Tests
{
    public class SplatBrushTests
    {
        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void FullStrengthSelectsExactlyOneLayer(int channel)
        {
            Color result = SplatBrush.Blend(new Color(0.1f, 0.2f, 0.3f, 0.4f), channel, 1);
            for (int i = 0; i < 4; i++) Assert.That(result[i], Is.EqualTo(i == channel ? 1 : 0).Within(0.00001f));
        }

        [Test]
        public void PartialStrokePreservesOtherLayerRatiosAndTotalWeight()
        {
            Color result = SplatBrush.Blend(new Color(0.2f, 0.4f, 0.6f, 0.8f), 1, 0.5f);
            Assert.That(result.r, Is.EqualTo(0.05f).Within(0.00001f));
            Assert.That(result.g, Is.EqualTo(0.6f).Within(0.00001f));
            Assert.That(result.b, Is.EqualTo(0.15f).Within(0.00001f));
            Assert.That(result.a, Is.EqualTo(0.2f).Within(0.00001f));
            Assert.That(result.r + result.g + result.b + result.a, Is.EqualTo(1).Within(0.00001f));
        }

        [Test]
        public void EmptyMapStartsFromBaseIncludingAlphaLayerPainting()
        {
            Assert.That(SplatBrush.Blend(new Color(0, 0, 0, 0), 3, 0.25f), Is.EqualTo(new Color(0.75f, 0, 0, 0.25f)));
        }

        [Test]
        public void CornerStrokeDoesNotWrapToOppositeEdge()
        {
            var pixels = new Color[16 * 16];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color(1, 0, 0, 0);
            SplatBrush.Stamp(pixels, 16, Vector2.zero, 0.2f, 1, false, 2);
            Assert.That(pixels[0].b, Is.EqualTo(1));
            Assert.That(pixels[15], Is.EqualTo(new Color(1, 0, 0, 0)));
            Assert.That(pixels[255], Is.EqualTo(new Color(1, 0, 0, 0)));
        }

        [Test]
        public void SoftBrushHasFullCenterAndWeakerEdge()
        {
            var pixels = new Color[9 * 9];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color(1, 0, 0, 0);
            SplatBrush.Stamp(pixels, 9, new Vector2(0.5f, 0.5f), 3f / 9, 1, true, 1);
            Assert.That(pixels[4 * 9 + 4].g, Is.EqualTo(1));
            Assert.That(pixels[4 * 9 + 6].g, Is.GreaterThan(0).And.LessThan(1));
            Assert.That(pixels[4 * 9 + 7].g, Is.EqualTo(0));
        }
    }
}
