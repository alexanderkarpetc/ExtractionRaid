using System;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace LevelDesign.Editor.Tests
{
    public sealed class ToolWindowTests
    {
        [TestCase(typeof(EnvironmentScatterWindow), typeof(ScatterSettings), "targetObjectCount")]
        [TestCase(typeof(MapAnalysisWindow), typeof(MapAnalysisSettings), "sampleSpacing")]
        public void SettingsAreEditableWhenCreatedAndAfterReload(Type windowType, Type settingsType, string propertyName)
        {
            string key = "ExtractionRaid.LevelDesign." + settingsType.Name + "." + Application.dataPath;
            bool hadPreference = EditorPrefs.HasKey(key);
            string preference = EditorPrefs.GetString(key);
            EditorWindow window = null;
            ScriptableObject settings = null;
            try
            {
                window = (EditorWindow)ScriptableObject.CreateInstance(windowType);
                var baseType = windowType.BaseType;
                settings = (ScriptableObject)baseType.GetField("settings", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(window);
                using (var serialized = new SerializedObject(settings))
                    Assert.That(serialized.FindProperty(propertyName).editable, Is.True, "Fresh window settings must be editable.");

                baseType.GetMethod("OnDisable", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(window, null);
                settings.hideFlags = HideFlags.HideAndDontSave; // Previously serialized window state.
                baseType.GetMethod("OnEnable", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(window, null);
                using (var serialized = new SerializedObject(settings))
                    Assert.That(serialized.FindProperty(propertyName).editable, Is.True, "Reload must repair existing settings too.");
            }
            finally
            {
                if (window) UnityEngine.Object.DestroyImmediate(window);
                if (settings) UnityEngine.Object.DestroyImmediate(settings);
                if (hadPreference) EditorPrefs.SetString(key, preference); else EditorPrefs.DeleteKey(key);
            }
        }
    }
}
