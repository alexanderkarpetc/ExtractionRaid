using System;
using System.Collections.Generic;
using Constants;
using State;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace Editor
{
    [CustomEditor(typeof(ItemBalanceAsset))]
    public class ItemBalanceAssetEditor : UnityEditor.Editor
    {
        SerializedProperty _entries;

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

                if (GUILayout.Button("Remove Stale"))
                    RemoveStale();

                if (GUILayout.Button("Sort by Category"))
                    SortByCategory();
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(new GUIContent("Seed Drop Counts",
                        "Fills unauthored (0) drop-count ranges with the stack-size derived " +
                        "default, so every row shows the number loot will actually use.")))
                    SeedDropCounts();
            }
        }

        // Generic weapon shells (ItemCategory.Weapon) are intentionally NOT balanced here —
        // a gun's price/rarity comes from its payload+delivery configuration, not the shell.
        static bool IsBalanced(ItemDefinition def) => def != null && def.Category != ItemCategory.Weapon;

        // Adds a row for every registry item missing from the table, seeding Price from the
        // item's intrinsic Value and DropWeight from the value-derived default. Existing rows
        // are left untouched so hand-tuned numbers survive a re-sync.
        void SyncFromRegistry()
        {
            var registry = ItemDefinition.Registry;
            var existing = new HashSet<string>();
            for (int i = 0; i < _entries.arraySize; i++)
                existing.Add(_entries.GetArrayElementAtIndex(i)
                    .FindPropertyRelative("DefinitionId").stringValue);

            int added = 0;
            foreach (var kv in registry)
            {
                if (!IsBalanced(kv.Value)) continue;
                if (existing.Contains(kv.Key)) continue;
                int idx = _entries.arraySize;
                _entries.InsertArrayElementAtIndex(idx);
                var e = _entries.GetArrayElementAtIndex(idx);
                e.FindPropertyRelative("DefinitionId").stringValue = kv.Key;
                e.FindPropertyRelative("Price").intValue = kv.Value.Value;
                e.FindPropertyRelative("DropWeight").floatValue =
                    ItemBalanceAsset.DefaultDropWeight(kv.Value.Value);
                ItemBalanceAsset.DefaultDropCountRange(kv.Key, out int cmin, out int cmax);
                e.FindPropertyRelative("MinDropCount").intValue = cmin;
                e.FindPropertyRelative("MaxDropCount").intValue = cmax;
                added++;
            }

            serializedObject.ApplyModifiedProperties();
            Debug.Log(added > 0
                ? $"[ItemBalance] Added {added} new item entries."
                : "[ItemBalance] Already up to date.");
        }

        // Drops rows whose id no longer resolves to a registry item (renamed/removed).
        void RemoveStale()
        {
            int removed = 0;
            for (int i = _entries.arraySize - 1; i >= 0; i--)
            {
                var id = _entries.GetArrayElementAtIndex(i)
                    .FindPropertyRelative("DefinitionId").stringValue;
                if (string.IsNullOrEmpty(id) || ItemDefinition.Get(id) == null)
                {
                    _entries.DeleteArrayElementAtIndex(i);
                    removed++;
                }
            }

            serializedObject.ApplyModifiedProperties();
            Debug.Log(removed > 0
                ? $"[ItemBalance] Removed {removed} stale entries."
                : "[ItemBalance] No stale entries.");
        }

        // Materializes the derived default into every row that hasn't authored a range yet, so
        // the table shows the real numbers loot uses instead of an implicit 0.
        void SeedDropCounts()
        {
            int seeded = 0;
            for (int i = 0; i < _entries.arraySize; i++)
            {
                var e = _entries.GetArrayElementAtIndex(i);
                var maxProp = e.FindPropertyRelative("MaxDropCount");
                if (maxProp.intValue > 0) continue;

                var id = e.FindPropertyRelative("DefinitionId").stringValue;
                ItemBalanceAsset.DefaultDropCountRange(id, out int min, out int max);
                e.FindPropertyRelative("MinDropCount").intValue = min;
                maxProp.intValue = max;
                seeded++;
            }

            serializedObject.ApplyModifiedProperties();
            Debug.Log(seeded > 0
                ? $"[ItemBalance] Seeded drop counts for {seeded} rows."
                : "[ItemBalance] Every row already has a drop-count range.");
        }

        void SortByCategory()
        {
            var snapshot = new List<(string id, int price, float weight, int minCount, int maxCount)>();
            for (int i = 0; i < _entries.arraySize; i++)
            {
                var e = _entries.GetArrayElementAtIndex(i);
                snapshot.Add((
                    e.FindPropertyRelative("DefinitionId").stringValue,
                    e.FindPropertyRelative("Price").intValue,
                    e.FindPropertyRelative("DropWeight").floatValue,
                    e.FindPropertyRelative("MinDropCount").intValue,
                    e.FindPropertyRelative("MaxDropCount").intValue
                ));
            }

            snapshot.Sort((a, b) =>
            {
                var defA = string.IsNullOrEmpty(a.id) ? null : ItemDefinition.Get(a.id);
                var defB = string.IsNullOrEmpty(b.id) ? null : ItemDefinition.Get(b.id);
                int catA = defA != null ? (int)defA.Category : int.MaxValue;
                int catB = defB != null ? (int)defB.Category : int.MaxValue;
                int cmp = catA.CompareTo(catB);
                if (cmp != 0) return cmp;
                string nameA = defA?.DisplayName ?? a.id ?? "";
                string nameB = defB?.DisplayName ?? b.id ?? "";
                return string.Compare(nameA, nameB, StringComparison.OrdinalIgnoreCase);
            });

            for (int i = 0; i < snapshot.Count; i++)
            {
                var e = _entries.GetArrayElementAtIndex(i);
                e.FindPropertyRelative("DefinitionId").stringValue = snapshot[i].id;
                e.FindPropertyRelative("Price").intValue = snapshot[i].price;
                e.FindPropertyRelative("DropWeight").floatValue = snapshot[i].weight;
                e.FindPropertyRelative("MinDropCount").intValue = snapshot[i].minCount;
                e.FindPropertyRelative("MaxDropCount").intValue = snapshot[i].maxCount;
            }

            serializedObject.ApplyModifiedProperties();
            Debug.Log("[ItemBalance] Sorted by category.");
        }

        void DrawStats()
        {
            int total = _entries.arraySize;
            int registryCount = 0;
            foreach (var def in ItemDefinition.Registry.Values)
                if (IsBalanced(def)) registryCount++;
            var style = total < registryCount ? EditorStyles.boldLabel : EditorStyles.label;
            EditorGUILayout.LabelField($"Entries: {total} / {registryCount} balanced items", style);
            if (total < registryCount)
                EditorGUILayout.HelpBox("Some items are missing — click \"Sync from ItemDefinition\".",
                    MessageType.Warning);
        }

        void DrawEntriesByCategory()
        {
            var groups = new Dictionary<ItemCategory, List<int>>();
            var unknownIndices = new List<int>();

            for (int i = 0; i < _entries.arraySize; i++)
            {
                var id = _entries.GetArrayElementAtIndex(i)
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
                e.FindPropertyRelative("Price").intValue = 10;
                e.FindPropertyRelative("DropWeight").floatValue = ItemBalanceAsset.DefaultDropWeight(10);
                e.FindPropertyRelative("MinDropCount").intValue = 1;
                e.FindPropertyRelative("MaxDropCount").intValue = 1;
            }
        }

        void DrawCategoryGroup(string label, List<int> indices)
        {
            bool hasKey = Enum.TryParse(label, out ItemCategory cat);

            if (!_foldouts.TryGetValue(cat, out bool open))
                open = true;

            string header = $"{label}  ({indices.Count})";
            bool newOpen = EditorGUILayout.Foldout(open, header, true, EditorStyles.foldoutHeader);
            if (hasKey) _foldouts[cat] = newOpen;
            if (!newOpen) return;

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(4);
                EditorGUILayout.LabelField("Item", EditorStyles.miniLabel);
                EditorGUILayout.LabelField("Price", EditorStyles.miniLabel, GUILayout.Width(64));
                EditorGUILayout.LabelField("Drop Wt", EditorStyles.miniLabel, GUILayout.Width(64));
                EditorGUILayout.LabelField("Count", EditorStyles.miniLabel, GUILayout.Width(104));
                GUILayout.Space(28);
            }

            bool deleted = false;
            foreach (int i in indices)
            {
                if (deleted) break;
                var entry = _entries.GetArrayElementAtIndex(i);
                var defIdProp = entry.FindPropertyRelative("DefinitionId");
                var priceProp = entry.FindPropertyRelative("Price");
                var weightProp = entry.FindPropertyRelative("DropWeight");
                var minCountProp = entry.FindPropertyRelative("MinDropCount");
                var maxCountProp = entry.FindPropertyRelative("MaxDropCount");

                using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
                {
                    DrawItemPicker(defIdProp);
                    EditorGUILayout.PropertyField(priceProp, GUIContent.none, GUILayout.Width(64));
                    EditorGUILayout.PropertyField(weightProp, GUIContent.none, GUILayout.Width(64));

                    // Drop count: how many units one roll of this item is worth. 0/0 = derived
                    // from MaxStackSize (shown as a hint so the row never reads as "nothing").
                    EditorGUILayout.PropertyField(minCountProp, GUIContent.none, GUILayout.Width(44));
                    EditorGUILayout.LabelField("–", GUILayout.Width(10));
                    EditorGUILayout.PropertyField(maxCountProp, GUIContent.none, GUILayout.Width(44));

                    if (GUILayout.Button("✕", GUILayout.Width(24)))
                    {
                        _entries.DeleteArrayElementAtIndex(i);
                        deleted = true;
                    }
                }

                if (deleted) break;

                if (maxCountProp.intValue <= 0 && !string.IsNullOrEmpty(defIdProp.stringValue))
                {
                    ItemBalanceAsset.DefaultDropCountRange(defIdProp.stringValue, out int dmin, out int dmax);
                    EditorGUILayout.LabelField(" ", $"count auto: {dmin}–{dmax}", EditorStyles.miniLabel);
                }
            }
        }

        static void DrawItemPicker(SerializedProperty defIdProp)
        {
            string id = defIdProp.stringValue;
            var def = string.IsNullOrEmpty(id) ? null : ItemDefinition.Get(id);
            string label = def != null
                ? $"{def.DisplayName}  ({def.Id})"
                : string.IsNullOrEmpty(id) ? "Select item…" : $"⚠ Unknown: {id}";

            var rect = GUILayoutUtility.GetRect(new GUIContent(label), EditorStyles.popup,
                GUILayout.ExpandWidth(true));
            if (EditorGUI.DropdownButton(rect, new GUIContent(label), FocusType.Keyboard))
            {
                var so = defIdProp.serializedObject;
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
