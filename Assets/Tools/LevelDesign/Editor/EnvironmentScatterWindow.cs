using UnityEditor;
using UnityEngine;

namespace LevelDesign.Editor
{
    public sealed class EnvironmentScatterWindow : ToolWindow<ScatterSettings>
    {
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
