using System;
using NUnit.Framework;
using UnityEngine;

namespace ExtractionRaid.Editor.Roads.Tests
{
    public class RoadGeometryTests
    {
        static Vector3[] Straight(float length = 10) => new[] { Vector3.zero, new Vector3(0, 0, length) };

        [TestCase(-1.9f)]
        [TestCase(1.9f)]
        public void OffsetFollowsCurvedRoadEdgeWithoutMovingPoints(float offset)
        {
            var points = new[] { Vector3.zero, new Vector3(2, 0, 5), new Vector3(7, 0, 9) };
            var original = (Vector3[])points.Clone();
            var road = RoadGeometry.Build(points, 4, 0.5f, true, 2, 2, 0);
            var marking = RoadGeometry.Build(points, 0.2f, 0.5f, true, 2, 2, 0, lateralOffset: offset);
            int edge = offset > 0 ? 2 : 0;
            for (int row = 0; row < road.vertices.Length / 3; row++)
                Assert.That(Vector3.Distance(road.vertices[row * 3 + edge], marking.vertices[row * 3 + edge]), Is.LessThan(0.00001f));
            CollectionAssert.AreEqual(original, points);
        }

        [Test]
        public void OffsetVerticesAreProjectedAtTheirNewPosition()
        {
            var road = RoadGeometry.Build(Straight(), 0.2f, 1, false, 2, 2, 0.02f, Slope, 1.9f);
            foreach (var p in road.vertices)
            {
                Assert.That(p.x, Is.InRange(1.7999f, 2.0001f));
                Assert.That(p.y, Is.EqualTo(p.x * 0.2f + p.z * 0.1f + 0.02f).Within(0.0001f));
            }
            Assert.Throws<InvalidOperationException>(() => RoadGeometry.Build(Straight(), 0.2f, 1, false, 2, 2, 0, NarrowGround, 2));
            Assert.Throws<ArgumentException>(() => RoadGeometry.Build(Straight(), 1, 1, false, 2, 2, 0, lateralOffset: float.NaN));
        }
        [Test]
        public void MarkingUVFitsWidthAndRepeatsAlongLength()
        {
            var data = RoadGeometry.Build(new[] { Vector3.zero, Vector3.forward * 12 }, 4, 1, false, 4, 3, 0);
            Assert.That(data.markingUV[0], Is.EqualTo(Vector2.zero));
            Assert.That(data.markingUV[4], Is.EqualTo(new Vector2(1, 0)));
            Assert.That(data.markingUV[data.markingUV.Length - 1], Is.EqualTo(new Vector2(1, 4)));
            Assert.That(data.paintUV[data.paintUV.Length - 1], Is.EqualTo(Vector2.one));
        }
        [Test]
        public void StraightRoadHasRequestedWidthLengthAndUpwardWinding()
        {
            RoadMeshData road = RoadGeometry.Build(Straight(), 4, 1, true, 2, 2, 0.02f);
            Assert.That(road.length, Is.EqualTo(10).Within(0.0001f));
            Assert.That(road.vertices.Length, Is.EqualTo(33));
            Assert.That(road.vertices[0], Is.EqualTo(new Vector3(-2, 0.02f, 0)));
            Assert.That(road.vertices[2], Is.EqualTo(new Vector3(2, 0.02f, 0)));
            for (int i = 0; i < road.triangles.Length; i += 3)
            {
                Vector3 a = road.vertices[road.triangles[i]], b = road.vertices[road.triangles[i + 1]], c = road.vertices[road.triangles[i + 2]];
                Assert.That(Vector3.Cross(b - a, c - a).y, Is.GreaterThan(0));
            }
        }

