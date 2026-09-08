using System.Collections;
using UnityEditor;
using UnityEngine;

namespace LevelDesign.Editor
{
    public sealed class MapAnalysisWindow : ToolWindow<MapAnalysisSettings>
    {
        [SerializeField] MapAnalysisResult result;
        MapAnalysisSettings measured;
        string measuredJson;
        int selected = -1;
        [MenuItem("Tools/Level Design/Map Analysis")]
        static void Open() => GetWindow<MapAnalysisWindow>("Map Analysis v0.1");
        protected override void OnEnable()
        {
            base.OnEnable();
            if (!result) { result = CreateInstance<MapAnalysisResult>(); result.hideFlags = HideFlags.HideAndDontSave; }
        }
        protected override void OnDisable()
        {
            base.OnDisable();
            if (measured) DestroyImmediate(measured);
        }
        protected override void Buttons()
        {
            EditorGUILayout.LabelField($"Estimated grid samples: {MapAnalyzer.Estimate(settings):N0}");
            string legend = settings.overlay == AnalysisOverlay.ObjectDensity
                ? "Density: Low / Medium / High (geometric count, not quality)"
                : settings.overlay == AnalysisOverlay.Connectivity
                    ? "Directions: 0–1 very low; 2 corridor; 3 moderate; 4 good"
                    : "Distance: Good / Warning / Bad — thresholds of last analysis";
            EditorGUILayout.HelpBox(legend, MessageType.None);
            EditorGUILayout.HelpBox("Geometry = enabled renderers / non-trigger colliders on selected layers. POIs = explicit scene references. Grey = module disabled. Click a displayed sample to inspect.", MessageType.None);
            if (GUILayout.Button("Analyze Map"))
            {
                string error = MapAnalyzer.Validate(settings);
                if (error != null) status = error;
                else StartJob(Analyze());
            }
            if (GUILayout.Button("Clear Analysis"))
            {
                Undo.RecordObject(result, "Clear Map Analysis");
                result.samples.Clear(); result.clusters.Clear(); result.complete = false;
                selected = -1; SceneView.RepaintAll(); status = "Analysis cleared.";
            }
            if (selected >= 0 && selected < result.samples.Count)
            {
                var sample = result.samples[selected];
                EditorGUILayout.HelpBox($"Position: {sample.position}\nInteresting: {MapAnalysisRenderer.Distance(sample.interestingDistance)}\nCover: {MapAnalysisRenderer.Distance(sample.coverDistance)}\nPOI: {MapAnalysisRenderer.Distance(sample.poiDistance)}\nDensity: {sample.density}; connectivity: {sample.directions}\nFlags: {sample.flags}", MessageType.None);
            }
        }
        IEnumerator Analyze()
        {
            var snapshot = Instantiate(settings);
            var next = CreateInstance<MapAnalysisResult>();
            snapshot.hideFlags = next.hideFlags = HideFlags.HideAndDontSave;
            try
            {
                var routine = new MapAnalyzer().Analyze(snapshot, next, message => status = message);
                while (routine.MoveNext()) yield return null;
                Undo.RecordObject(result, "Analyze Map");
                result.samples = next.samples; result.clusters = next.clusters;
                result.spacing = next.spacing; result.complete = next.complete;
                result.measurementSettings = next.measurementSettings;
                selected = -1;
            }
            finally { DestroyImmediate(snapshot); DestroyImmediate(next); }
        }
        protected override void SceneGUI(SceneView view)
        {
            base.SceneGUI(view);
            if (!result || !result.complete || EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (!measured || measuredJson != result.measurementSettings)
            {
                if (measured) DestroyImmediate(measured);
                measured = CreateInstance<MapAnalysisSettings>(); measured.hideFlags = HideFlags.HideAndDontSave;
                measuredJson = result.measurementSettings;
                JsonUtility.FromJsonOverwrite(measuredJson, measured);
            }
            int previous = selected;
            selected = MapAnalysisRenderer.Draw(view, result, settings, measured, selected);
            if (selected != previous) Repaint();
        }
    }
}
