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
        readonly Func<ItemDefinition, bool> _filter;
        readonly bool _includeNone;

        /// <param name="filter">Optional predicate — only matching definitions are listed
        /// (e.g. armor-slot filtering). Null = all items.</param>
        /// <param name="includeNone">When true, a top-level "(None)" entry is added that
        /// picks an empty id — used where "no item" is a valid choice.</param>
        public ItemPickerDropdown(AdvancedDropdownState state, Action<string> onPick,
            Func<ItemDefinition, bool> filter = null, bool includeNone = false)
            : base(state)
        {
            _onPick = onPick;
            _filter = filter;
            _includeNone = includeNone;
            minimumSize = new Vector2(320, 420);
        }

        protected override AdvancedDropdownItem BuildRoot()
        {
            var root = new AdvancedDropdownItem("Items");

            if (_includeNone)
                root.AddChild(new ItemEntry("(None)", ""));

            var values = ItemDefinition.Registry.Values.AsEnumerable();
            if (_filter != null)
                values = values.Where(_filter);

            var byCategory = values
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

            public ItemEntry(string label, string id)
                : base(label)
            {
                ItemId = id;
            }
        }
    }
}
