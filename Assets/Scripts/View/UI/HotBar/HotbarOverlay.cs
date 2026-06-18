using ApplicationCore;
using Constants;
using Dev;
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
        VisualElement _weaponStrip;
        VisualElement _separator;
        // Slot cells are full InventorySlotElement instances — same custom UTK
        // class the inventory grid uses. Reuse gives free pixel parity (name,
        // durability bar, stack count, quest dot, drag-highlight) + a single
        // place to evolve slot visuals.
        InventorySlotElement[] _slots;
        // Weapon hotbar slots (1-2) — display equipped weapons (PlayerEntityState.Hotbar /
        // InventoryState.WeaponSlots). Click = equip/holster (writes PendingHotbarSlot);
        // drag weapon→weapon = swap via HotbarWeaponSystem. Separate from quick `_slots`.
        InventorySlotElement[] _weaponSlots;
        ItemIconRegistryAsset _iconRegistry;

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

        // ── Drag-and-drop within weapon strip (slot 1 ↔ 2) ──
        int _draggedWi             = -1;
        int _weaponDragPointerId   = -1;
        Vector2 _weaponDragStartPanelPos;
        bool _isWeaponDragging;

        /// <summary>
        /// True while a hotbar slot is actively being dragged (ghost up, past
        /// threshold). Mirrors <see cref="View.UI.Inventory.InventoryWindow.IsDragging"/>;
        /// read by <see cref="View.PointerOverUiTracker"/> to keep IsPointerOverUi
        /// sticky so the OS cursor stays + crosshair stays hidden + attack stays
        /// gated even коли ghost виходить за межі hotbar strip.
        /// </summary>
        public bool IsDragging => _isDragging || _isWeaponDragging;

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
            BuildWeaponSlots();
            BuildSlots();
            BuildPicker();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void SetIconRegistry(ItemIconRegistryAsset registry)
        {
            _iconRegistry = registry;

            if (_weaponSlots != null)
                foreach (var slot in _weaponSlots)
                    slot?.SetIconRegistry(registry);

            if (_slots != null)
                foreach (var slot in _slots)
                    slot?.SetIconRegistry(registry);
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
            _weaponStrip = _root?.Q<VisualElement>("weapon-strip");
            _separator = _root?.Q<VisualElement>("separator");
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
                slot.SetIconRegistry(_iconRegistry);
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

        void BuildWeaponSlots()
        {
            if (_weaponStrip == null) return;

            _weaponSlots = new InventorySlotElement[InventoryState.WeaponSlotCount];
            for (int i = 0; i < InventoryState.WeaponSlotCount; i++)
            {
                int wi = i;
                int keyNum = wi + 1; // weapon slots use keys 1-2

                // Same 102×102 Backpack chrome as quick slots for row parity; the warm
                // .hb-slot--weapon tint + "1/2" key + separator gap differentiate them.
                var slot = new InventorySlotElement(
                    InventorySlotElement.SlotKind.Backpack,
                    emptyPlaceholder: string.Empty);
                slot.name = $"weapon-slot-{wi}";
                slot.AddToClassList("hb-slot--weapon");

                var keyLbl = new Label(keyNum.ToString());
                keyLbl.AddToClassList("hb-slot__key");
                keyLbl.pickingMode = PickingMode.Ignore;
                slot.Add(keyLbl);

                // Click = equip/holster (no picker — weapons aren't "bound"); drag = swap.
                slot.RegisterCallback<PointerDownEvent>(evt => OnWeaponPointerDown(wi, slot, evt));
                slot.RegisterCallback<PointerMoveEvent>(evt => OnWeaponPointerMove(wi, slot, evt));
                slot.RegisterCallback<PointerUpEvent>(evt => OnWeaponPointerUp(wi, slot, evt));
                slot.RegisterCallback<PointerCaptureOutEvent>(_ => OnWeaponPointerCaptureOut(wi));

                _weaponStrip.Add(slot);
                _weaponSlots[wi] = slot;
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
            var view      = ViewCheats.Config?.BattleHud;

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

                // Resting consumable tint — only when occupied AND not active, so the
                // empty (dim) + active (selected) USS treatments stay intact. Null reverts
                // the inline value so the USS cascade applies in those states.
                bool restingNormal = item != null && !(isHeld || isFlash);
                slot.style.backgroundColor = (view != null && restingNormal)
                    ? new StyleColor(view.ConsumableSlotBgTint)
                    : new StyleColor(StyleKeyword.Null);
            }

            RefreshWeaponSlots(inventory, player, registry, view);

            // Picker auto-close: if the slot it was opened for got bound from
            // elsewhere (hover+key, context menu, etc), the picker becomes stale.
            if (_pickerSlotIndex >= 0 && !IsSlotEmpty(_pickerSlotIndex))
                HidePicker();
        }

        // ── Weapon slots (1-2) ───────────────────────────────

        void RefreshWeaponSlots(InventoryState inventory, PlayerEntityState player,
            Adapters.ICoreDefinitionRegistry registry, ViewCheatsBattleHudSection view)
        {
            if (_weaponSlots == null) return;

            // Separator gap (live-tunable).
            if (_separator != null && view != null)
                _separator.style.width = view.HotbarWeaponSeparatorPx;

            int selected = player?.SelectedHotbarSlot ?? -1;

            for (int i = 0; i < _weaponSlots.Length; i++)
            {
                var slot = _weaponSlots[i];
                if (slot == null) continue;

                ItemState item = inventory != null && i < inventory.WeaponSlots.Length
                    ? inventory.WeaponSlots[i]
                    : null;

                slot.Bind(InventorySlotRef.Weapon(i), item, quickSlotKey: -1, registry);

                bool isEquipped = selected == i && item != null;
                slot.EnableInClassList("is-empty", item == null);
                slot.EnableInClassList("is-active", isEquipped); // accent border via USS

                // Fill tint pushed inline (border stays USS): active vs resting vs empty.
                StyleColor bg;
                if (view == null) bg = new StyleColor(StyleKeyword.Null);
                else if (item == null) bg = new StyleColor(StyleKeyword.Null); // empty → USS dim
                else bg = new StyleColor(isEquipped ? view.WeaponSlotActiveTint : view.WeaponSlotBgTint);
                slot.style.backgroundColor = bg;
            }
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

                if (item.IsResourceItem)
                {
                    var count = new Label($"{item.CurrentResource}/{item.MaxResource}");
                    count.AddToClassList("hb-picker__row-count");
                    count.pickingMode = PickingMode.Ignore;
                    row.Add(count);
                }
                else if (item.StackCount > 1)
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

        // ── Weapon-slot input (click = equip/holster, drag = swap) ───────────

        void OnWeaponPointerDown(int wi, InventorySlotElement slot, PointerDownEvent evt)
        {
            if (evt.button != 0) return;
            // Capture now; PointerMove decides drag-vs-click via threshold, PointerUp routes.
            slot.CapturePointer(evt.pointerId);
            _draggedWi               = wi;
            _weaponDragPointerId     = evt.pointerId;
            _weaponDragStartPanelPos = evt.position;
            _isWeaponDragging        = false;
            evt.StopPropagation();
        }

        void OnWeaponPointerMove(int wi, InventorySlotElement slot, PointerMoveEvent evt)
        {
            if (_draggedWi != wi) return;
            if (!slot.HasPointerCapture(evt.pointerId)) return;

            if (!_isWeaponDragging)
            {
                Vector2 delta = (Vector2)evt.position - _weaponDragStartPanelPos;
                if (delta.sqrMagnitude < DragThreshold * DragThreshold) return;
                // Nothing to drag from an empty slot — let it fall through to a click no-op.
                if (slot.CurrentItem == null) return;
                _isWeaponDragging = true;
                CreateWeaponGhost(wi);
            }

            UpdateGhostPosition(evt.position);
            UpdateWeaponSlotHover(evt.position);
        }

        void OnWeaponPointerUp(int wi, InventorySlotElement slot, PointerUpEvent evt)
        {
            if (_draggedWi != wi) return;

            if (slot.HasPointerCapture(evt.pointerId))
                slot.ReleasePointer(evt.pointerId);

            if (_isWeaponDragging)
            {
                Vector2 mouseScreen = Mouse.current?.position.ReadValue() ?? Vector2.zero;
                TryDropWeapon(wi, mouseScreen);
                DestroyGhost();
                ClearAllWeaponSlotHover();
            }
            else
            {
                // Plain click → equip/holster (intent only; WeaponStateMachineSystem handshakes).
                EquipWeaponSlot(wi);
            }

            _draggedWi           = -1;
            _weaponDragPointerId = -1;
            _isWeaponDragging    = false;
        }

        void OnWeaponPointerCaptureOut(int wi)
        {
            if (_draggedWi != wi) return;
            DestroyGhost();
            ClearAllWeaponSlotHover();
            _draggedWi           = -1;
            _weaponDragPointerId = -1;
            _isWeaponDragging    = false;
        }

        void EquipWeaponSlot(int wi)
        {
            var inv    = App.Instance?.Player?.Inventory;
            var player = App.Instance?.RaidSession?.RaidState?.PlayerEntity;
            if (inv == null || player == null) return;
            if (wi < 0 || wi >= InventoryState.WeaponSlotCount) return;
            if (inv.WeaponSlots[wi] == null) return; // empty → nothing to equip

            // Mirror key 1/2: set the switch intent. WeaponStateMachineSystem equips, or
            // holsters when wi == SelectedHotbarSlot.
            player.PendingHotbarSlot = wi;
        }

        void TryDropWeapon(int srcWi, Vector2 mouseScreen)
        {
            var state = App.Instance?.RaidSession?.RaidState;
            var inv   = App.Instance?.Player?.Inventory;
            if (state == null || inv == null) return;

            int targetWi = FindWeaponSlotUnder(mouseScreen);
            // Off-strip or self-drop → cancel. Weapons don't unbind (unlike quick slots).
            if (targetWi < 0 || targetWi == srcWi) return;

            HotbarWeaponSystem.SwapWeaponSlots(state, inv, srcWi, targetWi);
        }

        void CreateWeaponGhost(int srcWi)
        {
            if (_root == null) return;
            var srcSlot = (srcWi >= 0 && srcWi < _weaponSlots.Length) ? _weaponSlots[srcWi] : null;
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

            _root.Add(_dragGhost);
        }

        int FindWeaponSlotUnder(Vector2 screenPos)
        {
            if (_weaponSlots == null || _root == null) return -1;
            var panel = _root.panel;
            if (panel == null) return -1;

            Vector2 panelPos = RuntimePanelUtils.ScreenToPanel(panel,
                new Vector2(screenPos.x, Screen.height - screenPos.y));

            for (int wi = 0; wi < _weaponSlots.Length; wi++)
            {
                var s = _weaponSlots[wi];
                if (s != null && s.worldBound.Contains(panelPos)) return wi;
            }
            return -1;
        }

        void UpdateWeaponSlotHover(Vector2 panelPos)
        {
            if (_weaponSlots == null) return;
            for (int wi = 0; wi < _weaponSlots.Length; wi++)
            {
                var s = _weaponSlots[wi];
                if (s == null) continue;
                bool over = s.worldBound.Contains(panelPos);
                if (!over || wi == _draggedWi) { s.SetDragOver(false, false); continue; }
                s.SetDragOver(valid: true, hovering: true);
            }
        }

        void ClearAllWeaponSlotHover()
        {
            if (_weaponSlots == null) return;
            foreach (var s in _weaponSlots) s?.SetDragOver(false, false);
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
