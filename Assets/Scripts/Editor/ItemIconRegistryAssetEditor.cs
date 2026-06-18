using System.Collections.Generic;
using Constants;
using State;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace Editor
{
    [CustomEditor(typeof(ItemIconRegistryAsset))]
    public class ItemIconRegistryAssetEditor : UnityEditor.Editor
    {
        SerializedProperty _entries;

        void OnEnable() => _entries = serializedObject.FindProperty("_entries");

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            DrawSyncButton();
            EditorGUILayout.Space(4);
            DrawStats();
            EditorGUILayout.Space(6);
            DrawEntries();

            serializedObject.ApplyModifiedProperties();
        }

        void DrawSyncButton()
        {
            EditorGUILayout.LabelField("Sync", EditorStyles.boldLabel);
            if (GUILayout.Button("Sync from ItemDefinition"))
            {
                var registry = ItemDefinition.Registry;
                var existing = new HashSet<string>();
                for (int i = 0; i < _entries.arraySize; i++)
                    existing.Add(_entries.GetArrayElementAtIndex(i)
                        .FindPropertyRelative("DefinitionId").stringValue);

                int added = 0;
                foreach (var id in registry.Keys)
                {
                    if (existing.Contains(id)) continue;
                    int idx = _entries.arraySize;
                    _entries.InsertArrayElementAtIndex(idx);
                    var e = _entries.GetArrayElementAtIndex(idx);
                    e.FindPropertyRelative("DefinitionId").stringValue = id;
                    e.FindPropertyRelative("Icon").objectReferenceValue = null;
                    added++;
                }

                if (added > 0)
                    Debug.Log($"[ItemIconRegistry] Added {added} new item entries.");
                else
                    Debug.Log("[ItemIconRegistry] Already up to date.");
            }
        }

        void DrawStats()
        {
            int total = _entries.arraySize;
            int assigned = 0;
            for (int i = 0; i < total; i++)
                if (_entries.GetArrayElementAtIndex(i)
                        .FindPropertyRelative("Icon").objectReferenceValue != null)
                    assigned++;

            var style = assigned < total ? EditorStyles.boldLabel : EditorStyles.label;
            EditorGUILayout.LabelField($"Icons assigned: {assigned} / {total}", style);
        }

        void DrawEntries()
        {
            EditorGUILayout.LabelField("Entries", EditorStyles.boldLabel);

            for (int i = 0; i < _entries.arraySize; i++)
            {
                var entry = _entries.GetArrayElementAtIndex(i);
                var defIdProp = entry.FindPropertyRelative("DefinitionId");
                var iconProp  = entry.FindPropertyRelative("Icon");

                using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
                {
                    DrawItemPicker(defIdProp);

                    bool missing = iconProp.objectReferenceValue == null;
                    var prevColor = GUI.color;
                    if (missing) GUI.color = new Color(1f, 0.6f, 0.4f);
                    EditorGUILayout.PropertyField(iconProp, GUIContent.none, GUILayout.Width(120));
                    GUI.color = prevColor;

                    if (GUILayout.Button("✕", GUILayout.Width(24)))
                    {
                        _entries.DeleteArrayElementAtIndex(i);
                        break;
                    }
                }
            }

            EditorGUILayout.Space(2);
            if (GUILayout.Button("+ Add Entry"))
            {
                int idx = _entries.arraySize;
                _entries.InsertArrayElementAtIndex(idx);
                var e = _entries.GetArrayElementAtIndex(idx);
                e.FindPropertyRelative("DefinitionId").stringValue = "";
                e.FindPropertyRelative("Icon").objectReferenceValue = null;
            }
        }

        static void DrawItemPicker(SerializedProperty defIdProp)
        {
            string id  = defIdProp.stringValue;
            var def    = string.IsNullOrEmpty(id) ? null : ItemDefinition.Get(id);
            string label = def != null
                ? $"{def.DisplayName}  ({def.Id})"
                : string.IsNullOrEmpty(id) ? "Select item…" : $"⚠ Unknown: {id}";

            var rect = GUILayoutUtility.GetRect(new GUIContent(label), EditorStyles.popup,
                GUILayout.ExpandWidth(true));
            if (EditorGUI.DropdownButton(rect, new GUIContent(label), FocusType.Keyboard))
            {
                var so   = defIdProp.serializedObject;
                var path = defIdProp.propertyPath;
                var dropdown = new ItemPickerDropdown(new AdvancedDropdownState(), pickedId =>
                {
                    so.Update();
                    so.FindProperty(path).stringValue = pickedId;
                    so.ApplyModifiedProperties();
                });
                dropdown.Show(rect);
            }
        }
    }
}
