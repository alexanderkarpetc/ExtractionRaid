using System.Collections.Generic;
using Constants;
using State;
using UnityEditor;
using UnityEngine;

namespace Editor
{
    // Custom inspector for ContainerTypeConfigAsset. Two clearly separated halves: the
    // hardcoded contents (always spawn) and the weighted category pool (rolled). Each pool
    // entry shows a live preview of what ItemBalance would let out of that bucket, so a
    // designer can see the consequence of a category choice without opening the balance asset.
    [CustomEditor(typeof(ContainerTypeConfigAsset))]
    public class ContainerTypeConfigAssetEditor : UnityEditor.Editor
    {
        SerializedProperty _typeId;
        SerializedProperty _displayName;
        SerializedProperty _slotCount;
        SerializedProperty _guaranteedDrops;
        SerializedProperty _minDrops;
        SerializedProperty _maxDrops;
        SerializedProperty _pool;

        readonly Dictionary<int, bool> _previewOpen = new();

        void OnEnable()
        {
            _typeId = serializedObject.FindProperty("_typeId");
            _displayName = serializedObject.FindProperty("_displayName");
            _slotCount = serializedObject.FindProperty("_slotCount");
            _guaranteedDrops = serializedObject.FindProperty("_guaranteedDrops");
            _minDrops = serializedObject.FindProperty("_minDrops");
            _maxDrops = serializedObject.FindProperty("_maxDrops");
            _pool = serializedObject.FindProperty("_pool");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.HelpBox(
                "This asset says WHAT KIND of loot the container holds. Which item comes out of " +
                "a category — and how many units one drop is worth — comes from " +
                "Resources/Configs/ItemBalance. Use Guaranteed Drops for loot that must always " +
                "be there.", MessageType.Info);

            EditorGUILayout.LabelField("Identity", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_typeId);
            EditorGUILayout.PropertyField(_displayName);

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Capacity", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_slotCount);

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Guaranteed Drops (always spawn)", EditorStyles.boldLabel);
            DrawGuaranteedDrops();

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Drop Count (random rolls)", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_minDrops);
            EditorGUILayout.PropertyField(_maxDrops);
            if (_maxDrops.intValue < _minDrops.intValue)
                EditorGUILayout.HelpBox("Max Drops is below Min Drops — Min wins at runtime.",
                    MessageType.Warning);

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Category Pool", EditorStyles.boldLabel);
            DrawPool();

            serializedObject.ApplyModifiedProperties();
        }

