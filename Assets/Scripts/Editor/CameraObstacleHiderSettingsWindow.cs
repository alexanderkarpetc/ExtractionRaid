using System.IO;
using UnityEditor;
using UnityEngine;
using View;

namespace Editor
{
    public class CameraObstacleHiderSettingsWindow : EditorWindow
    {
        const string AssetPath = "Assets/Resources/Configs/CameraObstacleHiderSettings.asset";

        CameraObstacleHiderSettings _settings;
        SerializedObject _serializedSettings;

        [MenuItem("Tools/Camera Obstacle Dissolve Settings")]
        static void Open()
        {
            var window = GetWindow<CameraObstacleHiderSettingsWindow>("Camera Dissolve");
            window.minSize = new Vector2(380f, 320f);
            window.Show();
        }

        void OnEnable()
        {
            LoadOrCreateSettings();
        }

        void OnGUI()
        {
            if (_settings == null || _serializedSettings == null)
                LoadOrCreateSettings();

            if (_settings == null || _serializedSettings == null)
            {
                EditorGUILayout.HelpBox("Could not create CameraObstacleHiderSettings asset.", MessageType.Error);
                return;
            }

            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("Camera Obstacle Dissolve", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Tune the Dither range for the current dissolve texture. If dissolve does not become fully visible/invisible, widen Visible/Hidden values, for example -0.2 to 1.2.",
                MessageType.Info);

            _serializedSettings.Update();
            DrawProperty("_ditherPropertyName");
            DrawProperty("_fallbackDitherPropertyName");
            EditorGUILayout.Space(6f);
            DrawProperty("_visibleDither");
            DrawProperty("_hiddenDither");
            EditorGUILayout.Space(6f);
            DrawProperty("_dissolveDuration");
            DrawProperty("_restoreDuration");
            EditorGUILayout.Space(6f);
            DrawProperty("_dissolveCurve");
            DrawProperty("_restoreCurve");
            _serializedSettings.ApplyModifiedProperties();

            EditorGUILayout.Space(10f);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Select Asset"))
                {
                    Selection.activeObject = _settings;
                    EditorGUIUtility.PingObject(_settings);
                }

                if (GUILayout.Button("Reset Defaults"))
                    ResetDefaults();
            }
        }

        void DrawProperty(string propertyName)
        {
            var property = _serializedSettings.FindProperty(propertyName);
            if (property != null)
                EditorGUILayout.PropertyField(property);
        }

        void LoadOrCreateSettings()
        {
            _settings = AssetDatabase.LoadAssetAtPath<CameraObstacleHiderSettings>(AssetPath);
            if (_settings == null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(AssetPath));
                _settings = CreateInstance<CameraObstacleHiderSettings>();
                AssetDatabase.CreateAsset(_settings, AssetPath);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }

            _serializedSettings = _settings != null ? new SerializedObject(_settings) : null;
        }

        void ResetDefaults()
        {
            Undo.RecordObject(_settings, "Reset Camera Obstacle Dissolve Settings");
            var so = new SerializedObject(_settings);
            so.FindProperty("_ditherPropertyName").stringValue = "Dither";
            so.FindProperty("_fallbackDitherPropertyName").stringValue = "_Dither";
            so.FindProperty("_visibleDither").floatValue = 0f;
            so.FindProperty("_hiddenDither").floatValue = 1f;
            so.FindProperty("_dissolveDuration").floatValue = 0.25f;
            so.FindProperty("_restoreDuration").floatValue = 0.2f;
            so.FindProperty("_dissolveCurve").animationCurveValue = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
            so.FindProperty("_restoreCurve").animationCurveValue = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(_settings);
            AssetDatabase.SaveAssets();
            _serializedSettings = new SerializedObject(_settings);
        }
    }
}
