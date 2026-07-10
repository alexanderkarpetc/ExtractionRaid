using State;
using UnityEngine;

namespace Constants
{
    /// <summary>
    /// Marks a serialized string field as an <see cref="ItemDefinition"/> id, drawn in the
    /// Inspector as a searchable, category-grouped dropdown (the shared ItemPickerDropdown)
    /// instead of a raw text field. The list is filtered to items whose AllowedSlots include
    /// <see cref="Slot"/>, and an empty id ("(None)") is always selectable.
    ///
    /// Rendering lives in Editor/ItemIdPickerDrawer.cs; the stored value is still the item id.
    /// </summary>
    public class ItemIdPickerAttribute : PropertyAttribute
    {
        public readonly ItemSlotType Slot;
        public readonly ItemCategory Category;
        public readonly bool HasSlotFilter;
        public readonly bool HasCategoryFilter;

        /// <summary>Dropdown over every item.</summary>
        public ItemIdPickerAttribute() { }

        /// <summary>Dropdown filtered to items whose AllowedSlots include <paramref name="slot"/>.</summary>
        public ItemIdPickerAttribute(ItemSlotType slot)
        {
            Slot = slot;
            HasSlotFilter = true;
        }

        /// <summary>Dropdown filtered to items of the given <paramref name="category"/>.</summary>
        public ItemIdPickerAttribute(ItemCategory category)
        {
            Category = category;
            HasCategoryFilter = true;
        }
    }
}
