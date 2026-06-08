using System;
using System.Linq;
using State;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace Editor
{
    // Shared AdvancedDropdown picker over ItemDefinition.Registry, grouped by
    // ItemCategory. Built-in search bar handles fuzzy lookup so we never type or
    // copy raw ids. Used by DevCheatsWindow ("Give Item") and the
    // ContainerTypeConfigAsset inspector (drop pool entries).
    public class ItemPickerDropdown : AdvancedDropdown
    {
        readonly Action<string> _onPick;

        public ItemPickerDropdown(AdvancedDropdownState state, Action<string> onPick)
            : base(state)
        {
            _onPick = onPick;
            minimumSize = new Vector2(320, 420);
        }

        protected override AdvancedDropdownItem BuildRoot()
        {
            var root = new AdvancedDropdownItem("Items");

            var byCategory = ItemDefinition.Registry.Values
                .GroupBy(d => d.Category)
                .OrderBy(g => g.Key.ToString());

            foreach (var group in byCategory)
            {
                var groupItem = new AdvancedDropdownItem(group.Key.ToString());
                foreach (var def in group.OrderBy(d => d.DisplayName))
                    groupItem.AddChild(new ItemEntry(def));
                root.AddChild(groupItem);
            }

            return root;
        }

        protected override void ItemSelected(AdvancedDropdownItem item)
        {
            if (item is ItemEntry entry)
                _onPick?.Invoke(entry.ItemId);
        }

        class ItemEntry : AdvancedDropdownItem
        {
            public string ItemId { get; }
            public ItemEntry(ItemDefinition def)
                : base($"{def.DisplayName}  ({def.Id})")
            {
                ItemId = def.Id;
            }
        }
    }
}
