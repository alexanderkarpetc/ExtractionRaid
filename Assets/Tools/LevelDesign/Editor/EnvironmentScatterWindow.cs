using UnityEditor;
using UnityEngine;

namespace LevelDesign.Editor
{
    public sealed class EnvironmentScatterWindow : ToolWindow<ScatterSettings>
    {
        ScatterPreset[] presets = new ScatterPreset[0];
        string[] presetLabels = { "None" };

        protected override void OnEnable()
        {
            base.OnEnable();
            RefreshPresets();
            EditorApplication.projectChanged += RefreshPresets;
        }
        protected override void OnDisable()
        {
            EditorApplication.projectChanged -= RefreshPresets;
            base.OnDisable();
        }
        void RefreshPresets()
        {
            var guids = AssetDatabase.FindAssets("t:ScatterPreset");
            var found = new System.Collections.Generic.List<ScatterPreset>();
            foreach (string guid in guids)
            {
                var preset = AssetDatabase.LoadAssetAtPath<ScatterPreset>(AssetDatabase.GUIDToAssetPath(guid));
                if (preset) found.Add(preset);
            }
            found.Sort((a, b) => string.Compare(AssetDatabase.GetAssetPath(a), AssetDatabase.GetAssetPath(b), System.StringComparison.OrdinalIgnoreCase));
            presets = found.ToArray();
            presetLabels = new string[presets.Length + 1];
            presetLabels[0] = "None";
            for (int i = 0; i < presets.Length; i++)
                presetLabels[i + 1] = presets[i].name + " — " + AssetDatabase.GetAssetPath(presets[i]).Replace("/", " › ");
            Repaint();
        }
        protected override void HeaderGUI()
        {
            EditorGUILayout.LabelField("Presets", EditorStyles.boldLabel);
            var selected = settings.selectedPreset.Resolve() as ScatterPreset;
            int index = System.Array.IndexOf(presets, selected) + 1;
            int next = EditorGUILayout.Popup("Preset", index, presetLabels);
            if (next != index)
            {
                Undo.RecordObject(settings, "Select Scatter Preset");
                selected = next > 0 ? presets[next - 1] : null;
                settings.selectedPreset.Set(selected);
                Save();
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Save New…")) SaveNewPreset();
                using (new EditorGUI.DisabledScope(!selected))
                    if (GUILayout.Button("Update Selected")) PresetAction(() =>
                    {
                        selected.Capture(settings);
                        AssetDatabase.SaveAssetIfDirty(selected);
                        status = "Updated preset: " + selected.name + " (Undo available).";
                    });
            }
            using (new EditorGUILayout.HorizontalScope())
            using (new EditorGUI.DisabledScope(!selected || !selected.HasSettings))
            {
                if (GUILayout.Button("Apply")) PresetAction(() =>
                {
                    selected.Apply(settings);
                    status = "Applied preset: " + selected.name + ". Generate Preview to see the result.";
                });
                if (GUILayout.Button("Add Prefabs")) PresetAction(() =>
                    status = $"Added {selected.AddPrefabs(settings)} prefab entries; existing entries and placement rules retained.");
            }
            EditorGUILayout.HelpBox("Apply replaces prefab entries and generation rules. Area, scene exclusions and Preview stay in place. Add Prefabs merges entries without duplicates.", MessageType.None);
        }
        void PresetAction(System.Action action)
        {
            try { action(); Save(); Repaint(); SceneView.RepaintAll(); }
            catch (System.Exception error) { status = error.Message; Debug.LogException(error); }
        }
        void SaveNewPreset()
        {
            string path = EditorUtility.SaveFilePanelInProject("Save Scatter Preset", "ScatterPreset", "asset",
                "Choose a name such as Rocks or Paper. Store presets in an Editor folder.", "Assets/Tools/LevelDesign/Editor");
            if (string.IsNullOrEmpty(path)) return;
            if (!path.Contains("/Editor/") || AssetDatabase.LoadMainAssetAtPath(path) || System.IO.File.Exists(path))
            {
                status = "Choose a new filename inside an Editor folder. Use Update Selected to update an existing preset.";
                return;
            }
            PresetAction(() =>
            {
                var preset = CreateInstance<ScatterPreset>();
                try
                {
                    preset.Capture(settings);
                    AssetDatabase.CreateAsset(preset, path);
                    AssetDatabase.SaveAssetIfDirty(preset);
                    Undo.RecordObject(settings, "Select New Scatter Preset");
                    settings.selectedPreset.Set(preset);
                    RefreshPresets();
                    status = "Saved preset: " + preset.name;
                }
                finally { if (!AssetDatabase.Contains(preset)) DestroyImmediate(preset); }
            });
        }
        [MenuItem("Tools/Level Design/Environment Scatter")]
        static void Open() => GetWindow<EnvironmentScatterWindow>("Environment Scatter");
        protected override void Buttons()
        {
            if (GUILayout.Button("Generate Preview")) Generate(false);
            if (GUILayout.Button("Randomize Seed")) Generate(true);
            if (GUILayout.Button("Clear Preview")) { ScatterGenerator.Clear(settings); Save(); status = "Preview cleared (Undo available)."; }
            using (new EditorGUI.DisabledScope(!settings.preview.Resolve()))
                if (GUILayout.Button("Bake")) { ScatterGenerator.Bake(settings); Save(); status = "Baked scene objects (Undo available)."; }
        }
        void Generate(bool randomize)
        {
            string error = ScatterGenerator.Validate(settings);
            if (error != null) { status = error; return; }
            if (randomize)
            {
                Undo.RecordObject(settings, "Randomize Scatter Seed");
                settings.seed = System.Guid.NewGuid().GetHashCode();
            }
            StartJob(new ScatterGenerator().Generate(settings, message => status = message));
        }
    }
}
