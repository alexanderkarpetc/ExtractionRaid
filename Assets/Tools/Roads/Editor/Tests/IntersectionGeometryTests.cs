using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace ExtractionRaid.Editor.Roads.Tests
{
    public class IntersectionGeometryTests
    {
        [TestCase(3)]
        [TestCase(4)]
        public void EdgeMaskKeepsRoadMouthCentersVisible(int count)
        {
            var mouths = Mouths(count);
            var data = IntersectionGeometry.Build(mouths, Vector3.zero, 1, 8, 4, 2);
            var mask = IntersectionGeometry.InitialMask(data, 97, 0.5f);
            Vector3 min = data.vertices[0], max = min;
            foreach (var p in data.vertices) { min = Vector3.Min(min, p); max = Vector3.Max(max, p); }
            foreach (var mouth in mouths)
            {
                var center = (mouth[0] + mouth[mouth.Length - 1]) * 0.5f;
                int x = Mathf.RoundToInt((center.x - min.x) / (max.x - min.x) * 96);
                int y = Mathf.RoundToInt((center.z - min.z) / (max.z - min.z) * 96);
                Assert.That(mask[y * 97 + x], Is.EqualTo(Color.white));
            }
            Assert.That(Array.Exists(mask, p => p.r < 0.1f), Is.True);
        }

        [Test]
        public void EdgeFadeUsesWorldDistanceAndLeavesInteriorOpaque()
        {
            var data = IntersectionGeometry.Build(Mouths(3), Vector3.zero, 1, 8, 4, 2);
            var mask = IntersectionGeometry.InitialMask(data, 97, 0.5f);
            // T bounds: X [-6,6], Z [-2,6]. Exposed southern edge is Z=-2.
            Assert.That(mask[48].r, Is.Zero);
            Assert.That(mask[3 * 97 + 48].r, Is.EqualTo(0.5f).Within(0.0001f));
            Assert.That(mask[6 * 97 + 48], Is.EqualTo(Color.white));
            Assert.That(mask[24 * 97 + 48], Is.EqualTo(Color.white));
        }

        [Test]
        public void EdgeMaskIsIndependentOfWorldPosition()
        {
            var data = IntersectionGeometry.Build(Mouths(4), Vector3.zero, 0.75f, 8, 4, 2);
            var before = IntersectionGeometry.InitialMask(data, 65, 0.75f);
            for (int i = 0; i < data.vertices.Length; i++) data.vertices[i] += new Vector3(30, 20, -40);
            for (int i = 0; i < data.exposedEdgePairs.Length; i++) data.exposedEdgePairs[i] += new Vector2(30, -40);
            var after = IntersectionGeometry.InitialMask(data, 65, 0.75f);
            for (int i = 0; i < before.Length; i++)
                Assert.That(after[i].r, Is.EqualTo(before[i].r).Within(0.00002f));
        }

        [Test]
        public void ZeroFadeIsWhiteAndInvalidFadeIsRejected()
        {
            var data = IntersectionGeometry.Build(Mouths(3), Vector3.zero, 1, 8, 4, 2);
            foreach (var pixel in IntersectionGeometry.InitialMask(data, 33, 0)) Assert.That(pixel, Is.EqualTo(Color.white));
            Assert.Throws<ArgumentException>(() => IntersectionGeometry.InitialMask(data, 33, -1));
            Assert.Throws<ArgumentException>(() => IntersectionGeometry.InitialMask(data, 33, float.NaN));
        }
        [Test]
        public void MixedStartEndApproachesWorkAfterRotationAndTranslation()
        {
            var mouths = new List<Vector3[]>();
            var center = new Vector3(123, 4, -52);
            var directions = new[] { new Vector3(0.8f, 0, 0.6f), new Vector3(-0.6f, 0, 0.8f), new Vector3(-0.8f, 0, -0.6f) };
            for (int i = 0; i < directions.Length; i++)
            {
                bool start = i != 1;
                var points = start ? new[] { center, center + directions[i] * 20 } : new[] { center + directions[i] * 20, center };
                var full = RoadGeometry.Build(points, 4, 0.5f, false, 4, 2, 0);
                IntersectionGeometry.Trim(full, 5, start, 6, out var mouth);
                mouths.Add(mouth);
            }
            var data = IntersectionGeometry.Build(mouths, center, 0.75f, 8, 4, 2);
            foreach (var mouth in mouths) foreach (var p in mouth) CollectionAssert.Contains(data.vertices, p);
            for (int i = 0; i < data.triangles.Length; i += 3)
            {
                var a = data.vertices[data.triangles[i]];
                var b = data.vertices[data.triangles[i + 1]];
                var c = data.vertices[data.triangles[i + 2]];
                Assert.That(Vector3.Cross(b - a, c - a).y, Is.GreaterThan(0));
            }
        }
        [TestCase(3, 0f)]
        [TestCase(3, 1f)]
        [TestCase(4, 0f)]
        [TestCase(4, 1f)]
        public void JunctionMatchesEveryMouthVertexAndHasUpwardNondegenerateTriangles(int count, float rounding)
        {
            var mouths = Mouths(count);
            var node = IntersectionGeometry.Build(mouths, Vector3.zero, rounding, 8, 4, 2);
            foreach (var mouth in mouths)
            foreach (var vertex in mouth)
                CollectionAssert.Contains(node.vertices, vertex);
            for (int i = 0; i < node.triangles.Length; i += 3)
            {
                var a = node.vertices[node.triangles[i]];
                var b = node.vertices[node.triangles[i + 1]];
                var c = node.vertices[node.triangles[i + 2]];
                Assert.That(Vector3.Cross(b - a, c - a).y, Is.GreaterThan(0.000001f));
            }
            foreach (var uv in node.paintUV)
            {
                Assert.That(uv.x, Is.InRange(0, 1));
                Assert.That(uv.y, Is.InRange(0, 1));
            }
            // A manifold disk: boundary edges appear once, all interior edges twice.
            var edges = new Dictionary<string, int>();
            for (int i = 0; i < node.triangles.Length; i += 3)
            for (int j = 0; j < 3; j++)
            {
                int a = node.triangles[i + j], b = node.triangles[i + (j + 1) % 3];
                string key = Math.Min(a, b) + ":" + Math.Max(a, b);
                edges.TryGetValue(key, out int uses); edges[key] = uses + 1;
            }
            foreach (var uses in edges.Values) Assert.That(uses, Is.InRange(1, 2));
        }

        [TestCase(true)]
        [TestCase(false)]
        public void TrimPreservesOriginalUVAndDoesNotChangeSource(bool start)
        {
            var full = RoadGeometry.Build(new[] { Vector3.zero, Vector3.forward * 20 }, 4, 1, false, 4, 2, 0);
            var before = (Vector3[])full.vertices.Clone();
            var clipped = IntersectionGeometry.Trim(full, 5, start, 5, out var mouth);
            Assert.That(clipped.vertices.Length, Is.EqualTo(16 * 5));
            Assert.That(mouth[0].z, Is.EqualTo(start ? 5 : 15));
            int offset = start ? 25 : 0;
            for (int i = 0; i < clipped.vertices.Length; i++)
            {
                Assert.That(clipped.vertices[i], Is.EqualTo(full.vertices[offset + i]));
                Assert.That(clipped.paintUV[i], Is.EqualTo(full.paintUV[offset + i]));
                Assert.That(clipped.detailUV[i], Is.EqualTo(full.detailUV[offset + i]));
            }
            CollectionAssert.AreEqual(before, full.vertices);
        }

        [Test]
        public void GroundedJunctionPreservesSeamsAndProjectsInterior()
        {
            var mouths = Mouths(4);
            foreach (var mouth in mouths)
            for (int i = 0; i < mouth.Length; i++)
            {
                Slope(mouth[i], out var p);
                mouth[i] = p + Vector3.up * 0.02f;
            }
            var node = IntersectionGeometry.Build(mouths, Vector3.zero, 1, 8, 4, 2, Slope, 0.02f);
            foreach (var p in node.vertices)
                Assert.That(p.y, Is.EqualTo(p.x * 0.1f + p.z * 0.2f + 0.02f).Within(0.0001f));
        }

        [Test]
        public void InvalidApproachesFailBeforeProducingMeshes()
        {
            var full = RoadGeometry.Build(new[] { Vector3.zero, Vector3.forward * 4 }, 4, 1, false, 4, 2, 0);
            Assert.Throws<ArgumentException>(() => IntersectionGeometry.Trim(full, 5, true, 4, out _));
            Assert.Throws<ArgumentException>(() => IntersectionGeometry.Trim(full, 5, false, float.NaN, out _));
            var mouths = Mouths(4);
            mouths[1] = mouths[0];
            Assert.Throws<ArgumentException>(() => IntersectionGeometry.Build(mouths, Vector3.zero, 1, 8, 4, 2));
            Assert.Throws<InvalidOperationException>(() => IntersectionGeometry.Build(Mouths(3), Vector3.zero, 1, 8, 4, 2, Missing));
        }

        [Test]
        public void RoundingChangesCornerWithoutChangingMouths()
        {
            var mouths = Mouths(4);
            var square = IntersectionGeometry.Build(mouths, Vector3.zero, 0, 8, 4, 2);
            var rounded = IntersectionGeometry.Build(mouths, Vector3.zero, 1, 8, 4, 2);
            Assert.That(square.vertices.Length, Is.EqualTo(rounded.vertices.Length));
            float difference = 0;
            for (int i = 0; i < square.vertices.Length; i++)
                difference += Vector3.Distance(square.vertices[i], rounded.vertices[i]);
            Assert.That(difference, Is.GreaterThan(1));
            foreach (var mouth in mouths) foreach (var p in mouth) CollectionAssert.Contains(rounded.vertices, p);
        }

        static List<Vector3[]> Mouths(int count)
        {
            var result = new List<Vector3[]>();
            var directions = new[] { Vector3.right, Vector3.forward, Vector3.left, Vector3.back };
            for (int i = 0; i < count; i++)
            {
                var full = RoadGeometry.Build(new[] { Vector3.zero, directions[i] * 20 }, i == 1 ? 5 : 4, 0.5f, false, 4, 2, 0);
                IntersectionGeometry.Trim(full, 5, true, 6, out var mouth);
                result.Add(mouth);
            }
            return result;
        }
        static bool Slope(Vector3 point, out Vector3 projected)
        { projected = new Vector3(point.x, point.x * 0.1f + point.z * 0.2f, point.z); return true; }
        static bool Missing(Vector3 point, out Vector3 projected) { projected = point; return false; }
    }
}