        [Test]
        public void PaintUVIsUniqueAndDetailUVRepeatsAtWorldScale()
        {
            RoadMeshData road = RoadGeometry.Build(Straight(20), 4, 1, false, 2, 2, 0);
            Assert.That(road.paintUV[0], Is.EqualTo(Vector2.zero));
            Assert.That(road.paintUV[road.paintUV.Length - 1], Is.EqualTo(Vector2.one));
            Assert.That(road.detailUV[road.detailUV.Length - 1], Is.EqualTo(new Vector2(2, 10)));
            for (int row = 1; row < road.vertices.Length / 3; row++)
                Assert.That(road.paintUV[row * 3].y, Is.GreaterThan(road.paintUV[(row - 1) * 3].y));
            foreach (Vector2 uv in road.paintUV)
            { Assert.That(uv.x, Is.InRange(0, 1)); Assert.That(uv.y, Is.InRange(0, 1)); }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void SamplingPreservesEndpointsAndRespectsSpacing(bool smooth)
        {
            var points = new[] { Vector3.zero, new Vector3(2, 0, 3), new Vector3(3, 0, 12) };
            var path = RoadGeometry.SamplePath(points, 0.7f, smooth);
            Assert.That(path[0], Is.EqualTo(points[0]));
            Assert.That(path[path.Count - 1], Is.EqualTo(points[points.Length - 1]));
            for (int i = 1; i < path.Count; i++) Assert.That(Vector3.Distance(path[i - 1], path[i]), Is.LessThanOrEqualTo(0.7001f));
        }

        [Test]
        public void BothRoadEdgesFollowProjectedSurface()
        {
            RoadMeshData road = RoadGeometry.Build(Straight(), 4, 1, false, 4, 2, 0.02f, Slope);
            foreach (Vector3 p in road.vertices) Assert.That(p.y, Is.EqualTo(p.x * 0.2f + p.z * 0.1f + 0.02f).Within(0.0001f));
        }

        [Test]
        public void MissingGroundRejectsRoadInsteadOfLeavingFloatingGeometry()
        {
            Assert.Throws<InvalidOperationException>(() => RoadGeometry.Build(Straight(), 4, 1, false, 2, 2, 0, MissingGround));
        }

        [Test]
        public void MissingGroundAtEdgeRejectsRoadEvenWhenCenterHasGround()
        {
            Assert.Throws<InvalidOperationException>(() => RoadGeometry.Build(Straight(), 4, 1, false, 2, 2, 0, NarrowGround));
        }

        [Test]
        public void DuplicateAndVerticalOnlyControlPointsAreRejected()
        {
            Assert.Throws<ArgumentException>(() => RoadGeometry.SamplePath(new[] { Vector3.zero, Vector3.zero }, 1, true));
            Assert.Throws<ArgumentException>(() => RoadGeometry.SamplePath(new[] { Vector3.zero, Vector3.up }, 1, true));
        }

        [Test]
        public void ExcessiveSamplingAndNonFiniteInputsAreRejected()
        {
            Assert.Throws<ArgumentException>(() => RoadGeometry.SamplePath(Straight(10000), 0.1f, false));
            Assert.Throws<ArgumentException>(() => RoadGeometry.SamplePath(Straight(), float.NaN, false));
            Assert.Throws<ArgumentException>(() => RoadGeometry.SamplePath(new[] { Vector3.zero, new Vector3(float.PositiveInfinity, 0, 1) }, 1, false));
        }

        [Test]
        public void InitialMaskFadesSidesAndEndsButKeepsCenterVisible()
        {
            Color[] mask = RoadGeometry.InitialMask(9, 4, 10, 1, true);
            Assert.That(mask[4 * 9 + 4], Is.EqualTo(Color.white));
            Assert.That(mask[4 * 9].r, Is.Zero);
            Assert.That(mask[4].r, Is.Zero);
            Assert.That(mask[4 * 9 + 1].r, Is.EqualTo(0.5f).Within(0.0001f));
            Assert.That(mask[4 * 9 + 1].a, Is.EqualTo(1));
        }

        [Test]
        public void EndFadeCanBeDisabledWithoutDisablingSideFade()
        {
            Color[] mask = RoadGeometry.InitialMask(9, 4, 10, 1, false);
            Assert.That(mask[4], Is.EqualTo(Color.white));
            Assert.That(mask[0].r, Is.Zero);
        }

        [Test]
        public void ZeroFadeCreatesFullyVisibleMask()
        {
            foreach (Color pixel in RoadGeometry.InitialMask(9, 4, 10, 0, true)) Assert.That(pixel, Is.EqualTo(Color.white));
        }

        static bool Slope(Vector3 point, out Vector3 projected)
        { projected = new Vector3(point.x, point.x * 0.2f + point.z * 0.1f, point.z); return true; }
        static bool MissingGround(Vector3 point, out Vector3 projected)
        { projected = point; return false; }
        static bool NarrowGround(Vector3 point, out Vector3 projected)
        { projected = point; return Mathf.Abs(point.x) <= 1; }
    }
}
