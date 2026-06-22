using Adapters;
using Constants;
using State;
using Systems;
using UnityEngine;
using UnityEngine.UIElements;
using View.UI.Attachments;

namespace View.UI.Inventory
{
    /// <summary>
    /// One inventory slot — backpack OR equipment, distinguished by USS class.
    /// Renders item display name (via <see cref="WeaponDisplayName"/>),
    /// stack count or armor rating, durability bar для armor, optional hotbar
    /// key badge. Items з category Quest show a small quest marker dot.
    /// Drag/drop manipulator is wired externally by <see cref="InventoryWindow"/>.
    /// </summary>
    public class InventorySlotElement : VisualElement
    {
        public enum SlotKind { Backpack, Equipment }
        public enum SlotSource { Player, Loot, Floor, Stash }

        // Pre-allocated badge labels for the 7 quick-slot keys (3-9). Avoids
        // a per-frame int.ToString allocation у UpdateQuickSlotBadge.
        static readonly string[] QuickSlotKeyLabels =
        {
            "3", "4", "5", "6", "7", "8", "9",
        };

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
        readonly Label _priceBadge;
        readonly VisualElement _icon;
        readonly VisualElement _quest;
        readonly VisualElement _durabilityRoot;
        readonly VisualElement _durabilityFill;
        readonly VisualElement _rarityTl;
        readonly VisualElement _rarityBr;
        readonly VisualElement _modPips;

        ItemIconRegistryAsset _iconRegistry;

        public InventorySlotElement(SlotKind kind, string emptyPlaceholder = "")
        {
            Kind = kind;
            AddToClassList("inv-slot");
            AddToClassList(kind == SlotKind.Backpack ? "inv-slot--bp" : "inv-slot--eq");

            _emptyLabel = new Label(emptyPlaceholder);
            _emptyLabel.AddToClassList("inv-slot__empty");
            _emptyLabel.pickingMode = PickingMode.Ignore;
            Add(_emptyLabel);

            _icon = new VisualElement();
            _icon.AddToClassList("inv-slot__icon");
            _icon.pickingMode = PickingMode.Ignore;
            _icon.style.display = DisplayStyle.None;
            Add(_icon);

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

            _priceBadge = new Label();
            _priceBadge.AddToClassList("inv-slot__price");
            _priceBadge.pickingMode = PickingMode.Ignore;
            _priceBadge.style.display = DisplayStyle.None;
            Add(_priceBadge);

            _rarityTl = new VisualElement();
            _rarityTl.AddToClassList("inv-slot__rarity-tl");
            _rarityTl.pickingMode = PickingMode.Ignore;
            _rarityTl.style.display = DisplayStyle.None;
            Add(_rarityTl);

            _rarityBr = new VisualElement();
            _rarityBr.AddToClassList("inv-slot__rarity-br");
            _rarityBr.pickingMode = PickingMode.Ignore;
            _rarityBr.style.display = DisplayStyle.None;
            Add(_rarityBr);

            _modPips = new VisualElement();
            _modPips.AddToClassList("inv-slot__mod-pips");
            _modPips.pickingMode = PickingMode.Ignore;
            _modPips.style.display = DisplayStyle.None;
            Add(_modPips);
        }

        public void SetIconRegistry(ItemIconRegistryAsset registry) => _iconRegistry = registry;

        public void SetShopPrice(int price)
        {
            if (price < 0 || _priceBadge == null)
            {
                if (_priceBadge != null) _priceBadge.style.display = DisplayStyle.None;
                return;
            }
            _priceBadge.text = price + "¢";
            _priceBadge.style.display = DisplayStyle.Flex;
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
                _priceBadge.style.display = DisplayStyle.None;
                _icon.style.display = DisplayStyle.None;
                UpdateRarityFrame(null);
                UpdateModPips(null);
                return;
            }

            _emptyLabel.style.display = DisplayStyle.None;
            _name.style.display = DisplayStyle.Flex;
            _name.text = WeaponDisplayName.For(item, registry);

            UpdateResource(item);
            UpdateDurability(item);
            UpdateQuickSlotBadge(quickSlotKey);
            UpdateQuestMarker(item);
            UpdateRarityFrame(item);
            UpdateModPips(item);
            UpdateIcon(item);
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
            else if (item.IsResourceItem)
            {
                _resource.text = $"{item.CurrentResource}/{item.MaxResource}";
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

            // Green > yellow > red thresholds for armor durability bar.
            Color c;
            if (pct >= 0.7f)      c = new Color(0.20f, 0.80f, 0.20f, 0.9f);
            else if (pct >= 0.4f) c = new Color(0.90f, 0.75f, 0.10f, 0.9f);
            else                  c = new Color(0.90f, 0.20f, 0.15f, 0.9f);
            _durabilityFill.style.backgroundColor = c;
        }

