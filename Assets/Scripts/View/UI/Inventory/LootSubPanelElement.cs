using System;
using System.Collections.Generic;
using Constants;
using State;
using UnityEngine.UIElements;

namespace View.UI.Inventory
{
    /// <summary>
    /// One floating sub-panel next to the main inventory window. Represents a
    /// single loot source — nearby lootable container, corpse, floor items, or
    /// hideout stash. Owns its own slot pool grown on demand; slots are wired
    /// to the host <see cref="InventoryWindow"/>'s drag/drop manager so the
    /// drag pipeline sees them like any other slot.
    ///
    /// Source identification is by the Dictionary key in <c>InventoryWindow._subPanels</c>
    /// — sub-panels are reconciled between refreshes without recreating
    /// elements (so an active drag survives reconcile).
    /// </summary>
    public class LootSubPanelElement : VisualElement
    {
        public InventorySlotElement.SlotSource SlotSource { get; private set; }
            = InventorySlotElement.SlotSource.Loot;
        public EId LootableId { get; private set; }

        readonly Label _title;
        readonly VisualElement _grid;
        readonly List<InventorySlotElement> _slots = new();
        readonly Action<InventorySlotElement> _wire;

        ItemIconRegistryAsset _iconRegistry;

        public IReadOnlyList<InventorySlotElement> Slots => _slots;

        public LootSubPanelElement(Action<InventorySlotElement> wireInteractions)
        {
            _wire = wireInteractions;
            AddToClassList("inv-subpanel");

            _title = new Label();
            _title.AddToClassList("inv-subpanel__title");
            Add(_title);

            var scroll = new ScrollView { mode = ScrollViewMode.Vertical };
            scroll.AddToClassList("inv-subpanel__scroll");
            scroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            scroll.verticalScrollerVisibility   = ScrollerVisibility.Auto;
            Add(scroll);

            _grid = new VisualElement();
            _grid.AddToClassList("inv-subpanel__grid");
            scroll.Add(_grid);
        }

        public void SetTitle(string title) => _title.text = title ?? string.Empty;

        public void SetIconRegistry(ItemIconRegistryAsset registry)
        {
            _iconRegistry = registry;
            foreach (var s in _slots) s.SetIconRegistry(registry);
        }

        public void SetSourceMeta(InventorySlotElement.SlotSource source, EId lootableId)
        {
            SlotSource = source;
            LootableId = lootableId;
            foreach (var s in _slots)
            {
                s.Source = source;
                s.SourceLootableId = lootableId;
            }
        }

        /// <summary>
        /// Ensure the pool has at least <paramref name="needed"/> slots. Extra
        /// slots beyond <paramref name="needed"/> are hidden (display: None).
        ///
        /// Column count is enforced purely by the grid's explicit width in
        /// `.inv-subpanel__grid` (6 × cell pitch, trailing margin included), so
        /// every cell keeps the same uniform right margin. The old per-6th-cell
        /// `.inv-slot--row-end` override desynced as soon as the scrollbar took
        /// width and the grid wrapped to 5 per row — leaving one glued-together
        /// pair in every row. Hidden slots (display: None) also don't consume a
        /// column, which that index-based scheme could not account for.
        /// </summary>
        public void EnsureSlotCount(int needed)
        {
            while (_slots.Count < needed)
            {
                var s = new InventorySlotElement(InventorySlotElement.SlotKind.Backpack, "");
                s.Source = SlotSource;
                s.SourceLootableId = LootableId;
                _grid.Add(s);
                _slots.Add(s);
                _wire?.Invoke(s);
                if (_iconRegistry != null) s.SetIconRegistry(_iconRegistry);
            }

            for (int i = 0; i < _slots.Count; i++)
                _slots[i].style.display = i < needed ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }
}
