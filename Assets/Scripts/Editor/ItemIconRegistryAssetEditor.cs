using System;
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

        // Foldout state per category — survives inspector redraws, resets on domain reload (fine).
        readonly Dictionary<ItemCategory, bool> _foldouts = new();

        void OnEnable() => _entries = serializedObject.FindProperty("_entries");

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            DrawToolbar();
            EditorGUILayout.Space(4);
            DrawStats();
            EditorGUILayout.Space(6);
            DrawEntriesByCategory();

            serializedObject.ApplyModifiedProperties();
        }

        void DrawToolbar()
        {
            EditorGUILayout.LabelField("Tools", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Sync from ItemDefinition"))
                    SyncFromRegistry();

                if (GUILayout.Button("Sort by Category"))
                    SortByCategory();
            }
        }

        void SyncFromRegistry()
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

            serializedObject.ApplyModifiedProperties();
            Debug.Log(added > 0
                ? $"[ItemIconRegistry] Added {added} new item entries."
                : "[ItemIconRegistry] Already up to date.");
        }

        void SortByCategory()
        {
            // Snapshot current data.
            var snapshot = new List<(string id, UnityEngine.Object icon)>();
            for (int i = 0; i < _entries.arraySize; i++)
            {
                var e = _entries.GetArrayElementAtIndex(i);
                snapshot.Add((
                    e.FindPropertyRelative("DefinitionId").stringValue,
                    e.FindPropertyRelative("Icon").objectReferenceValue
                ));
            }

            snapshot.Sort((a, b) =>
            {
                var defA = string.IsNullOrEmpty(a.id) ? null : ItemDefinition.Get(a.id);
                var defB = string.IsNullOrEmpty(b.id) ? null : ItemDefinition.Get(b.id);
                int catA = defA != null ? (int)defA.Category : int.MaxValue;
                int catB = defB != null ? (int)defB.Category : int.MaxValue;
                int cmp  = catA.CompareTo(catB);
                if (cmp != 0) return cmp;
                string nameA = defA?.DisplayName ?? a.id ?? "";
                string nameB = defB?.DisplayName ?? b.id ?? "";
                return string.Compare(nameA, nameB, StringComparison.OrdinalIgnoreCase);
            });

            // Write back.
            for (int i = 0; i < snapshot.Count; i++)
            {
                var e = _entries.GetArrayElementAtIndex(i);
                e.FindPropertyRelative("DefinitionId").stringValue   = snapshot[i].id;
                e.FindPropertyRelative("Icon").objectReferenceValue  = snapshot[i].icon;
            }

            serializedObject.ApplyModifiedProperties();
            Debug.Log("[ItemIconRegistry] Sorted by category.");
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

        // Build a per-category index so we can draw headers + collapse groups.
        void DrawEntriesByCategory()
        {
            // Group serialized-array indices by category.
            var groups = new Dictionary<ItemCategory, List<int>>();
            var unknownIndices = new List<int>();

            for (int i = 0; i < _entries.arraySize; i++)
            {
                var id  = _entries.GetArrayElementAtIndex(i)
                              .FindPropertyRelative("DefinitionId").stringValue;
                var def = string.IsNullOrEmpty(id) ? null : ItemDefinition.Get(id);
                if (def == null) { unknownIndices.Add(i); continue; }

                if (!groups.TryGetValue(def.Category, out var list))
                {
                    list = new List<int>();
                    groups[def.Category] = list;
                }
                list.Add(i);
            }

            // Draw in enum order.
            foreach (ItemCategory cat in Enum.GetValues(typeof(ItemCategory)))
            {
                if (!groups.TryGetValue(cat, out var indices) || indices.Count == 0) continue;
                DrawCategoryGroup(cat.ToString(), indices);
            }

            if (unknownIndices.Count > 0)
                DrawCategoryGroup("Unknown / No ID", unknownIndices);

            EditorGUILayout.Space(4);
            if (GUILayout.Button("+ Add Entry"))
            {
                int idx = _entries.arraySize;
                _entries.InsertArrayElementAtIndex(idx);
                var e = _entries.GetArrayElementAtIndex(idx);
                e.FindPropertyRelative("DefinitionId").stringValue = "";
                e.FindPropertyRelative("Icon").objectReferenceValue = null;
            }
        }

        void DrawCategoryGroup(string label, List<int> indices)
        {
            // Parse category for foldout key — fallback for "Unknown" group.
            bool hasKey = Enum.TryParse(label, out ItemCategory cat);

            if (!_foldouts.TryGetValue(cat, out bool open))
                open = true; // expanded by default

            int assigned = 0;
            foreach (int i in indices)
                if (_entries.GetArrayElementAtIndex(i)
                        .FindPropertyRelative("Icon").objectReferenceValue != null)
                    assigned++;

            string header = $"{label}  ({assigned}/{indices.Count})";
            bool newOpen = EditorGUILayout.Foldout(open, header, true, EditorStyles.foldoutHeader);
            if (hasKey) _foldouts[cat] = newOpen;
            if (!newOpen) return;

            bool deleted = false;
            foreach (int i in indices)
            {
                if (deleted) break; // array shifted — bail and repaint next frame
                var entry    = _entries.GetArrayElementAtIndex(i);
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
                        deleted = true;
                    }
                }
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
