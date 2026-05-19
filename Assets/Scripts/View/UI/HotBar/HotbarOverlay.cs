using ApplicationCore;
using State;
using Systems;
using UnityEngine;
using UnityEngine.UIElements;

namespace View.UI.Hotbar
{
    /// <summary>
    /// UI Toolkit hotbar HUD — bottom-center strip showing
    /// <see cref="InventoryState.QuickSlotBindings"/> (medkit / bandage /
    /// grenade shortcuts), keys 3-9.
    ///
    /// Activation continues to flow through keyboard (<c>QuickSlotSystem</c>
    /// reads <c>IInputAdapter.QuickSlotPressed</c>). The overlay handles
    /// display + secondary input:
    /// <list type="bullet">
    ///   <item>Display: bound item name + stack count, dim "is-empty" state,
    ///     gold "is-active" highlight while the player holds the bound key.</item>
    ///   <item>Click on empty slot — opens a picker popup listing bindable
    ///     consumables from the backpack. Choose one → bound.</item>
    ///   <item>Shift+left-click — unbind.</item>
    /// </list>
    ///
    /// Two extra bind paths live in <see cref="View.UI.Inventory.InventoryWindow"/>:
    /// hover backpack item + key 3-9, and right-click → "Bind to N" context menu.
    /// </summary>
    [DefaultExecutionOrder(-90)]
    public class HotbarOverlay : MonoBehaviour
    {
        public static HotbarOverlay Instance { get; private set; }

        UIDocument _doc;
        VisualElement _root;
        VisualElement _strip;
        SlotElement[] _slots;

        VisualElement _pickerBackdrop;
        VisualElement _picker;
        Label _pickerTitle;
        VisualElement _pickerRows;
        int _pickerSlotIndex = -1;

        struct SlotElement
        {
            public VisualElement Root;
            public Label Name;
            public Label Count;
        }

