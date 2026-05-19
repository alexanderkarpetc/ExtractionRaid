using ApplicationCore;
using State;
using Systems;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using View.UI;
using View.UI.Inventory;

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
        // Slot cells are full InventorySlotElement instances — same custom UTK
        // class the inventory grid uses. Reuse gives free pixel parity (name,
        // durability bar, stack count, quest dot, drag-highlight) + a single
        // place to evolve slot visuals.
        InventorySlotElement[] _slots;

        VisualElement _pickerBackdrop;
        VisualElement _picker;
        Label _pickerTitle;
        VisualElement _pickerRows;
        int _pickerSlotIndex = -1;

        // ── Drag-and-drop within hotbar ──────────────────────
        // Slot → empty hotbar slot   = move binding
        // Slot → occupied hotbar slot = swap bindings
        // Slot → empty world (no UI)  = unbind
        // Slot → any other UI         = silent cancel
        const float DragThreshold = 4f;
        int _draggedQi     = -1;
        int _dragPointerId = -1;
        Vector2 _dragStartPanelPos;
        bool _isDragging;
        VisualElement _dragGhost;

        /// <summary>
        /// True while a hotbar slot is actively being dragged (ghost up, past
        /// threshold). Mirrors <see cref="View.UI.Inventory.InventoryWindow.IsDragging"/>;
        /// read by <see cref="View.PointerOverUiTracker"/> to keep IsPointerOverUi
        /// sticky so the OS cursor stays + crosshair stays hidden + attack stays
        /// gated even коли ghost виходить за межі hotbar strip.
        /// </summary>
        public bool IsDragging => _isDragging;

        // ── Active-slot press flash ──────────────────────────
        // QuickSlotSystem sets PlayerEntityState.ActiveQuickSlot only while the
        // key is held. For a quick tap that's a single frame — visually invisible
        // (`is-active` toggles on/off too fast to register). To give clear
        // feedback on every press, we sample edges (-1 → qi) and hold the
        // `is-active` class for at least ActiveFlashDuration regardless of how
        // briefly the key was held. Pressing a DIFFERENT bound slot restarts.
        const float ActiveFlashDuration = 0.18f;
        int   _lastActiveQuickSlot = -1;
        int   _activeFlashSlot     = -1;
        float _activeFlashStart    = -1f;

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

            _slots = new InventorySlotElement[InventoryState.QuickSlotCount];
            for (int i = 0; i < InventoryState.QuickSlotCount; i++)
            {
                int qi = i;
                int keyNum = qi + InventoryState.QuickSlotKeyOffset;

                // Backpack-variant InventorySlotElement — 102×102, inv-slot
                // chrome, all the same visual children (name, resource, durability,
                // quest, _quickSlotKey). emptyPlaceholder left blank so a pust slot
                // shows ТІЛЬКИ the always-on "3..9" hotkey hint, без redundant "Empty"
                // word. is-empty USS class buys us the dim surface treatment.
                var slot = new InventorySlotElement(
                    InventorySlotElement.SlotKind.Backpack,
                    emptyPlaceholder: string.Empty);
                slot.name = $"slot-{qi}";

                // Always-visible hotbar key hint. Distinct semantic from
                // InventorySlotElement._quickSlotKey (which marks "this backpack
                // item is bound to slot N" inside the inventory grid). Stays
                // visible even when the hotbar slot is unbound — гравцеві потрібно
                // бачити яку клавішу тиснути. Last child = top of z-stack.
                var keyLbl = new Label(keyNum.ToString());
                keyLbl.AddToClassList("hb-slot__key");
                keyLbl.pickingMode = PickingMode.Ignore;
                slot.Add(keyLbl);

                // Slot input is pointer-based (not MouseDown) so we can branch:
                //   shift+click → unbind, click-empty → picker, click-occupied → drag start.
                // PointerDown captures the pointer; PointerMove flips _isDragging
                // after threshold and spawns the ghost; PointerUp routes to
                // TryDropFromHotbar (move/swap/unbind/cancel per drop target).
                slot.RegisterCallback<PointerDownEvent>(evt => OnSlotPointerDown(qi, slot, evt));
                slot.RegisterCallback<PointerMoveEvent>(evt => OnSlotPointerMove(qi, slot, evt));
                slot.RegisterCallback<PointerUpEvent>(evt => OnSlotPointerUp(qi, slot, evt));
                slot.RegisterCallback<PointerCaptureOutEvent>(_ => OnSlotPointerCaptureOut(qi));

                _strip.Add(slot);
                _slots[qi] = slot;
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
            var player    = App.Instance?.RaidSession?.RaidState?.PlayerEntity;
            var registry  = App.Instance?.CoreDefinitions;

            // Edge-detect a new activation (only fires коли binding exists +
            // player can actually use it — QuickSlotSystem gates на rolling /
            // hands busy, тому unbound presses не дають фалшивого flash-у).
            int active = player?.ActiveQuickSlot ?? -1;
            if (active >= 0 && active != _lastActiveQuickSlot)
            {
                _activeFlashSlot  = active;
                _activeFlashStart = Time.time;
            }
            _lastActiveQuickSlot = active;
            bool flashAlive = _activeFlashSlot >= 0
                              && (Time.time - _activeFlashStart) < ActiveFlashDuration;

            for (int i = 0; i < _slots.Length; i++)
            {
                var slot = _slots[i];
                if (slot == null) continue;

                int boundSlot = inventory != null && i < inventory.QuickSlotBindings.Length
                    ? inventory.QuickSlotBindings[i]
                    : -1;
                ItemState item = boundSlot >= 0 && boundSlot < InventoryState.BackpackSize
                    ? inventory.Backpack[boundSlot]
                    : null;

                // Bind() handles name / stack count "xN" via _resource / durability /
                // quest dot / empty placeholder visibility — same rendering as the
                // inventory grid. quickSlotKey: -1 → InventorySlotElement's own
                // _quickSlotKey badge stays hidden (ми малюємо власний `hb-slot__key`).
                var slotRef = boundSlot >= 0
                    ? InventorySlotRef.BackpackSlot(boundSlot)
                    : default;
                slot.Bind(slotRef, item, quickSlotKey: -1, registry);

                // is-active = held right now OR within the press-flash window.
                // Tap (release < 0.18s) keeps highlight visible; held key extends
                // it naturally through the `isHeld` branch.
                bool isHeld  = active == i;
                bool isFlash = flashAlive && _activeFlashSlot == i;
                slot.EnableInClassList("is-empty", item == null);
                slot.EnableInClassList("is-active", isHeld || isFlash);
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

        // ── Drag handlers ────────────────────────────────────

        void OnSlotPointerDown(int qi, InventorySlotElement slot, PointerDownEvent evt)
        {
            if (evt.button != 0) return;

            // Shift+click → unbind. Never enters drag mode (drag-then-unbind
            // is unintuitive, and Shift+drag would surprise the user).
            if (evt.shiftKey)
            {
                Unbind(qi);
                evt.StopPropagation();
                return;
            }

            // Empty slot — open picker. Drag-from-empty has no source binding
            // so it would be a no-op anyway; picker is the only useful action.
            if (IsSlotEmpty(qi))
            {
                OpenPicker(qi, slot);
                evt.StopPropagation();
                return;
            }

            // Occupied + no shift → start drag tracking. Ghost only spawns
            // у OnSlotPointerMove after DragThreshold so a plain click on an
            // occupied slot still feels like a click (no-op currently).
            slot.CapturePointer(evt.pointerId);
            _draggedQi         = qi;
            _dragPointerId     = evt.pointerId;
            _dragStartPanelPos = evt.position;
            _isDragging        = false;
            evt.StopPropagation();
        }

        void OnSlotPointerMove(int qi, InventorySlotElement slot, PointerMoveEvent evt)
        {
            if (_draggedQi != qi) return;
            if (!slot.HasPointerCapture(evt.pointerId)) return;

            if (!_isDragging)
            {
                Vector2 delta = (Vector2)evt.position - _dragStartPanelPos;
                if (delta.sqrMagnitude < DragThreshold * DragThreshold) return;
                _isDragging = true;
                CreateGhost(qi);
            }

            UpdateGhostPosition(evt.position);
            UpdateSlotHover(evt.position);
        }

        void OnSlotPointerUp(int qi, InventorySlotElement slot, PointerUpEvent evt)
        {
            if (_draggedQi != qi) return;

            if (slot.HasPointerCapture(evt.pointerId))
                slot.ReleasePointer(evt.pointerId);

            if (_isDragging)
            {
                // Mouse.current.position is bottom-left screen coords — same convention
                // as InventoryWindow.TryDropOnSlot, consistent with UiPanelHitTest.
                Vector2 mouseScreen = Mouse.current?.position.ReadValue() ?? Vector2.zero;
                TryDropFromHotbar(qi, mouseScreen);
                DestroyGhost();
                ClearAllSlotHover();
            }

            _draggedQi     = -1;
            _dragPointerId = -1;
            _isDragging    = false;
        }

        void OnSlotPointerCaptureOut(int qi)
        {
            if (_draggedQi != qi) return;
            // Pointer capture lost mid-drag (e.g. panel rebuilt, scene change).
            // Drop the ghost and reset — never run drop logic on a lost gesture.
            DestroyGhost();
            ClearAllSlotHover();
            _draggedQi     = -1;
            _dragPointerId = -1;
            _isDragging    = false;
        }

        // ── Drop routing ─────────────────────────────────────

        void TryDropFromHotbar(int srcQi, Vector2 mouseScreen)
        {
            var inv = App.Instance?.Player?.Inventory;
            if (inv == null) return;
            if (srcQi < 0 || srcQi >= inv.QuickSlotBindings.Length) return;

            int srcIdx = inv.QuickSlotBindings[srcQi];
            if (srcIdx < 0) return; // drag started but binding vanished — bail.

            int targetQi = FindHotbarSlotUnder(mouseScreen);
            if (targetQi >= 0)
            {
                if (targetQi == srcQi) return; // self-drop = no-op
                if (targetQi >= inv.QuickSlotBindings.Length) return;

                int tgtIdx = inv.QuickSlotBindings[targetQi];
                if (tgtIdx < 0)
                {
                    // Rule 1: move to empty hotbar slot.
                    inv.QuickSlotBindings[srcQi]    = -1;
                    inv.QuickSlotBindings[targetQi] = srcIdx;
                }
                else
                {
                    // Rule 2: swap bindings.
                    inv.QuickSlotBindings[srcQi]    = tgtIdx;
                    inv.QuickSlotBindings[targetQi] = srcIdx;
                }
                inv.Version++;
                return;
            }

            // Rule 4: dropped on some OTHER UI panel (inventory, tooltip, builder...).
            // Silent cancel — hotbar drag is hotbar-internal only, никаких передач
            // в інвентар чи деінде.
            if (UiPanelHitTest.IsScreenPointOverUi(mouseScreen)) return;

            // Rule 3: dropped onto empty world space → drop-to-clear (unbind).
            inv.QuickSlotBindings[srcQi] = -1;
            inv.Version++;
        }

        // Shared by drag-internal target lookup AND public TryBindFromBackpack
        // (inventory→hotbar drop) — single source of truth for "which hotbar slot
        // is under this screen point".
        int FindHotbarSlotUnder(Vector2 screenPos)
        {
            if (_slots == null || _root == null) return -1;
            var panel = _root.panel;
            if (panel == null) return -1;

            Vector2 panelPos = RuntimePanelUtils.ScreenToPanel(panel,
                new Vector2(screenPos.x, Screen.height - screenPos.y));

            for (int qi = 0; qi < _slots.Length; qi++)
            {
                var slot = _slots[qi];
                if (slot != null && slot.worldBound.Contains(panelPos))
                    return qi;
            }
            return -1;
        }

        // ── Ghost + hover-highlight ──────────────────────────
        // Mirror InventoryWindow's drag-visual idioms 1:1 — same .inv-slot + .inv-slot--bp
        // + .inv-drag-ghost class triplet, same center-on-cursor formula, same
        // .SetDragOver(valid, hovering) protocol for drop-target highlight.

        void CreateGhost(int srcQi)
        {
            if (_root == null) return;
            var srcSlot = (srcQi >= 0 && srcQi < _slots.Length) ? _slots[srcQi] : null;
            if (srcSlot?.CurrentItem == null) return;

            DestroyGhost();
            _dragGhost = new VisualElement { name = "hb-drag-ghost" };
            _dragGhost.pickingMode = PickingMode.Ignore;
            _dragGhost.AddToClassList("inv-slot");
            _dragGhost.AddToClassList("inv-slot--bp");
            _dragGhost.AddToClassList("inv-drag-ghost");

            var registry = App.Instance?.CoreDefinitions;
            var name = new Label(WeaponDisplayName.For(srcSlot.CurrentItem, registry));
            name.AddToClassList("inv-slot__name");
            name.pickingMode = PickingMode.Ignore;
            _dragGhost.Add(name);

            // Last child of root → top of z-stack (above strip + picker).
            _root.Add(_dragGhost);
        }

        void UpdateGhostPosition(Vector2 panelPos)
        {
            if (_dragGhost == null) return;
            // Center the ghost on the cursor — matches InventoryWindow.UpdateGhostPosition.
            float w = _dragGhost.resolvedStyle.width;
            float h = _dragGhost.resolvedStyle.height;
            if (w <= 0f) w = 102f;
            if (h <= 0f) h = 102f;
            _dragGhost.style.left = panelPos.x - w * 0.5f;
            _dragGhost.style.top  = panelPos.y - h * 0.5f;
        }

        void DestroyGhost()
        {
            if (_dragGhost == null) return;
            _dragGhost.RemoveFromHierarchy();
            _dragGhost = null;
        }

        // Drop-target highlight while dragging — adds inv-slot-drag-valid (green
        // border + bg) to the slot under the cursor, clears on the rest. Source
        // slot itself never highlights. Hotbar drop is always valid on any other
        // slot (Rules 1+2 cover move/swap), тому валідність всюди true.
        void UpdateSlotHover(Vector2 panelPos)
        {
            if (_slots == null) return;
            for (int qi = 0; qi < _slots.Length; qi++)
            {
                var s = _slots[qi];
                if (s == null) continue;
                bool over = s.worldBound.Contains(panelPos);
                if (!over || qi == _draggedQi)
                {
                    s.SetDragOver(false, false);
                    continue;
                }
                s.SetDragOver(valid: true, hovering: true);
            }
        }

        void ClearAllSlotHover()
        {
            if (_slots == null) return;
            foreach (var s in _slots) s?.SetDragOver(false, false);
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
            var inv = App.Instance?.Player?.Inventory;
            if (inv == null) return false;
            if (backpackIndex < 0 || backpackIndex >= InventoryState.BackpackSize) return false;
            var item = inv.Backpack[backpackIndex];
            if (item == null) return false;
            if (!QuickSlotRules.IsAssignable(item.DefinitionId)) return false;

            int qi = FindHotbarSlotUnder(screenPos);
            if (qi < 0) return false;
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
    }
}