        void UpdateQuickSlotBadge(int quickSlotKey)
        {
            // quickSlotKey is the displayed key number (3..9) per
            // InventoryState.QuickSlotKeyOffset.
            bool show = quickSlotKey >= 0;
            _quickSlotKey.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
            if (!show) return;
            int idx = quickSlotKey - 3;
            _quickSlotKey.text = (idx >= 0 && idx < QuickSlotKeyLabels.Length)
                ? QuickSlotKeyLabels[idx]
                : quickSlotKey.ToString();
        }

        void UpdateQuestMarker(ItemState item)
        {
            bool show = item?.Definition?.Category == ItemCategory.Quest;
            _quest.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
        }

        // Dual-rarity corner brackets — Payload (top-left) + Delivery (bottom-right),
        // each tinted by its core's rarity. Shown only for built weapons. Rarity lives
        // on the WeaponConfiguration instance, so no registry lookup is needed.
        void UpdateRarityFrame(ItemState item)
        {
            bool show = item != null && item.HasWeaponConfiguration;
            _rarityTl.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
            _rarityBr.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
            if (!show) return;

            var cfg = item.WeaponConfiguration;
            var payloadColor  = RarityVisuals.Color(cfg.Payload.Rarity);
            var deliveryColor = RarityVisuals.Color(cfg.Delivery.Rarity);

            _rarityTl.style.borderLeftColor = payloadColor;
            _rarityTl.style.borderTopColor  = payloadColor;
            _rarityBr.style.borderRightColor  = deliveryColor;
            _rarityBr.style.borderBottomColor = deliveryColor;
        }

        // Total fixed attachment slots a built weapon exposes (MVP: not rarity-scaled).
        // Single-sourced from the editor presenter so the inventory hint and the editor agree.
        static readonly int TotalAttachmentSlots =
            AttachmentEditorPresenter.PayloadSlots.Length + AttachmentEditorPresenter.DeliverySlots.Length;

        // Mod indicators (top-right): one solid dot per installed attachment, then one
        // yellow-orange "!" per still-free slot — an at-a-glance "kitted out" readout that
        // doubles as a "you can still upgrade this" call-to-action. Hidden for non-weapons.
        void UpdateModPips(ItemState item)
        {
            _modPips.Clear();

            if (item == null || !item.HasWeaponConfiguration)
            {
                _modPips.style.display = DisplayStyle.None;
                return;
            }

            int installed = 0;
            var atts = item.WeaponConfiguration.Attachments;
            if (atts != null)
                for (int i = 0; i < atts.Length; i++)
                    if (!string.IsNullOrEmpty(atts[i].DefinitionId)) installed++;

            int free = TotalAttachmentSlots - installed;
            if (free < 0) free = 0;
            if (installed > TotalAttachmentSlots) installed = TotalAttachmentSlots;

            if (installed <= 0 && free <= 0)
            {
                _modPips.style.display = DisplayStyle.None;
                return;
            }

            // Installed slots → solid dots.
            for (int i = 0; i < installed; i++)
            {
                var pip = new VisualElement();
                pip.AddToClassList("inv-slot__mod-pip");
                pip.pickingMode = PickingMode.Ignore;
                _modPips.Add(pip);
            }

            // Free slots → yellow-orange exclamation marks (install-available hint).
            for (int i = 0; i < free; i++)
            {
                var bang = new Label("!");
                bang.AddToClassList("inv-slot__mod-free");
                bang.pickingMode = PickingMode.Ignore;
                _modPips.Add(bang);
            }

            _modPips.style.display = DisplayStyle.Flex;
        }

        void UpdateIcon(ItemState item)
        {
            if (_iconRegistry == null || item == null)
            {
                _icon.style.display = DisplayStyle.None;
                return;
            }
            var sprite = _iconRegistry.GetIcon(item.DefinitionId);
            if (sprite == null)
            {
                _icon.style.display = DisplayStyle.None;
                return;
            }
            _icon.style.backgroundImage = new StyleBackground(sprite);
            _icon.style.display = DisplayStyle.Flex;
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