        void Awake()
        {
            Instance = this;
            BuildDocument();
            BuildSlots();
            BuildPicker();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void BuildDocument()
        {
            _doc = GetComponent<UIDocument>();
            if (_doc == null) _doc = gameObject.AddComponent<UIDocument>();

            var panel = Resources.Load<PanelSettings>("UI/Hotbar/HotbarPanelSettings");
            if (panel != null)
            {
                // Re-apply scale config — see docs/ai/ui-styling.md "Override
                // PanelSettings scale fields in code". Unity caches asset edits
                // unreliably, so do it at runtime to guarantee consistency.
                panel.scaleMode = PanelScaleMode.ScaleWithScreenSize;
                panel.referenceResolution = new Vector2Int(1920, 1080);
                panel.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
                panel.match = 0.5f;
                _doc.panelSettings = panel;
            }
            else
            {
                Debug.LogWarning("[HotbarOverlay] HotbarPanelSettings missing at Resources/UI/Hotbar/.");
            }

            var visualTree = Resources.Load<VisualTreeAsset>("UI/Hotbar/HotbarOverlay");
            if (visualTree != null)
                _doc.visualTreeAsset = visualTree;
            else
                Debug.LogWarning("[HotbarOverlay] HotbarOverlay.uxml missing.");

            _root = _doc.rootVisualElement;
            _strip = _root?.Q<VisualElement>("strip");
        }

        void BuildSlots()
        {
            if (_strip == null) return;

            _slots = new SlotElement[InventoryState.QuickSlotCount];
            for (int i = 0; i < InventoryState.QuickSlotCount; i++)
            {
                int qi = i;
                int keyNum = qi + InventoryState.QuickSlotKeyOffset;

                var slot = new VisualElement { name = $"slot-{qi}" };
                slot.AddToClassList("hb-slot");
                slot.AddToClassList("is-empty");

                var keyLbl = new Label(keyNum.ToString());
                keyLbl.AddToClassList("hb-slot__key");
                keyLbl.pickingMode = PickingMode.Ignore;
                slot.Add(keyLbl);

                var countLbl = new Label();
                countLbl.AddToClassList("hb-slot__count");
                countLbl.pickingMode = PickingMode.Ignore;
                slot.Add(countLbl);

                var nameLbl = new Label("Empty");
                nameLbl.AddToClassList("hb-slot__name");
                nameLbl.pickingMode = PickingMode.Ignore;
                slot.Add(nameLbl);

                slot.RegisterCallback<MouseDownEvent>(evt =>
                {
                    if (evt.button != 0) return;

                    if (evt.shiftKey)
                    {
                        Unbind(qi);
                        evt.StopPropagation();
                        return;
                    }

                    // Plain click on an empty slot opens the picker. Click on
                    // a busy slot is a no-op for now (use Shift+click to free
                    // the slot first, or right-click in inventory to rebind).
                    if (IsSlotEmpty(qi))
                    {
                        OpenPicker(qi, slot);
                        evt.StopPropagation();
                    }
                });

                _strip.Add(slot);
                _slots[qi] = new SlotElement
                {
                    Root = slot,
                    Name = nameLbl,
                    Count = countLbl,
                };
            }
        }

        void BuildPicker()
        {
            if (_root == null) return;

            // Fullscreen backdrop catches "click outside" → closes the picker.
            // Sits between strip and picker in the visual stack: picker draws
            // over it, slots stay clickable underneath because they live in
            // _strip which is added before the backdrop is shown.
            _pickerBackdrop = new VisualElement { name = "picker-backdrop" };
            _pickerBackdrop.AddToClassList("hb-picker-backdrop");
            _pickerBackdrop.style.display = DisplayStyle.None;
            _pickerBackdrop.RegisterCallback<MouseDownEvent>(evt =>
            {
                HidePicker();
                evt.StopPropagation();
            });
            _root.Add(_pickerBackdrop);

            _picker = new VisualElement { name = "picker" };
            _picker.AddToClassList("hb-picker");
            _picker.style.position = Position.Absolute;
            _picker.style.display = DisplayStyle.None;
            // Swallow clicks on the picker chrome (title, padding) so they
            // don't bubble through to the backdrop and close it. Row clicks
            // already StopPropagation; this covers the gaps between rows.
            _picker.RegisterCallback<MouseDownEvent>(evt => evt.StopPropagation());

            _pickerTitle = new Label("Bind to slot");
            _pickerTitle.AddToClassList("hb-picker__title");
            _pickerTitle.pickingMode = PickingMode.Ignore;
            _picker.Add(_pickerTitle);

            _pickerRows = new VisualElement { name = "picker-rows" };
            _picker.Add(_pickerRows);

            _root.Add(_picker);
        }

        void LateUpdate()
        {
            if (_slots == null) return;

            var inventory = App.Instance?.Player?.Inventory;
            var player = App.Instance?.RaidSession?.RaidState?.PlayerEntity;

            for (int i = 0; i < _slots.Length; i++)
            {
                var s = _slots[i];
                if (s.Root == null) continue;

                int boundSlot = inventory != null && i < inventory.QuickSlotBindings.Length
                    ? inventory.QuickSlotBindings[i]
                    : -1;
                ItemState item = boundSlot >= 0 && boundSlot < InventoryState.BackpackSize
                    ? inventory.Backpack[boundSlot]
                    : null;

                bool hasItem = item != null;
                bool isActive = player != null && player.ActiveQuickSlot == i;

                s.Root.EnableInClassList("is-empty", !hasItem);
                s.Root.EnableInClassList("is-active", isActive);

                s.Name.text = hasItem ? item.DisplayName : "Empty";
                s.Count.text = hasItem && item.StackCount > 1 ? $"x{item.StackCount}" : string.Empty;
            }

            // Picker auto-close: if the slot it was opened for got bound from
            // elsewhere (hover+key, context menu, etc), the picker becomes stale.
            if (_pickerSlotIndex >= 0 && !IsSlotEmpty(_pickerSlotIndex))
                HidePicker();
        }

        // ── Picker ─────────────────────────────────────────

        void OpenPicker(int qi, VisualElement anchor)
        {
            var inv = App.Instance?.Player?.Inventory;
            if (inv == null || _picker == null) return;

            _pickerSlotIndex = qi;
            _pickerRows.Clear();
            _pickerTitle.text = $"Bind to slot {qi + InventoryState.QuickSlotKeyOffset}";

            bool any = false;
            for (int i = 0; i < inv.Backpack.Length; i++)
            {
                var item = inv.Backpack[i];
                if (item == null) continue;
                if (!QuickSlotRules.IsAssignable(item.DefinitionId)) continue;

                bool boundElsewhere = false;
                for (int j = 0; j < inv.QuickSlotBindings.Length; j++)
                    if (inv.QuickSlotBindings[j] == i) { boundElsewhere = true; break; }
                if (boundElsewhere) continue;

                any = true;
                int backpackIndex = i;
                var row = new VisualElement();
                row.AddToClassList("hb-picker__row");

                var label = new Label(item.DisplayName);
                label.AddToClassList("hb-picker__row-label");
                label.pickingMode = PickingMode.Ignore;
                row.Add(label);

                if (item.StackCount > 1)
                {
                    var count = new Label($"x{item.StackCount}");
                    count.AddToClassList("hb-picker__row-count");
                    count.pickingMode = PickingMode.Ignore;
                    row.Add(count);
                }

                row.RegisterCallback<MouseDownEvent>(evt =>
                {
                    if (evt.button != 0) return;
                    BindAndHide(qi, backpackIndex);
                    evt.StopPropagation();
                });

                _pickerRows.Add(row);
            }

            if (!any)
            {
                var emptyLbl = new Label("No bindable items in backpack");
                emptyLbl.AddToClassList("hb-picker__empty");
                emptyLbl.pickingMode = PickingMode.Ignore;
                _pickerRows.Add(emptyLbl);
            }

            if (_pickerBackdrop != null)
                _pickerBackdrop.style.display = DisplayStyle.Flex;
            _picker.style.display = DisplayStyle.Flex;
            // Defer positioning to next frame — picker height is 0 until layout
            // pass runs after the display flip.
            _picker.schedule.Execute(() => PositionPicker(anchor)).StartingIn(0);
        }

        void PositionPicker(VisualElement anchor)
        {
            if (_picker == null || anchor == null || _root == null) return;

            float pickerW = _picker.resolvedStyle.width;
            float pickerH = _picker.resolvedStyle.height;
            var anchorRect = anchor.worldBound;
            var rootRect = _root.worldBound;

            float x = anchorRect.center.x - pickerW * 0.5f;
            float y = anchorRect.y - pickerH - 8f;

            float padding = 8f;
            x = Mathf.Clamp(x, rootRect.x + padding, rootRect.xMax - pickerW - padding);
            y = Mathf.Max(y, rootRect.y + padding);

            _picker.style.left = x;
            _picker.style.top = y;
        }

        void BindAndHide(int qi, int backpackIndex)
        {
            var inv = App.Instance?.Player?.Inventory;
            if (inv == null) return;
            if (qi < 0 || qi >= inv.QuickSlotBindings.Length) return;

            // Defensive dup-clear — OpenPicker already filters bound-elsewhere
            // items out of the row list, але якщо логіка пікера зміниться
            // bind path має дати таку саму гарантію "один айтем = один слот"
            // як digit-bind / ctx-menu / drag-bind.
            for (int i = 0; i < inv.QuickSlotBindings.Length; i++)
                if (inv.QuickSlotBindings[i] == backpackIndex)
                    inv.QuickSlotBindings[i] = -1;

            inv.QuickSlotBindings[qi] = backpackIndex;
            // Bump version так само як інші bind paths — інакше InventoryWindow
            // не оновить "3..9" badge на bound backpack-слоті після пікер-біндингу.
            inv.Version++;
            HidePicker();
        }

        void HidePicker()
        {
            if (_picker == null) return;
            _picker.style.display = DisplayStyle.None;
            if (_pickerBackdrop != null)
                _pickerBackdrop.style.display = DisplayStyle.None;
            _pickerSlotIndex = -1;
        }

        // ── Helpers ────────────────────────────────────────

        bool IsSlotEmpty(int qi)
        {
            var inv = App.Instance?.Player?.Inventory;
            if (inv == null || qi < 0 || qi >= inv.QuickSlotBindings.Length) return true;
            int boundSlot = inv.QuickSlotBindings[qi];
            return boundSlot < 0 || boundSlot >= InventoryState.BackpackSize
                   || inv.Backpack[boundSlot] == null;
        }

        void Unbind(int qi)
        {
            var inv = App.Instance?.Player?.Inventory;
            if (inv == null) return;
            if (qi < 0 || qi >= inv.QuickSlotBindings.Length) return;
            inv.QuickSlotBindings[qi] = -1;
            // Mirror CtxUnbindQuickSlot: bump version so InventoryWindow re-binds
            // the backpack pane and clears the "3..9" key badge.
            inv.Version++;
        }

        /// <summary>
        /// Drag-drop entry point — called by InventoryWindow.TryDropOnSlot when a
        /// drop lands outside any inventory slot. Locates the hotbar slot under
        /// <paramref name="screenPos"/> (bottom-left origin, raw Input System
        /// coords), validates that the backpack item is assignable via
        /// <see cref="QuickSlotRules"/>, and binds (replacing any existing
        /// binding). Returns false for "not over any hotbar slot" OR "item not
        /// assignable" — caller treats both as silent cancel.
        /// </summary>
        public bool TryBindFromBackpack(Vector2 screenPos, int backpackIndex)
        {
            if (_slots == null || _root == null) return false;
            var panel = _root.panel;
            if (panel == null) return false;

            var inv = App.Instance?.Player?.Inventory;
            if (inv == null) return false;
            if (backpackIndex < 0 || backpackIndex >= InventoryState.BackpackSize) return false;
            var item = inv.Backpack[backpackIndex];
            if (item == null) return false;
            if (!QuickSlotRules.IsAssignable(item.DefinitionId)) return false;

            Vector2 panelPos = RuntimePanelUtils.ScreenToPanel(panel,
                new Vector2(screenPos.x, Screen.height - screenPos.y));

            for (int qi = 0; qi < _slots.Length; qi++)
            {
                var root = _slots[qi].Root;
                if (root == null) continue;
                if (!root.worldBound.Contains(panelPos)) continue;

                if (qi >= inv.QuickSlotBindings.Length) return false;

                // Clear any prior binding pointing at this same backpack slot — mirrors
                // digit-bind (HandleQuickSlotKeys) + ctx-menu bind (CtxBindToQuickSlot):
                // a single item can occupy at most one quick slot, otherwise dragging
                // through several slots duplicates it.
                for (int i = 0; i < inv.QuickSlotBindings.Length; i++)
                    if (inv.QuickSlotBindings[i] == backpackIndex)
                        inv.QuickSlotBindings[i] = -1;

                inv.QuickSlotBindings[qi] = backpackIndex;
                // Bump InventoryState.Version so InventoryWindow.RefreshAll re-binds the
                // backpack pane and the new "3..9" key badge appears on the source slot
                // (RefreshAll early-outs when Version is unchanged).
                inv.Version++;
                HidePicker();
                return true;
            }
            return false;
        }
    }
}
