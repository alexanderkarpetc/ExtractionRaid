using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace LevelDesign.Editor
{
    public static class MapAnalysisRenderer
    {
        public static Color SampleColor(MapAnalysisSample sample, MapAnalysisSettings display, MapAnalysisSettings measured)
        {
            int category;
            switch (display.overlay)
            {
                case AnalysisOverlay.CoverDistance:
                    if (float.IsNaN(sample.coverDistance)) return Color.gray;
                    category = measured.coverThresholds.Classify(sample.coverDistance); break;
                case AnalysisOverlay.POIDistance:
                    if (float.IsNaN(sample.poiDistance)) return Color.gray;
                    category = measured.poiThresholds.Classify(sample.poiDistance); break;
                case AnalysisOverlay.ObjectDensity:
                    if (sample.density < 0) return Color.gray;
                    category = sample.density <= measured.lowDensityThreshold ? 2 : sample.density >= measured.highDensityThreshold ? 0 : 1; break;
                case AnalysisOverlay.Connectivity:
                    if (sample.directions < 0) return Color.gray;
                    category = sample.directions <= 1 ? 2 : sample.directions <= 3 ? 1 : 0; break;
                default:
                    if (float.IsNaN(sample.interestingDistance)) return Color.gray;
                    category = measured.traversalThresholds.Classify(sample.interestingDistance); break;
            }
            return category == 0 ? display.goodColor : category == 1 ? display.warningColor : display.badColor;
        }
        public static int Draw(SceneView view, MapAnalysisResult result, MapAnalysisSettings display, MapAnalysisSettings measured, int selected)
        {
            if (!result || !display.showVisualization || !result.complete) return selected;
            var previousZ = Handles.zTest;
            var previousColor = Handles.color;
            try
            {
                Handles.zTest = CompareFunction.LessEqual;
                int stride = Mathf.Max(1, Mathf.CeilToInt(result.samples.Count / (float)Mathf.Max(1, display.maximumDrawnSamples)));
                float nearest = 10; int hovered = -1;
                bool pick = Event.current.type == EventType.MouseDown && Event.current.button == 0 && !Event.current.alt;
                if (display.showSamples)
                    for (int i = 0; i < result.samples.Count; i += stride)
                    {
                        var sample = result.samples[i];
                        var point = sample.position + Vector3.up * .08f;
                        var viewport = view.camera.WorldToViewportPoint(point);
                        if (viewport.z <= 0 || viewport.x < 0 || viewport.x > 1 || viewport.y < 0 || viewport.y > 1) continue;
                        if (Event.current.type == EventType.Repaint)
                        {
                            Handles.color = SampleColor(sample, display, measured);
                            Handles.DrawSolidDisc(point, Vector3.up, result.spacing * .18f);
                            if ((sample.flags & AnalysisFlags.PotentialDeadEnd) != 0)
                            { Handles.color = Color.white; Handles.DrawWireDisc(point, Vector3.up, result.spacing * .3f); }
                        }
                        if (pick)
                        {
                            float distance = Vector2.Distance(HandleUtility.WorldToGUIPoint(point), Event.current.mousePosition);
                            if (distance < nearest) { nearest = distance; hovered = i; }
                        }
                    }
                if (pick && hovered >= 0) { selected = hovered; Event.current.Use(); }
                if (display.showProblemAreas && Event.current.type == EventType.Repaint)
                    for (int i = 0; i < Mathf.Min(result.clusters.Count, display.maximumDrawnMarkers); i++)
                    {
                        var cluster = result.clusters[i];
                        Handles.color = display.badColor;
                        Handles.DrawWireDisc(cluster.center + Vector3.up * .1f, Vector3.up, cluster.radius);
                        if (display.showLabels) Handles.Label(cluster.center + Vector3.up,
                            $"LONG EMPTY TRAVERSAL ({cluster.count})\nCover: {Distance(cluster.coverDistance)}  POI: {Distance(cluster.poiDistance)}");
                    }
            }
            finally { Handles.zTest = previousZ; Handles.color = previousColor; }
            return selected;
        }
        public static string Distance(float distance) => float.IsNaN(distance) ? "disabled" :
            float.IsPositiveInfinity(distance) ? "∞ (no target)" : $"{distance:F1} m";
    }
}
