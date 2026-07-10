using System.Collections.Generic;
using Constants;
using UnityEditor;
using UnityEngine;

namespace Editor
{
    // Custom inspector for BotTypeConfigAsset. Draws the whole asset with the default
    // per-field layout, except the "Loadout" block (weapon + armor + equipment link),
    // which is shown conditionally on Equipment Source:
    //   • FromThisConfig      → inline Weapon + Armor fields (equipment link hidden)
    //   • RandomFromEquipment → only the Equipment config link (inline fields hidden)
    // so unused config never clutters the inspector.
    [CustomEditor(typeof(BotTypeConfigAsset))]
    public class BotTypeConfigAssetEditor : UnityEditor.Editor
    {
        // Fields owned by the Loadout block — drawn there, skipped in the normal pass.
        static readonly HashSet<string> LoadoutProps = new()
        {
            "_equipmentSource", "_equipment",
            "_payload", "_delivery", "_exotic", "_weaponRarity", "_magazineAmmo",
            "_helmetDefinitionId", "_bodyArmorDefinitionId",
        };

        // The Loadout block is injected where this (first) property would appear.
        const string AnchorProp = "_equipmentSource";

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            var it = serializedObject.GetIterator();
            bool enterChildren = true;
            while (it.NextVisible(enterChildren))
            {
                enterChildren = false;
                var name = it.name;

                if (name == "m_Script")
                {
                    using (new EditorGUI.DisabledScope(true))
                        EditorGUILayout.PropertyField(it);
                    continue;
                }

                if (name == AnchorProp)
                {
                    DrawLoadout();
                    continue;
                }

                if (LoadoutProps.Contains(name))
                    continue; // handled inside DrawLoadout

                EditorGUILayout.PropertyField(it, true);
            }

            serializedObject.ApplyModifiedProperties();
        }

        void DrawLoadout()
        {
            var source = serializedObject.FindProperty("_equipmentSource");

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Loadout", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(source, new GUIContent("Equipment Source"));

            EditorGUI.indentLevel++;
            if (source.enumValueIndex == (int)BotEquipmentSource.FromThisConfig)
            {
                EditorGUILayout.LabelField("Weapon", EditorStyles.miniBoldLabel);
                DrawProp("_payload");
                DrawProp("_delivery");
                DrawProp("_exotic");
                DrawProp("_weaponRarity");
                DrawProp("_magazineAmmo");

                EditorGUILayout.Space(2);
                EditorGUILayout.LabelField("Armor", EditorStyles.miniBoldLabel);
                DrawProp("_helmetDefinitionId");
                DrawProp("_bodyArmorDefinitionId");
            }
            else
            {
                DrawProp("_equipment");
            }
            EditorGUI.indentLevel--;
        }

        void DrawProp(string propName)
        {
            var p = serializedObject.FindProperty(propName);
            if (p != null) EditorGUILayout.PropertyField(p, true);
        }
    }
}
