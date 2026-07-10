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

        public ItemIdPickerAttribute(ItemSlotType slot)
        {
            Slot = slot;
        }
    }
}
