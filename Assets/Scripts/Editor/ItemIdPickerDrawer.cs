using Constants;
using State;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace Editor
{
    // Draws any string field tagged with [ItemIdPicker(slot)] as a searchable,
    // category-grouped dropdown over ItemDefinition.Registry (reusing ItemPickerDropdown),
    // filtered to the given armor slot plus a "(None)" option. Keeps the stored value as
    // the item id — no more typing raw ids into bot equipment pools.
    [CustomPropertyDrawer(typeof(ItemIdPickerAttribute))]
    public class ItemIdPickerDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (property.propertyType != SerializedPropertyType.String)
            {
                EditorGUI.PropertyField(position, property, label);
                return;
            }

            var attr = (ItemIdPickerAttribute)attribute;
            EditorGUI.BeginProperty(position, label, property);

            var ctrlRect = EditorGUI.PrefixLabel(position, label);

            string id = property.stringValue;
            var def = string.IsNullOrEmpty(id) ? null : ItemDefinition.Get(id);
            string btnLabel = def != null
                ? $"{def.DisplayName}  ({def.Id})"
                : string.IsNullOrEmpty(id) ? "(None)" : $"⚠ Unknown id: {id}";

            if (EditorGUI.DropdownButton(ctrlRect, new GUIContent(btnLabel), FocusType.Keyboard))
            {
                var so = property.serializedObject;
                var path = property.propertyPath;
                System.Func<ItemDefinition, bool> filter = null;
                if (attr.HasSlotFilter)
                {
                    var slot = attr.Slot;
                    filter = d => (d.AllowedSlots & slot) != 0;
                }
                else if (attr.HasCategoryFilter)
                {
                    var category = attr.Category;
                    filter = d => d.Category == category;
                }

                var dropdown = new ItemPickerDropdown(
                    new AdvancedDropdownState(),
                    pickedId =>
                    {
                        so.Update();
                        so.FindProperty(path).stringValue = pickedId;
                        so.ApplyModifiedProperties();
                    },
                    filter: filter,
                    includeNone: true);
                dropdown.Show(ctrlRect);
            }

            EditorGUI.EndProperty();
        }
    }
}
