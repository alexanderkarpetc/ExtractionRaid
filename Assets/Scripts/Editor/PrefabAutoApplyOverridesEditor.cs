using Dev;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    [CustomEditor(typeof(PrefabAutoApplyOverrides))]
    public class PrefabAutoApplyOverridesEditor : UnityEditor.Editor
    {
        SerializedProperty _autoApplyInEditMode;
        SerializedProperty _applyIntervalSeconds;
        SerializedProperty _logEachApply;

        void OnEnable()
        {
            _autoApplyInEditMode = serializedObject.FindProperty("_autoApplyInEditMode");
            _applyIntervalSeconds = serializedObject.FindProperty("_applyIntervalSeconds");
            _logEachApply = serializedObject.FindProperty("_logEachApply");
        }

        public override bool RequiresConstantRepaint()
        {
            return true;
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.HelpBox(
                "Automatically applies scene prefab overrides back to the source prefab on a timer. " +
                "Best used on the outermost root of the prefab instance designers are editing.",
                MessageType.Info);

            EditorGUILayout.PropertyField(_autoApplyInEditMode);
            EditorGUILayout.PropertyField(_applyIntervalSeconds);
            EditorGUILayout.PropertyField(_logEachApply);

            serializedObject.ApplyModifiedProperties();

            EditorGUILayout.Space(8);

            var autoApply = (PrefabAutoApplyOverrides)target;
            var inactiveReason = autoApply.GetInactiveReason();

            if (!string.IsNullOrEmpty(inactiveReason))
                EditorGUILayout.HelpBox(inactiveReason, MessageType.Warning);
            else if (autoApply.HasPendingOverrides())
                EditorGUILayout.HelpBox("Overrides detected. They will be applied on the next timer tick.", MessageType.Info);
            else
                EditorGUILayout.HelpBox("No pending overrides right now.", MessageType.None);

            EditorGUILayout.LabelField("Next auto apply", $"{autoApply.SecondsUntilNextApply:0.0}s");

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(!string.IsNullOrEmpty(inactiveReason)))
                {
                    if (GUILayout.Button("Apply Now"))
                    {
                        if (autoApply.TryApplyNow())
                            Repaint();
                    }
                }

                if (GUILayout.Button("Reset Timer"))
                {
                    autoApply.ResetTimer();
                    Repaint();
                }
            }
        }
    }
}
