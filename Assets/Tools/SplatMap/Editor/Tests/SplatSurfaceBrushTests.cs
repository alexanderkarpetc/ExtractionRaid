using NUnit.Framework;
using UnityEngine;

namespace ExtractionRaid.Editor.SplatMap.Tests
{
    public class SplatSurfaceBrushTests
    {
        static readonly Vector2[] QuadUV = { Vector2.zero, Vector2.right, Vector2.up, Vector2.one };
        static readonly int[] QuadIndices = { 0, 2, 1, 1, 2, 3 };

        static Vector3[] Quad(float width, float length, float shear = 0) => new[]
        {
            new Vector3(-width / 2 - shear / 2, 0, -length / 2), new Vector3(width / 2 - shear / 2, 0, -length / 2),
            new Vector3(-width / 2 + shear / 2, 0, length / 2), new Vector3(width / 2 + shear / 2, 0, length / 2)
        };
        static Color[] Pixels(int size, bool mask = false)
        {
            var pixels = new Color[size * size];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = mask ? Color.white : new Color(1, 0, 0, 0);
            return pixels;
        }

        [TestCase(128, 32, false)]
        [TestCase(32, 128, false)]
        [TestCase(128, 32, true)]
        [TestCase(32, 128, true)]
        public void RectangularMapMatchesWorldSpaceFootprint(int width, int height, bool mask)
        {
            var vertices = new[] { Vector3.zero, new Vector3(4, 0, 0), new Vector3(0, 0, 4), new Vector3(4, 0, 4) };
            var uv = new[] { Vector2.zero, Vector2.right, Vector2.up, Vector2.one };
            var brush = new SplatSurfaceBrush(vertices, uv, new[] { 0, 2, 1, 1, 2, 3 }, width, height);
            var pixels = new Color[width * height];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = mask ? Color.white : new Color(1, 0, 0, 0);
            var center = new Vector3(3.7f, 0, 2.3f);
            Assert.That(brush.Stamp(pixels, center, Vector3.up, 0.8f, 1, true, 1, mask, 0), Is.True);
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                var world = new Vector3((x + 0.5f) / width * 4, 0, (y + 0.5f) / height * 4);
                float amount = 1 - Mathf.SmoothStep(0, 1, Vector3.Distance(world, center) / 0.8f);
                float actual = mask ? 1 - pixels[y * width + x].r : pixels[y * width + x].g;
                Assert.That(actual, Is.EqualTo(amount).Within(0.00001f), $"Texel {x},{y}");
            }
        }
        [TestCase(false)]
        [TestCase(true)]
        public void LongScaledRoadHasEqualFalloffAtEqualWorldDistances(bool mask)
        {
            const int size = 201;
            var brush = new SplatSurfaceBrush(Quad(4, 40), QuadUV, QuadIndices, size);
            Color[] pixels = Pixels(size, mask);
            Assert.That(brush.Stamp(pixels, Vector3.zero, Vector3.up, 1, 1, true, 1, mask, 0), Is.True);
            float across = mask ? 1 - pixels[100 * size + 140].r : pixels[100 * size + 140].g;
            float along = mask ? 1 - pixels[104 * size + 100].r : pixels[104 * size + 100].g;
            Assert.That(across, Is.GreaterThan(0));
            Assert.That(along, Is.EqualTo(across).Within(0.0001f));
            Assert.That(pixels[100 * size + 160], Is.EqualTo(mask ? Color.white : new Color(1, 0, 0, 0)));
            Assert.That(pixels[106 * size + 100], Is.EqualTo(mask ? Color.white : new Color(1, 0, 0, 0)));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ShearedMeshAndRotatedUVKeepCircularWorldFootprint(bool rotateUV)
        {
            const int size = 101;
            Vector2[] uv = rotateUV ? new[] { Vector2.zero, Vector2.up, Vector2.right, Vector2.one } : QuadUV;
            var brush = new SplatSurfaceBrush(Quad(4, 8, 3), uv, QuadIndices, size);
            Color[] pixels = Pixels(size);
            brush.Stamp(pixels, Vector3.zero, Vector3.up, 1, 1, false, 1);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float u = ((rotateUV ? y : x) + 0.5f) / size - 0.5f;
                float v = ((rotateUV ? x : y) + 0.5f) / size - 0.5f;
                float distanceSquared = (u * 4 + v * 3) * (u * 4 + v * 3) + (v * 8) * (v * 8);
                if (Mathf.Abs(distanceSquared - 1) < 0.001f) continue;
                Assert.That(pixels[y * size + x].g, Is.EqualTo(distanceSquared < 1 ? 1 : 0), $"Texel {x}, {y}");
            }
        }

        [Test]
        public void SharedTriangleEdgeDoesNotDoubleStrengthAndScratchClearsBetweenStamps()
        {
            const int size = 101;
            var brush = new SplatSurfaceBrush(Quad(4, 4), QuadUV, QuadIndices, size);
            Color[] pixels = Pixels(size);
            brush.Stamp(pixels, Vector3.zero, Vector3.up, 1, 0.5f, false, 1);
            Assert.That(pixels[50 * size + 50].g, Is.EqualTo(0.5f).Within(0.00001f));
            brush.Stamp(pixels, Vector3.zero, Vector3.up, 1, 0.5f, false, 1);
            Assert.That(pixels[50 * size + 50].g, Is.EqualTo(0.75f).Within(0.00001f));
        }

        [Test]
        public void BrushCrossesUVSeamWithoutPaintingEmptyAtlasGap()
        {
            const int size = 200;
            var vertices = new[]
            {
                new Vector3(-1,0,-1), new Vector3(0,0,-1), new Vector3(-1,0,1), new Vector3(0,0,1),
                new Vector3(0,0,-1), new Vector3(1,0,-1), new Vector3(0,0,1), new Vector3(1,0,1)
            };
            var uv = new[] { new Vector2(0,0), new Vector2(0.45f,0), new Vector2(0,1), new Vector2(0.45f,1),
                new Vector2(0.55f,0), new Vector2(1,0), new Vector2(0.55f,1), new Vector2(1,1) };
            var indices = new[] { 0,2,1, 1,2,3, 4,6,5, 5,6,7 };
            var brush = new SplatSurfaceBrush(vertices, uv, indices, size);
            Color[] pixels = Pixels(size);
            brush.Stamp(pixels, Vector3.zero, Vector3.up, 0.25f, 1, false, 1);
            Assert.That(pixels[100 * size + 89].g, Is.EqualTo(1));
            Assert.That(pixels[100 * size + 110].g, Is.EqualTo(1));
            Assert.That(pixels[100 * size + 100].g, Is.Zero);
        }

        [Test]
        public void OppositeFacingSurfaceIsNotPainted()
        {
            var brush = new SplatSurfaceBrush(Quad(4, 4), QuadUV, new[] { 0,1,2, 1,3,2 }, 21);
            Color[] pixels = Pixels(21);
            Assert.That(brush.Stamp(pixels, Vector3.zero, Vector3.up, 1, 1, false, 1), Is.False);
            Assert.That(pixels[10 * 21 + 10].g, Is.Zero);
        }
    }
}
