using Constants;
using State;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace Editor
{
    // Custom inspector for ContainerTypeConfigAsset. Replaces the raw "definitionId"
    // text field on each drop entry with a category-grouped item picker (the same
    // dropdown used by the DevCheats "Give Item" row), so you never copy item ids.
    [CustomEditor(typeof(ContainerTypeConfigAsset))]
    public class ContainerTypeConfigAssetEditor : UnityEditor.Editor
    {
        SerializedProperty _typeId;
        SerializedProperty _displayName;
        SerializedProperty _slotCount;
        SerializedProperty _minDrops;
        SerializedProperty _maxDrops;
        SerializedProperty _drops;

        void OnEnable()
        {
            _typeId = serializedObject.FindProperty("_typeId");
            _displayName = serializedObject.FindProperty("_displayName");
            _slotCount = serializedObject.FindProperty("_slotCount");
            _minDrops = serializedObject.FindProperty("_minDrops");
            _maxDrops = serializedObject.FindProperty("_maxDrops");
            _drops = serializedObject.FindProperty("_drops");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.LabelField("Identity", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_typeId);
            EditorGUILayout.PropertyField(_displayName);

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Capacity", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_slotCount);

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Drop Count", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_minDrops);
            EditorGUILayout.PropertyField(_maxDrops);

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Drop Pool", EditorStyles.boldLabel);
            DrawDropPool();

            serializedObject.ApplyModifiedProperties();
        }

        void DrawDropPool()
        {
            for (int i = 0; i < _drops.arraySize; i++)
            {
                var entry = _drops.GetArrayElementAtIndex(i);
                var defId = entry.FindPropertyRelative("definitionId");
                var minCount = entry.FindPropertyRelative("minCount");
                var maxCount = entry.FindPropertyRelative("maxCount");
                var weight = entry.FindPropertyRelative("weight");
                var weaponPreset = entry.FindPropertyRelative("weaponPreset");

                bool isPreset = weaponPreset.objectReferenceValue != null;

                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        // When a weapon preset is assigned it drives the drop — the item id
                        // and count are ignored, so disable the item picker to make that clear.
                        using (new EditorGUI.DisabledScope(isPreset))
                            DrawItemPicker(defId);

                        if (GUILayout.Button("✕", GUILayout.Width(24)))
                        {
                            _drops.DeleteArrayElementAtIndex(i);
                            break;
                        }
                    }

                    EditorGUILayout.PropertyField(weaponPreset,
                        new GUIContent("Weapon Preset", "Optional. When set, this drop spawns the " +
                            "assembled weapon from the preset instead of the item above."));

                    using (new EditorGUILayout.HorizontalScope())
                    {
                        using (new EditorGUI.DisabledScope(isPreset))
                        {
                            EditorGUILayout.LabelField("Count", GUILayout.Width(40));
                            EditorGUILayout.PropertyField(minCount, GUIContent.none, GUILayout.Width(50));
                            EditorGUILayout.LabelField("–", GUILayout.Width(10));
                            EditorGUILayout.PropertyField(maxCount, GUIContent.none, GUILayout.Width(50));
                        }

                        GUILayout.Space(12);
                        EditorGUILayout.LabelField("Weight", GUILayout.Width(48));
                        EditorGUILayout.PropertyField(weight, GUIContent.none, GUILayout.Width(60));
                        GUILayout.FlexibleSpace();
                    }
                }
            }

            EditorGUILayout.Space(2);
            if (GUILayout.Button("+ Add Drop"))
            {
                int idx = _drops.arraySize;
                _drops.InsertArrayElementAtIndex(idx);
                var entry = _drops.GetArrayElementAtIndex(idx);
                // Reset to sensible defaults (InsertArrayElement copies the previous element).
                entry.FindPropertyRelative("definitionId").stringValue = "";
                entry.FindPropertyRelative("minCount").intValue = 1;
                entry.FindPropertyRelative("maxCount").intValue = 1;
                entry.FindPropertyRelative("weight").floatValue = 1f;
                entry.FindPropertyRelative("weaponPreset").objectReferenceValue = null;
            }
        }

        static void DrawItemPicker(SerializedProperty defId)
        {
            string id = defId.stringValue;
            var def = string.IsNullOrEmpty(id) ? null : ItemDefinition.Get(id);
            string label = def != null
                ? $"{def.DisplayName}  ({def.Id})"
                : string.IsNullOrEmpty(id) ? "Select item…" : $"⚠ Unknown id: {id}";

            var btnRect = GUILayoutUtility.GetRect(new GUIContent(label), EditorStyles.popup,
                GUILayout.ExpandWidth(true));
            if (EditorGUI.DropdownButton(btnRect, new GUIContent(label), FocusType.Keyboard))
            {
                var so = defId.serializedObject;
                var path = defId.propertyPath;
                var dropdown = new ItemPickerDropdown(new AdvancedDropdownState(), pickedId =>
                {
                    so.Update();
                    so.FindProperty(path).stringValue = pickedId;
                    so.ApplyModifiedProperties();
                });
                dropdown.Show(btnRect);
            }
        }
    }
}
