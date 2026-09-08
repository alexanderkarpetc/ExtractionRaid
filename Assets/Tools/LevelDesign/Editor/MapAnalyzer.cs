using System;
using System.Collections;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

namespace LevelDesign.Editor
{
    public sealed class MapAnalyzer
    {
        public static long Estimate(MapAnalysisSettings settings)
        {
            if (!settings.area.Valid || !float.IsFinite(settings.sampleSpacing) || settings.sampleSpacing < .1f) return long.MaxValue;
            double x = Math.Ceiling(settings.area.size.x / settings.sampleSpacing);
            double z = Math.Ceiling(settings.area.size.y / settings.sampleSpacing);
            return x * z >= long.MaxValue ? long.MaxValue : (long)(x * z);
        }
        public static string Validate(MapAnalysisSettings settings)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || PrefabStageUtility.GetCurrentPrefabStage() != null)
                return "Use a normal scene in Edit Mode.";
            long estimated = Estimate(settings);
            if (estimated < 1 || estimated > Mathf.Clamp(settings.maximumSamples, 1, 100000))
                return $"Estimated samples: {estimated:N0}. Increase spacing or reduce area; maximum {settings.maximumSamples:N0} (hard cap 100,000).";
            if (!settings.traversalThresholds.Valid || !settings.coverThresholds.Valid || !settings.poiThresholds.Valid)
                return "Distance thresholds require 0 <= Warning < Bad.";
            if (settings.sampleHeightTolerance <= 0 || settings.connectivityRadius < .1f || settings.densityRadius < .1f ||
                settings.lowDensityThreshold < 0 || settings.highDensityThreshold <= settings.lowDensityThreshold || settings.minimumClusterSamples < 1)
                return "Check sample height, radii, density thresholds and minimum cluster size.";
            return null;
        }

        public IEnumerator Analyze(MapAnalysisSettings settings, MapAnalysisResult result, Action<string> report)
        {
            result.samples.Clear(); result.clusters.Clear(); result.complete = false;
            result.spacing = settings.sampleSpacing;
            result.measurementSettings = JsonUtility.ToJson(settings);
            var geometry = new MapGeometrySnapshot();
            report("Collecting loaded scene geometry…");
            var collection = geometry.Collect(settings);
            while (collection.MoveNext()) yield return null;
            int width = Mathf.CeilToInt(settings.area.size.x / settings.sampleSpacing);
            int depth = Mathf.CeilToInt(settings.area.size.y / settings.sampleSpacing);
            var filter = new NavMeshQueryFilter { agentTypeID = settings.agentTypeId, areaMask = settings.navMeshAreaMask };
            for (int z = 0; z < depth; z++) for (int x = 0; x < width; x++)
            {
                var point = settings.area.Point((x + .5f) / width - .5f, (z + .5f) / depth - .5f);
                if (TrySample(point, settings, filter, out var position))
                {
                    var sample = new MapAnalysisSample { position = position, gridX = x, gridZ = z };
                    if (settings.emptyTraversal) sample.interestingDistance = geometry.interesting.Nearest(position);
                    if (settings.cover) sample.coverDistance = geometry.cover.Nearest(position);
                    if (settings.poiDistance) sample.poiDistance = geometry.NearestPOI(position);
                    if (settings.objectDensity) sample.density = geometry.density.CountWithin(position, settings.densityRadius);
                    result.samples.Add(sample);
                }
                report($"Sampling NavMesh: {z * width + x + 1}/{width * depth}; valid: {result.samples.Count}");
                yield return null;
            }
            report("Checking local NavMesh connectivity and grouping empty regions…");
            var heuristics = MapAnalysisHeuristics.Calculate(settings, result, filter);
            while (heuristics.MoveNext()) yield return null;
            result.complete = true;
            report($"Analysis ready: {result.samples.Count:N0} samples; {geometry.GeometryCount:N0} geometry components. " +
                (result.samples.Count == 0 ? "Check baked NavMesh, agent, mask and area height." : "Distances are XZ bounds approximations; missing targets report infinity."));
        }

        public static bool TrySample(Vector3 point, MapAnalysisSettings settings, NavMeshQueryFilter filter, out Vector3 position)
        {
            position = default;
            float horizontalTolerance = settings.sampleSpacing * .2f;
            if (!NavMesh.SamplePosition(point, out var hit, Mathf.Max(horizontalTolerance, settings.sampleHeightTolerance), filter)) return false;
            var offset = hit.position - point;
            if (Mathf.Abs(offset.y) > settings.sampleHeightTolerance ||
                offset.x * offset.x + offset.z * offset.z > horizontalTolerance * horizontalTolerance) return false;
            position = hit.position;
            return true;
        }
    }
}
