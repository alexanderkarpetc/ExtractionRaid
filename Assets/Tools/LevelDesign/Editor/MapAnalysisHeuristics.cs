using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace LevelDesign.Editor
{
    public static class MapAnalysisHeuristics
    {
        public static AnalysisFlags Classify(MapAnalysisSample sample, MapAnalysisSettings settings)
        {
            AnalysisFlags flags = AnalysisFlags.None;
            if (settings.emptyTraversal && sample.interestingDistance >= settings.traversalThresholds.bad) flags |= AnalysisFlags.Empty;
            if (settings.cover && sample.coverDistance >= settings.coverThresholds.bad) flags |= AnalysisFlags.LowCover;
            if (settings.poiDistance && sample.poiDistance >= settings.poiThresholds.bad) flags |= AnalysisFlags.FarPOI;
            if (settings.objectDensity && sample.density >= 0 && sample.density <= settings.lowDensityThreshold) flags |= AnalysisFlags.LowDensity;
            if (settings.connectivity && sample.directions >= 0)
            {
                if (sample.directions <= settings.deadEndMaxDirections) flags |= AnalysisFlags.PotentialDeadEnd;
                if (sample.directions == 2) flags |= AnalysisFlags.Corridor;
            }
            return flags;
        }
        public static bool IsEmptyCandidate(MapAnalysisSample sample, MapAnalysisSettings settings) =>
            settings.emptyTraversal && (sample.flags & AnalysisFlags.Empty) != 0 &&
            (!settings.cover || (sample.flags & AnalysisFlags.LowCover) != 0) &&
            (!settings.poiDistance || (sample.flags & AnalysisFlags.FarPOI) != 0) &&
            (!settings.objectDensity || (sample.flags & AnalysisFlags.LowDensity) != 0);

        public static IEnumerator Calculate(MapAnalysisSettings settings, MapAnalysisResult result, NavMeshQueryFilter filter)
        {
            var offsets = new[] { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
            var grid = new Dictionary<Vector2Int, int>();
            for (int i = 0; i < result.samples.Count; i++)
            {
                var sample = result.samples[i];
                grid[new Vector2Int(sample.gridX, sample.gridZ)] = i;
                if (settings.connectivity)
                {
                    sample.directions = 0;
                    foreach (var offset in offsets)
                    {
                        var target = sample.position + new Vector3(offset.x, 0, offset.y) * settings.connectivityRadius;
                        if (MapAnalyzer.TrySample(target, settings, filter, out var point) &&
                            !NavMesh.Raycast(sample.position, point, out _, filter)) sample.directions++;
                    }
                }
                sample.flags = Classify(sample, settings);
                yield return null;
            }
            var visited = new HashSet<int>();
            var queue = new Queue<int>();
            var members = new List<int>();
            for (int start = 0; start < result.samples.Count; start++)
            {
                if (!IsEmptyCandidate(result.samples[start], settings) || !visited.Add(start)) continue;
                queue.Enqueue(start); members.Clear();
                var cluster = new MapAnalysisCluster();
                while (queue.Count > 0)
                {
                    int index = queue.Dequeue(); members.Add(index);
                    var sample = result.samples[index];
                    cluster.center += sample.position;
                    if (settings.cover) cluster.coverDistance = Mathf.Max(cluster.coverDistance, sample.coverDistance);
                    if (settings.poiDistance) cluster.poiDistance = Mathf.Max(cluster.poiDistance, sample.poiDistance);
                    foreach (var offset in offsets)
                    {
                        if (!grid.TryGetValue(new Vector2Int(sample.gridX, sample.gridZ) + offset, out int neighbor) || visited.Contains(neighbor)) continue;
                        var next = result.samples[neighbor];
                        if (!IsEmptyCandidate(next, settings) || NavMesh.Raycast(sample.position, next.position, out _, filter)) continue;
                        visited.Add(neighbor); queue.Enqueue(neighbor);
                    }
                    yield return null;
                }
                if (members.Count < settings.minimumClusterSamples) continue;
                cluster.count = members.Count; cluster.center /= cluster.count;
                if (!settings.cover) cluster.coverDistance = float.NaN;
                if (!settings.poiDistance) cluster.poiDistance = float.NaN;
                foreach (int index in members)
                {
                    var sample = result.samples[index];
                    sample.flags |= AnalysisFlags.LongEmptyTraversal;
                    cluster.radius = Mathf.Max(cluster.radius, Vector3.Distance(cluster.center, sample.position) + result.spacing * .5f);
                }
                result.clusters.Add(cluster);
            }
            result.clusters.Sort((a, b) => b.count.CompareTo(a.count));
        }
    }
}
