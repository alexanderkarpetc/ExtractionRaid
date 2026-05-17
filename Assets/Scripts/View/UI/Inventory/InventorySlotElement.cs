using Adapters;
using State;
using Systems;
using UnityEngine;
using UnityEngine.UIElements;

namespace View.UI.Inventory
{
    /// <summary>
    /// One inventory slot — backpack OR equipment, distinguished by USS class.
    /// Read-only render at Stage 1 — drag/drop wired up у Stage 2.
    ///
    /// Mirrors the data shape of the legacy <c>InventorySlotView</c> / <c>EquipmentSlotView</c>:
    /// item display name (via <see cref="WeaponDisplayName"/>), stack count or armor rating,
    /// durability bar для armor, optional hotbar key badge. Items з category Quest
    /// show a small quest marker dot.
    /// </summary>
    public class InventorySlotElement : VisualElement
    {
        public enum SlotKind { Backpack, Equipment }
        public enum SlotSource { Player, Loot, Floor, Stash }

        public SlotKind Kind { get; }

        /// <summary>
        /// Which source this slot represents. <see cref="SlotSource.Player"/>
        /// for the main inventory window; the others tag floating sub-panels.
        /// </summary>
        public SlotSource Source { get; set; } = SlotSource.Player;

        /// <summary>
        /// Only meaningful when <see cref="Source"/> is <see cref="SlotSource.Loot"/>
        /// — points at the lootable container whose Inventory backs this slot.
        /// </summary>
        public EId SourceLootableId { get; set; }

        public InventorySlotRef SlotRef { get; private set; }
        public ItemState CurrentItem { get; private set; }

        // Right-pane only: index into the active source (loot container backpack,
        // floor items list, or stash list). -1 для player-pane slots.
        public int RightIndex { get; set; } = -1;

        readonly Label _name;
        readonly Label _resource;
        readonly Label _quickSlotKey;
        readonly Label _emptyLabel;
        readonly VisualElement _quest;
        readonly VisualElement _durabilityRoot;
        readonly VisualElement _durabilityFill;

        public InventorySlotElement(SlotKind kind, string emptyPlaceholder = "")
        {
            Kind = kind;
            AddToClassList("inv-slot");
            AddToClassList(kind == SlotKind.Backpack ? "inv-slot--bp" : "inv-slot--eq");

            _emptyLabel = new Label(emptyPlaceholder);
            _emptyLabel.AddToClassList("inv-slot__empty");
            _emptyLabel.pickingMode = PickingMode.Ignore;
            Add(_emptyLabel);

            _name = new Label();
            _name.AddToClassList("inv-slot__name");
            _name.pickingMode = PickingMode.Ignore;
            Add(_name);

            _resource = new Label();
            _resource.AddToClassList("inv-slot__resource");
            _resource.pickingMode = PickingMode.Ignore;
            Add(_resource);

            _quickSlotKey = new Label();
            _quickSlotKey.AddToClassList("inv-slot__key");
            _quickSlotKey.pickingMode = PickingMode.Ignore;
            Add(_quickSlotKey);

            _quest = new VisualElement();
            _quest.AddToClassList("inv-slot__quest");
            _quest.pickingMode = PickingMode.Ignore;
            Add(_quest);

            _durabilityRoot = new VisualElement();
            _durabilityRoot.AddToClassList("inv-slot__durability");
            _durabilityRoot.pickingMode = PickingMode.Ignore;
            _durabilityFill = new VisualElement();
            _durabilityFill.AddToClassList("inv-slot__durability-fill");
            _durabilityFill.pickingMode = PickingMode.Ignore;
            _durabilityRoot.Add(_durabilityFill);
            Add(_durabilityRoot);
        }

        public void Bind(InventorySlotRef slotRef, ItemState item, int quickSlotKey,
                         ICoreDefinitionRegistry registry)
        {
            SlotRef = slotRef;
            CurrentItem = item;

            if (item == null)
            {
                _emptyLabel.style.display = DisplayStyle.Flex;
                _name.style.display = DisplayStyle.None;
                _resource.text = "";
                _quickSlotKey.style.display = DisplayStyle.None;
                _quest.style.display = DisplayStyle.None;
                _durabilityRoot.style.display = DisplayStyle.None;
                return;
            }

            _emptyLabel.style.display = DisplayStyle.None;
            _name.style.display = DisplayStyle.Flex;
            _name.text = WeaponDisplayName.For(item, registry);

            UpdateResource(item);
            UpdateDurability(item);
            UpdateQuickSlotBadge(quickSlotKey);
            UpdateQuestMarker(item);
        }

        void UpdateResource(ItemState item)
        {
            var def = item.Definition;
            bool hasArmor = def != null && def.ArmorPoints > 0f;

            if (hasArmor)
            {
                float max = item.HasCustomDurability ? item.MaxDurability : def.MaxDurability;
                float cur = item.HasCustomDurability ? item.CurrentDurability : max;
                _resource.text = max > 0f ? $"{cur:0}/{max:0}" : "";
            }
            else if (item.StackCount > 1)
            {
                _resource.text = $"x{item.StackCount}";
            }
            else
            {
                _resource.text = "";
            }
        }

        void UpdateDurability(ItemState item)
        {
            bool show = item.Definition != null && item.Definition.ArmorPoints > 0f;
            _durabilityRoot.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
            if (!show) return;

            var def = item.Definition;
            float max = item.HasCustomDurability ? item.MaxDurability : def.MaxDurability;
            float cur = item.HasCustomDurability ? item.CurrentDurability : max;
            float pct = max > 0f ? Mathf.Clamp01(cur / max) : 0f;

            _durabilityFill.style.width = new Length(pct * 100f, LengthUnit.Percent);

            // Green > yellow > red thresholds — mirrors legacy InventorySlotView.
            Color c;
            if (pct >= 0.7f)      c = new Color(0.20f, 0.80f, 0.20f, 0.9f);
            else if (pct >= 0.4f) c = new Color(0.90f, 0.75f, 0.10f, 0.9f);
            else                  c = new Color(0.90f, 0.20f, 0.15f, 0.9f);
            _durabilityFill.style.backgroundColor = c;
        }

        void UpdateQuickSlotBadge(int quickSlotKey)
        {
            bool show = quickSlotKey >= 0;
            _quickSlotKey.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
            if (show) _quickSlotKey.text = quickSlotKey.ToString();
        }

        void UpdateQuestMarker(ItemState item)
        {
            bool show = item?.Definition?.Category == ItemCategory.Quest;
            _quest.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
        }

        // ── Drag visuals (Stage 2) ────────────────────────────

        public void SetDragOver(bool valid, bool hovering)
        {
            RemoveFromClassList("inv-slot-drag-valid");
            RemoveFromClassList("inv-slot-drag-invalid");
            if (!hovering) return;
            AddToClassList(valid ? "inv-slot-drag-valid" : "inv-slot-drag-invalid");
        }
    }
}
