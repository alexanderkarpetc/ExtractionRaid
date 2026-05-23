using System;
using System.Collections.Generic;
using Quests;
using UnityEditor;
using UnityEngine;

namespace Editor.Quests
{
    [CustomEditor(typeof(QuestDefinition))]
    public class QuestDefinitionEditor : UnityEditor.Editor
    {
        static readonly (string Label, Func<QuestTask> Factory)[] TaskTypes =
        {
            ("Find and Transfer", () => new FindAndTransferTask()),
            ("Kill Enemy", () => new KillEnemyTask()),
            ("Find Place", () => new FindPlaceTask()),
            ("Extract", () => new ExtractTask()),
            ("Craft", () => new CraftTask()),
            ("Find Item", () => new FindItemTask()),
            ("Sell Items", () => new SellItemsTask()),
            ("Upgrade Building", () => new UpgradeBuildingTask()),
        };

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            DrawPropertiesExcluding(serializedObject, "Tasks", "m_Script");

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Tasks", EditorStyles.boldLabel);

            var quest = (QuestDefinition)target;

            for (int i = 0; i < quest.Tasks.Count; i++)
            {
                var task = quest.Tasks[i];
                if (task == null)
                {
                    quest.Tasks.RemoveAt(i--);
                    EditorUtility.SetDirty(target);
                    continue;
                }

                EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"[{task.TaskType}]", EditorStyles.boldLabel);
                if (GUILayout.Button("X", GUILayout.Width(22)))
                {
                    Undo.RecordObject(target, "Remove Quest Task");
                    quest.Tasks.RemoveAt(i--);
                    EditorUtility.SetDirty(target);
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.EndVertical();
                    continue;
                }
                EditorGUILayout.EndHorizontal();

                DrawTaskFields(quest.Tasks, i);

                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.Space(4);

            if (EditorGUILayout.DropdownButton(new GUIContent("Add Task..."), FocusType.Keyboard))
            {
                var menu = new GenericMenu();
                foreach (var (label, factory) in TaskTypes)
                {
                    var f = factory;
                    menu.AddItem(new GUIContent(label), false, () =>
                    {
                        Undo.RecordObject(target, "Add Quest Task");
                        quest.Tasks.Add(f());
                        EditorUtility.SetDirty(target);
                    });
                }
                menu.ShowAsContext();
            }

            serializedObject.ApplyModifiedProperties();
        }

        void DrawTaskFields(List<QuestTask> tasks, int index)
        {
            var tasksProp = serializedObject.FindProperty("Tasks");
            if (index >= tasksProp.arraySize) return;
            var taskProp = tasksProp.GetArrayElementAtIndex(index);

            var iter = taskProp.Copy();
            var end = iter.GetEndProperty();
            bool entered = iter.NextVisible(true);

            while (entered && !SerializedProperty.EqualContents(iter, end))
            {
                EditorGUILayout.PropertyField(iter, true);
                entered = iter.NextVisible(false);
            }
        }
    }
}