        void DrawGuaranteedDrops()
        {
            for (int i = 0; i < _guaranteedDrops.arraySize; i++)
            {
                var entry = _guaranteedDrops.GetArrayElementAtIndex(i);
                var defId = entry.FindPropertyRelative("definitionId");
                var fromBalance = entry.FindPropertyRelative("countFromBalance");
                var minCount = entry.FindPropertyRelative("minCount");
                var maxCount = entry.FindPropertyRelative("maxCount");
                var preset = entry.FindPropertyRelative("weaponPreset");

                bool isPreset = preset.objectReferenceValue != null;

                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        using (new EditorGUI.DisabledScope(isPreset))
                            EditorGUILayout.PropertyField(defId, new GUIContent("Item"));

                        if (GUILayout.Button("✕", GUILayout.Width(24)))
                        {
                            _guaranteedDrops.DeleteArrayElementAtIndex(i);
                            break;
                        }
                    }

                    EditorGUILayout.PropertyField(preset,
                        new GUIContent("Weapon Preset", "Optional. When set, this drop spawns the " +
                            "assembled weapon from the preset instead of the item above."));

                    using (new EditorGUI.DisabledScope(isPreset))
                    {
                        EditorGUILayout.PropertyField(fromBalance, new GUIContent("Count From Balance"));

                        using (new EditorGUI.DisabledScope(fromBalance.boolValue))
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            EditorGUILayout.LabelField("Count", GUILayout.Width(44));
                            EditorGUILayout.PropertyField(minCount, GUIContent.none, GUILayout.Width(50));
                            EditorGUILayout.LabelField("–", GUILayout.Width(10));
                            EditorGUILayout.PropertyField(maxCount, GUIContent.none, GUILayout.Width(50));
                            GUILayout.FlexibleSpace();
                        }

                        if (fromBalance.boolValue && !string.IsNullOrEmpty(defId.stringValue))
                        {
                            ItemBalanceAsset.DropCountRangeOf(defId.stringValue, out int bmin, out int bmax);
                            EditorGUILayout.LabelField(" ", $"ItemBalance: {bmin}–{bmax}",
                                EditorStyles.miniLabel);
                        }
                    }
                }
            }

            EditorGUILayout.Space(2);
            if (GUILayout.Button("+ Add Guaranteed Drop"))
            {
                int idx = _guaranteedDrops.arraySize;
                _guaranteedDrops.InsertArrayElementAtIndex(idx);
                var entry = _guaranteedDrops.GetArrayElementAtIndex(idx);
                // InsertArrayElement copies the previous element — reset to defaults.
                entry.FindPropertyRelative("definitionId").stringValue = "";
                entry.FindPropertyRelative("countFromBalance").boolValue = false;
                entry.FindPropertyRelative("minCount").intValue = 1;
                entry.FindPropertyRelative("maxCount").intValue = 1;
                entry.FindPropertyRelative("weaponPreset").objectReferenceValue = null;
            }
        }

        void DrawPool()
        {
            float totalWeight = 0f;
            for (int i = 0; i < _pool.arraySize; i++)
                totalWeight += _pool.GetArrayElementAtIndex(i).FindPropertyRelative("weight").floatValue;

            for (int i = 0; i < _pool.arraySize; i++)
            {
                var entry = _pool.GetArrayElementAtIndex(i);
                var kind = entry.FindPropertyRelative("kind");
                var category = entry.FindPropertyRelative("category");
                var defId = entry.FindPropertyRelative("definitionId");
                var weight = entry.FindPropertyRelative("weight");

                bool isCategory = kind.enumValueIndex == (int)ContainerTypeConfigAsset.PoolEntry.EntryKind.Category;

                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.PropertyField(kind, GUIContent.none, GUILayout.Width(80));
                        if (isCategory)
                            EditorGUILayout.PropertyField(category, GUIContent.none);
                        else
                            EditorGUILayout.PropertyField(defId, GUIContent.none);

                        if (GUILayout.Button("✕", GUILayout.Width(24)))
                        {
                            _pool.DeleteArrayElementAtIndex(i);
                            break;
                        }
                    }

                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.LabelField("Weight", GUILayout.Width(48));
                        EditorGUILayout.PropertyField(weight, GUIContent.none, GUILayout.Width(60));
                        float share = totalWeight > 0f ? weight.floatValue / totalWeight : 0f;
                        EditorGUILayout.LabelField($"{share:P0} of rolls", EditorStyles.miniLabel);
                    }

                    if (isCategory)
                        DrawCategoryPreview(i, (LootCategory)category.enumValueIndex);
                    else if (!string.IsNullOrEmpty(defId.stringValue))
                        DrawItemBalanceLine(defId.stringValue);
                }
            }

            EditorGUILayout.Space(2);
            if (GUILayout.Button("+ Add Pool Entry"))
            {
                int idx = _pool.arraySize;
                _pool.InsertArrayElementAtIndex(idx);
                var entry = _pool.GetArrayElementAtIndex(idx);
                entry.FindPropertyRelative("kind").enumValueIndex =
                    (int)ContainerTypeConfigAsset.PoolEntry.EntryKind.Category;
                entry.FindPropertyRelative("category").enumValueIndex = (int)LootCategory.Materials;
                entry.FindPropertyRelative("definitionId").stringValue = "";
                entry.FindPropertyRelative("weight").floatValue = 1f;
            }
        }

        // What ItemBalance would actually let out of this bucket, most likely first. Zero-weight
        // rows are excluded from loot entirely, so they're called out as such.
        void DrawCategoryPreview(int index, LootCategory category)
        {
            var candidates = LootConstants.CandidatesFor(category);
            if (candidates.Count == 0)
            {
                EditorGUILayout.HelpBox("No items resolve to this category.", MessageType.Warning);
                return;
            }

            var rows = new List<(string id, float weight)>(candidates.Count);
            float total = 0f;
            foreach (var def in candidates)
            {
                float w = ItemBalanceAsset.DropWeightOf(def.Id);
                rows.Add((def.Id, w));
                total += w;
            }
            rows.Sort((a, b) => b.weight.CompareTo(a.weight));

            _previewOpen.TryGetValue(index, out bool open);
            open = EditorGUILayout.Foldout(open,
                $"ItemBalance says: {rows.Count} items", true);
            _previewOpen[index] = open;
            if (!open) return;

            using (new EditorGUI.IndentLevelScope())
                foreach (var row in rows)
                {
                    if (row.weight <= 0f)
                    {
                        EditorGUILayout.LabelField(Label(row.id), "never (weight 0)", EditorStyles.miniLabel);
                        continue;
                    }
                    ItemBalanceAsset.DropCountRangeOf(row.id, out int min, out int max);
                    string chance = total > 0f ? $"{row.weight / total:P1}" : "—";
                    string count = min == max ? $"×{min}" : $"×{min}–{max}";
                    EditorGUILayout.LabelField(Label(row.id), $"{chance}   {count}",
                        EditorStyles.miniLabel);
                }
        }

        static void DrawItemBalanceLine(string id)
        {
            ItemBalanceAsset.DropCountRangeOf(id, out int min, out int max);
            string count = min == max ? $"×{min}" : $"×{min}–{max}";
            float w = ItemBalanceAsset.DropWeightOf(id);
            string note = w <= 0f
                ? $"ItemBalance: {count}  (drop weight 0 — only reachable as a named entry)"
                : $"ItemBalance: {count}";
            EditorGUILayout.LabelField(" ", note, EditorStyles.miniLabel);
        }

        static string Label(string id) => ItemDefinition.Get(id)?.DisplayName ?? id;
    }
}
