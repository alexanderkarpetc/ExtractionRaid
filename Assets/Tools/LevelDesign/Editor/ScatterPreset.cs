using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace LevelDesign.Editor
{
    // Stores a detached settings snapshot, never scene ownership or window state.
    public sealed class ScatterPreset : ScriptableObject
    {
        [SerializeField, HideInInspector] string settingsJson;
        public bool HasSettings => !string.IsNullOrEmpty(settingsJson);

        public void Capture(ScatterSettings source)
        {
            foreach (var entry in source.prefabs)
            {
                var obj = entry.prefab.Resolve();
                if (obj && (!(obj is GameObject go) || !PrefabUtility.IsPartOfPrefabAsset(go) || go.transform.parent))
                    throw new InvalidOperationException("Presets accept prefab assets from Project, not scene objects or prefab children.");
            }
            var copy = Instantiate(source);
            try
            {
                copy.area = new BoxArea();
                copy.showArea = true;
                copy.editAreaHandles = false;
                copy.preview = new SceneReference();
                copy.selectedPreset = new SceneReference();
                copy.exclusionColliders.Clear();
                string json = JsonUtility.ToJson(copy);
                Undo.IncrementCurrentGroup();
                Undo.RecordObject(this, "Update Scatter Preset");
                settingsJson = json;
                EditorUtility.SetDirty(this);
            }
            finally { DestroyImmediate(copy); }
        }

        public void Apply(ScatterSettings target)
        {
            if (!HasSettings) throw new InvalidOperationException("This preset has no saved settings.");
            var copy = CreateInstance<ScatterSettings>();
            try
            {
                JsonUtility.FromJsonOverwrite(settingsJson, copy);
                using var source = new SerializedObject(copy);
                using var destination = new SerializedObject(target);
                Undo.IncrementCurrentGroup();
                Undo.SetCurrentGroupName("Apply Scatter Preset");
                var property = source.GetIterator();
                bool children = true;
                while (property.NextVisible(children))
                {
                    children = false;
                    switch (property.name)
                    {
                        case "m_Script": case "area": case "showArea": case "editAreaHandles":
                        case "preview": case "selectedPreset": case "exclusionColliders": continue;
                    }
                    destination.CopyFromSerializedProperty(property);
                }
                destination.ApplyModifiedProperties();
            }
            finally { DestroyImmediate(copy); }
        }

        public int AddPrefabs(ScatterSettings target)
        {
            if (!HasSettings) throw new InvalidOperationException("This preset has no saved settings.");
            var copy = CreateInstance<ScatterSettings>();
            try
            {
                JsonUtility.FromJsonOverwrite(settingsJson, copy);
                var existing = new HashSet<string>();
                foreach (var entry in target.prefabs) existing.Add(entry.prefab.id);
                var additions = new List<ScatterPrefabEntry>();
                foreach (var entry in copy.prefabs)
                    if (!string.IsNullOrEmpty(entry.prefab.id) && existing.Add(entry.prefab.id)) additions.Add(entry);
                if (additions.Count > 0)
                {
                    Undo.IncrementCurrentGroup();
                    Undo.RecordObject(target, "Add Scatter Preset Prefabs");
                    target.prefabs.AddRange(additions);
                }
                return additions.Count;
            }
            finally { DestroyImmediate(copy); }
        }
    }
}
