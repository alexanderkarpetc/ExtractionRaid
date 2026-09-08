using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;

namespace LevelDesign.Editor.Tests
{
    public sealed class MapAnalysisTests
    {
        [Test]
        public void BoundsIndexMatchesBruteForceAndIncludesRadiusBoundary()
        {
            var bounds = new List<Bounds>();
            var random = new System.Random(33);
            for (int i = 0; i < 200; i++) bounds.Add(new Bounds(new Vector3(random.Next(-100, 100), 0,
                random.Next(-100, 100)), new Vector3(3, 5, 8)));
            var index = new BoundsIndex(bounds);
            for (int i = 0; i < 50; i++)
            {
                var point = new Vector3(random.Next(-100, 100), 100, random.Next(-100, 100));
                float nearest = float.PositiveInfinity; int count = 0;
                foreach (var item in bounds)
                {
                    float distance = Mathf.Sqrt(BoundsIndex.SquaredDistance(point, item));
                    nearest = Mathf.Min(nearest, distance); if (distance <= 20) count++;
                }
                Assert.That(index.Nearest(point), Is.EqualTo(nearest).Within(.0001f));
                Assert.That(index.CountWithin(point, 20), Is.EqualTo(count));
            }
            Assert.That(new BoundsIndex(new Bounds[0]).Nearest(Vector3.zero), Is.EqualTo(float.PositiveInfinity));
            Assert.That(new BoundsIndex(new[] { new Bounds(new Vector3(2, 0, 0), Vector3.zero) }).CountWithin(Vector3.zero, 2), Is.EqualTo(1));
        }
        [Test]
        public void InvalidSamplingAndThresholdsAreRejectedBeforeAllocation()
        {
            var settings = ScriptableObject.CreateInstance<MapAnalysisSettings>();
            try
            {
                settings.area.size = new Vector2(10000, 10000); settings.sampleSpacing = .1f;
                Assert.That(MapAnalyzer.Validate(settings), Is.Not.Null);
                settings.area.size = new Vector2(10, 10); settings.sampleSpacing = 2;
                Assert.That(MapAnalyzer.Estimate(settings), Is.EqualTo(25));
                settings.coverThresholds.bad = settings.coverThresholds.warning;
                Assert.That(MapAnalyzer.Validate(settings), Is.Not.Null);
                Assert.That(new DistanceThresholds().Classify(15), Is.EqualTo(2));
            }
            finally { Object.DestroyImmediate(settings); }
        }
        [Test]
        public void AnalyzerSamplesRealNavMeshAndMeasuresGeometryAndPOIRadius()
        {
            using var testScene = new ScatterTests.TestSceneScope();
            var settings = ScriptableObject.CreateInstance<MapAnalysisSettings>();
            var result = ScriptableObject.CreateInstance<MapAnalysisResult>();
            var cover = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var poi = new GameObject("Test POI");
            NavMeshData data = null; NavMeshDataInstance instance = default;
            try
            {
                cover.layer = 1; cover.transform.position = new Vector3(0, .5f, 0);
                var source = new NavMeshBuildSource { shape = NavMeshBuildSourceShape.Box,
                    size = new Vector3(30, 1, 30), transform = Matrix4x4.TRS(new Vector3(0, -.5f, 0), Quaternion.identity, Vector3.one), area = 0 };
                var build = NavMesh.GetSettingsByIndex(0);
                settings.agentTypeId = build.agentTypeID;
                data = NavMeshBuilder.BuildNavMeshData(build, new List<NavMeshBuildSource> { source },
                    new Bounds(Vector3.zero, new Vector3(40, 10, 40)), Vector3.zero, Quaternion.identity);
                Assert.That(data, Is.Not.Null); instance = NavMesh.AddNavMeshData(data);
                settings.area.size = new Vector2(10, 10); settings.sampleSpacing = 2;
                settings.interestingLayerMask = settings.coverLayerMask = settings.densityLayerMask = 2;
                var entry = new MapAnalysisPOI { radius = 2 }; entry.sceneObject.Set(poi); settings.pois.Add(entry);
                var routine = new MapAnalyzer().Analyze(settings, result, _ => { });
                while (routine.MoveNext()) { }
                Assert.That(result.complete, Is.True); Assert.That(result.samples.Count, Is.EqualTo(25));
                var center = result.samples.Find(sample => sample.gridX == 2 && sample.gridZ == 2);
                Assert.That(center.coverDistance, Is.EqualTo(0).Within(.01f));
                Assert.That(center.poiDistance, Is.EqualTo(0).Within(.01f));
                Assert.That(center.density, Is.EqualTo(1)); // Renderer + collider count once.
                Assert.That(center.directions, Is.EqualTo(4));
                var corner = result.samples[0];
                Assert.That(corner.poiDistance, Is.EqualTo(Mathf.Sqrt(32) - 2).Within(.1f));
                settings.cover = false;
                routine = new MapAnalyzer().Analyze(settings, result, _ => { });
                while (routine.MoveNext()) { }
                Assert.That(float.IsNaN(result.samples[0].coverDistance), Is.True);
                settings.interestingLayerMask = settings.coverLayerMask = settings.densityLayerMask = 0;
                settings.cover = true; settings.pois.Clear();
                routine = new MapAnalyzer().Analyze(settings, result, _ => { });
                while (routine.MoveNext()) { }
                Assert.That(result.clusters.Count, Is.EqualTo(1));
                Assert.That(result.clusters[0].count, Is.EqualTo(25));
                Assert.That(result.samples[0].flags.HasFlag(AnalysisFlags.LongEmptyTraversal), Is.True);
            }
            finally
            {
                if (instance.valid) instance.Remove();
                if (data) Object.DestroyImmediate(data);
                Object.DestroyImmediate(cover); Object.DestroyImmediate(poi);
                Object.DestroyImmediate(settings); Object.DestroyImmediate(result);
            }
        }

        [Test]
        public void DisabledModulesDoNotProduceFalseFlags()
        {
            var settings = ScriptableObject.CreateInstance<MapAnalysisSettings>();
            try
            {
                var sample = new MapAnalysisSample { directions = 1, interestingDistance = 100,
                    coverDistance = 100, poiDistance = 100, density = 0 };
                sample.flags = MapAnalysisHeuristics.Classify(sample, settings);
                Assert.That(sample.flags.HasFlag(AnalysisFlags.PotentialDeadEnd), Is.True);
                Assert.That(MapAnalysisHeuristics.IsEmptyCandidate(sample, settings), Is.True);
                settings.emptyTraversal = settings.cover = settings.poiDistance = settings.objectDensity = settings.connectivity = false;
                Assert.That(MapAnalysisHeuristics.Classify(sample, settings), Is.EqualTo(AnalysisFlags.None));
                Assert.That(MapAnalysisHeuristics.IsEmptyCandidate(sample, settings), Is.False);
            }
            finally { Object.DestroyImmediate(settings); }
        }
    }
}

