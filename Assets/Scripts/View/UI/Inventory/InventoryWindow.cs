using System.Collections.Generic;
using ApplicationCore;
using Constants;
using State;
using Systems;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using View.UI.Compare;
using View.UI.Hotbar;
using View.UI.Tooltip;
using View.UI.Tooltip.Builders;

namespace View.UI.Inventory
{
    /// <summary>
    /// Runtime UI Toolkit inventory modal — canonical inventory UI.
    ///
    /// Layout:
    ///   - Main window left-anchored, holds only the player pane.
    ///   - Floating <see cref="LootSubPanelElement"/> sub-panels stack to the
    ///     right of the window — one per nearby lootable / corpse / floor /
    ///     hideout stash. Mirrors the multi-container view convention, while
    ///     keeping drag-drop centralised here.
    ///
    /// Visibility is driven by <see cref="View.InventoryUI"/> via Open / Close
    /// when <c>DevCheats.UseUiToolkitInventory</c> is on; the legacy uGUI popup
    /// remains the default path until the migration is validated end-to-end.
    ///
    /// Drag pattern mirrors WeaponBuilderWindow — pointer-capture state machine
    /// з 4-px threshold, absolute-positioned ghost, suppress-next-click flag.
    /// Sub-panels reconcile by source key; the dictionary holds long-lived
    /// elements so they don't recycle slot references mid-drag.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class InventoryWindow : MonoBehaviour
    {
        public static InventoryWindow Instance { get; private set; }

        // ── Document ──────────────────────────────────────────
        UIDocument _doc;
        VisualElement _root;
        VisualElement _backdrop;
        VisualElement _inner;
        VisualElement _window;
        Button _closeBtn;
        Label _creditsLabel;

        // ── Fade animation ────────────────────────────────────
        const int FadeDurationMs = 160;
        const string FadingClass = "inv-fading";
        int _fadeGen;

        // Player-pane rebind gate. Skips the 24-slot rebind loop (which
        // allocates strings via WeaponDisplayName.For + stack-count interp)
        // when the inventory hasn't been mutated since last bind. Sub-panel
        // refresh always runs — it depends on player position для distance
        // filtering of loot/floor. Reset to -1 on Open() to force a fresh
        // bind. ItemState mutations (stack count change via reload etc) are
        // NOT covered — those rarely happen while inv window open.
        int _lastPlayerInvVersion = -1;

        VisualElement _equipmentRow;
        VisualElement _backpackGrid;
        InventorySlotElement[] _weaponSlots;
        InventorySlotElement _helmetSlot;
        InventorySlotElement _armorSlot;
        InventorySlotElement[] _backpackSlots;

        // ── Floating sub-panels ───────────────────────────────
        VisualElement _subPanelsHost;
        readonly Dictionary<string, LootSubPanelElement> _subPanels = new();
        readonly List<GroundItemState> _floorItems = new();
        readonly List<string> _scratchRemoveKeys = new();

        // Scratch HashSet reused each RefreshSubPanels frame instead of new'd
        // per call (~250-400 B/frame saved). Cleared at the start of each pass.
        readonly HashSet<string> _scratchWanted = new();

        // ── Context menu (right-click) ────────────────────────
        ContextMenuElement _contextMenu;
        readonly List<ContextMenuElement.Option> _scratchOptions = new();

        // ── Drag state ────────────────────────────────────────
        const float DragThreshold = 4f;
        InventorySlotElement _draggedSlot;
        int _dragPointerId = -1;
        Vector2 _dragStartPanelPos;
        bool _isDragging;
        VisualElement _dragGhost;

        ItemIconRegistryAsset _iconRegistry;

        // ── Hover state (for tooltip + hover-key quick-slot bind) ─
        InventorySlotElement _hoveredSlot;

        // ── Weapon-compare state (hovering a weapon → side-by-side vs equipped baseline) ─
        ItemState _compareItem;
        Vector2 _comparePos;
        bool _compareAltHeld;
        IReadOnlyList<ItemState> _compareCandidates;
        string _compareHoveredTag;
        int _compareSelectedSlot;

        // Quick-slot keys 3..9 → bindings index 0..6.
        static readonly Key[] QuickSlotKeys =
        {
            Key.Digit3, Key.Digit4, Key.Digit5,
            Key.Digit6, Key.Digit7, Key.Digit8, Key.Digit9,
        };

        bool _isVisible;

        public bool IsOpen => _isVisible;

        // True once a drag has passed the threshold and the ghost is up (set in
        // OnSlotPointerMove). Read by PointerOverUiTracker to keep IsPointerOverUi
        // sticky during drag — without this, dragging the ghost outside the inventory
        // window flips the cursor back to crosshair + un-gates attack input,
        // and the player starts shooting mid-drag. Resets on drop / cancel.
        public bool IsDragging => _isDragging;

        void Awake()
        {
            Instance = this;
            BuildDocument();
            BuildPlayerSlots();
            HideImmediate();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void Update()
        {
            if (!_isVisible) return;
            RefreshAll();
            HandleQuickSlotKeys();
            HandleFKey();
            HandleCompareFlip();
        }

        // Holding Alt peeks the other equipped weapon as the baseline; releasing returns to the
        // active one. Re-renders only on the press/release edge while the panel is up.
        void HandleCompareFlip()
        {
            if (_compareItem == null || _compareCandidates == null || _compareCandidates.Count <= 1) return;
            var panel = WeaponComparePanel.Instance;
            if (panel == null || !panel.IsVisible) return;
            var kb = Keyboard.current;
            if (kb == null) return;

            bool held = kb[Key.LeftAlt].isPressed;
            if (held == _compareAltHeld) return;
            _compareAltHeld = held;
            var baseline = WeaponCompareTarget.Pick(_compareCandidates, held ? 1 : 0);
            panel.Show(_compareItem, _compareHoveredTag, baseline,
                       BaselineTag(baseline, App.Instance?.Player?.Inventory, _compareSelectedSlot),
                       hasMore: true, _comparePos);
        }

        void HandleFKey()
        {
            var hovered = _hoveredSlot;
            if (hovered == null || hovered.CurrentItem == null) return;
            var kb = Keyboard.current;
            if (kb == null || !kb[Key.F].wasPressedThisFrame) return;

            switch (hovered.Source)
            {
                case InventorySlotElement.SlotSource.Loot:
                    CtxPickUpFromLoot(hovered); // shop → buy, free loot → pick up
                    break;
                case InventorySlotElement.SlotSource.Floor:
                    CtxPickUpFromFloor(hovered);
                    break;
                case InventorySlotElement.SlotSource.Stash:
                    CtxTakeFromStash(hovered);
                    break;
                case InventorySlotElement.SlotSource.Player:
                    if (hovered.SlotRef.Type != SlotType.Backpack) break;
                    var shop = FindNearbyShop();
                    if (shop != null) CtxSellToShop(hovered, shop);
                    else if (App.Instance != null && App.Instance.IsInHideout)
                        CtxStashPlayer(hovered);
                    else
                        CtxDropPlayer(hovered);
                    break;
            }
        }

        // ── Public API ────────────────────────────────────────

        public void SetIconRegistry(ItemIconRegistryAsset registry)
        {
            _iconRegistry = registry;
            if (_weaponSlots != null)
                foreach (var s in _weaponSlots) s.SetIconRegistry(registry);
            _helmetSlot?.SetIconRegistry(registry);
            _armorSlot?.SetIconRegistry(registry);
            if (_backpackSlots != null)
                foreach (var s in _backpackSlots) s.SetIconRegistry(registry);
            foreach (var panel in _subPanels.Values) panel.SetIconRegistry(registry);
        }

        public void Toggle()
        {
            if (_isVisible) Close(); else Open();
        }

        public void Open()
        {
            if (_root == null) return;

            // Mount invisible, then drop the fading class next frame so the
            // USS opacity transition runs (0 → 1). Mirrors WeaponBuilderWindow.
            int gen = ++_fadeGen;
            _isVisible = true;
            _lastPlayerInvVersion = -1; // force first bind on open
            _root.style.display = DisplayStyle.Flex;
            if (_inner != null) _inner.AddToClassList(FadingClass);
            RefreshAll();

            _root.schedule.Execute(() =>
            {
                if (gen != _fadeGen) return;
                if (_inner != null) _inner.RemoveFromClassList(FadingClass);
            }).StartingIn(0);
        }

        public void Close()
        {
            if (_root == null) return;
            CancelActiveDrag();
            _contextMenu?.Hide();

            // Start fade-out, then collapse display after the transition.
            int gen = ++_fadeGen;
            _isVisible = false;
            if (_inner != null) _inner.AddToClassList(FadingClass);

            _root.schedule.Execute(() =>
            {
                if (gen != _fadeGen) return;
                _root.style.display = DisplayStyle.None;
                if (_inner != null) _inner.RemoveFromClassList(FadingClass);
            }).StartingIn(FadeDurationMs);
        }

        // ── Build ─────────────────────────────────────────────

        void BuildDocument()
        {
            _doc = GetComponent<UIDocument>();
            if (_doc == null) _doc = gameObject.AddComponent<UIDocument>();

            var panel = Resources.Load<PanelSettings>("UI/Inventory/InventoryPanelSettings");
            if (panel != null)
            {
                // Re-apply scale config in code — Unity caches PanelSettings
                // asset edits unreliably across domain reloads. See
                // docs/ai/ui-styling.md "Override PanelSettings scale fields in code".
                panel.scaleMode = PanelScaleMode.ScaleWithScreenSize;
                panel.referenceResolution = new Vector2Int(1920, 1080);
                panel.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
                panel.match = 0.5f;
                _doc.panelSettings = panel;
            }
            else
            {
                Debug.LogWarning("[InventoryWindow] InventoryPanelSettings missing at Resources/UI/Inventory/.");
            }

            var visualTree = Resources.Load<VisualTreeAsset>("UI/Inventory/InventoryWindow");
            if (visualTree != null)
                _doc.visualTreeAsset = visualTree;
            else
                Debug.LogWarning("[InventoryWindow] InventoryWindow.uxml missing at Resources/UI/Inventory/.");

            _root = _doc.rootVisualElement;
            if (_root == null) return;

            _backdrop       = _root.Q<VisualElement>("backdrop");
            _inner          = _root.Q<VisualElement>("inner");
            _window         = _root.Q<VisualElement>("window");
            _closeBtn       = _root.Q<Button>("closeBtn");
            _creditsLabel   = _root.Q<Label>("creditsLabel");
            _equipmentRow   = _root.Q<VisualElement>("equipmentRow");
            _backpackGrid   = _root.Q<VisualElement>("backpackGrid");
            _subPanelsHost  = _root.Q<VisualElement>("subPanels");

            if (_closeBtn != null)
                _closeBtn.clicked += Close;

            _contextMenu = new ContextMenuElement();
            _root.Add(_contextMenu);
            // Capture-phase pointer-down on root dismisses an open menu when
            // the click lands outside the menu's bounds.
            _root.RegisterCallback<PointerDownEvent>(OnRootPointerDown, TrickleDown.TrickleDown);
        }

        void OnRootPointerDown(PointerDownEvent evt)
        {
            if (_contextMenu == null || !_contextMenu.IsVisible) return;
            if (_contextMenu.worldBound.Contains(evt.position)) return;
            _contextMenu.Hide();
        }

        void BuildPlayerSlots()
        {
            if (_equipmentRow == null || _backpackGrid == null) return;

            _weaponSlots = new InventorySlotElement[InventoryState.WeaponSlotCount];
            for (int i = 0; i < InventoryState.WeaponSlotCount; i++)
            {
                var s = new InventorySlotElement(InventorySlotElement.SlotKind.Equipment, "(weapon)");
                _equipmentRow.Add(s);
                _weaponSlots[i] = s;
                WireSlotInteractions(s);
            }

            _helmetSlot = new InventorySlotElement(InventorySlotElement.SlotKind.Equipment, "(helmet)");
            _equipmentRow.Add(_helmetSlot);
            WireSlotInteractions(_helmetSlot);

            _armorSlot = new InventorySlotElement(InventorySlotElement.SlotKind.Equipment, "(armor)");
            _equipmentRow.Add(_armorSlot);
            WireSlotInteractions(_armorSlot);

            _backpackSlots = new InventorySlotElement[InventoryState.BackpackSize];
            for (int i = 0; i < InventoryState.BackpackSize; i++)
            {
                var s = new InventorySlotElement(InventorySlotElement.SlotKind.Backpack, "");
                _backpackGrid.Add(s);
                _backpackSlots[i] = s;
                WireSlotInteractions(s);
                // Every 5th slot ends a row — drop trailing right-margin so 5 cells
                // fit exactly у the player pane. USS :nth-child() is unsupported
                // by Unity's UI Toolkit parser; ми ставимо class + inline-style
                // (defensive — inline style has highest specificity, гарантовано
                // переб'є будь-який USS cascade quirk з shorthand `margin`).
                if ((i + 1) % 5 == 0)
                {
                    s.AddToClassList("inv-slot--row-end");
                    s.style.marginRight = 0;
                }
            }
        }

        // ── Refresh ───────────────────────────────────────────

        void RefreshAll()
        {
            if (_isDragging) return; // Stale slot refs would break if we reconcile mid-drag.

            if (_creditsLabel != null)
                _creditsLabel.text = $"{App.Instance?.Player?.Credits ?? 0}¢";

            var inventory = App.Instance?.Player?.Inventory;
            var registry  = App.Instance?.CoreDefinitions;

            if (inventory == null)
            {
                ClearPlayerSlots();
                RemoveAllSubPanels();
                _lastPlayerInvVersion = -1;
                return;
            }

            // Skip player-pane rebind when inventory hasn't been mutated since
            // last frame. Sub-panel refresh below always runs (depends on
            // player position для distance filtering of nearby lootables/floor).
            if (inventory.Version != _lastPlayerInvVersion)
            {
                BindPlayerSlots(inventory, registry);
                _lastPlayerInvVersion = inventory.Version;
            }

            RefreshSubPanels(registry);
        }

        void BindPlayerSlots(InventoryState inventory, Adapters.ICoreDefinitionRegistry registry)
        {
            for (int i = 0; i < _weaponSlots.Length; i++)
            {
                var item = i < inventory.WeaponSlots.Length ? inventory.WeaponSlots[i] : null;
                _weaponSlots[i].Bind(InventorySlotRef.Weapon(i), item, quickSlotKey: -1, registry);
            }

            _helmetSlot.Bind(InventorySlotRef.Helmet(), inventory.HelmetSlot, quickSlotKey: -1, registry);
            _armorSlot.Bind(InventorySlotRef.BodyArmor(), inventory.BodyArmorSlot, quickSlotKey: -1, registry);

            for (int i = 0; i < _backpackSlots.Length; i++)
            {
                var item = i < inventory.Backpack.Length ? inventory.Backpack[i] : null;
                _backpackSlots[i].Bind(InventorySlotRef.BackpackSlot(i),
                    item, FindQuickSlotKey(inventory, i), registry);
            }
        }

        void ClearPlayerSlots()
        {
            foreach (var s in _weaponSlots)   s.Bind(default, null, -1, null);
            _helmetSlot?.Bind(default, null, -1, null);
            _armorSlot?.Bind(default, null, -1, null);
            foreach (var s in _backpackSlots) s.Bind(default, null, -1, null);
        }

        void RefreshSubPanels(Adapters.ICoreDefinitionRegistry registry)
        {
            if (_subPanelsHost == null) return;

            // Side-by-side з Builder: ховаємо праву колонку — Builder сидить
            // y тій самій горизонтальній зоні (right-anchored 1280px), а у
            // workbench-режимі лут-джерел поряд все одно немає.
            if (IsBuilderOpen())
            {
                RemoveAllSubPanels();
                _subPanelsHost.style.display = DisplayStyle.None;
                return;
            }
            _subPanelsHost.style.display = DisplayStyle.Flex;

            _scratchWanted.Clear();

            bool inHideout = App.Instance != null && App.Instance.IsInHideout;
            var state  = App.Instance?.RaidSession?.RaidState;
            var player = state?.PlayerEntity;
            bool tradingShop = FindNearbyShop() != null;

            if (inHideout && !tradingShop)
                BindStashPanel(_scratchWanted, registry);

            if (state != null && player != null)
            {
                BindLootablePanels(state, player.Position, _scratchWanted, registry);
                if (!inHideout)
                    BindFloorPanel(state, player.Position, _scratchWanted, registry);
            }

            // Sweep out sub-panels for sources that disappeared (player walked
            // away, container got cleaned up, hideout returned to raid, etc).
            _scratchRemoveKeys.Clear();
            foreach (var key in _subPanels.Keys)
                if (!_scratchWanted.Contains(key)) _scratchRemoveKeys.Add(key);
            foreach (var key in _scratchRemoveKeys)
            {
                _subPanels[key].RemoveFromHierarchy();
                _subPanels.Remove(key);
            }
        }

        void BindStashPanel(HashSet<string> wanted, Adapters.ICoreDefinitionRegistry registry)
        {
            var stash = App.Instance.Player?.Stash;
            if (stash == null) return;

            const string key = "stash";
            wanted.Add(key);
            var panel = EnsureSubPanel(key, "STASH", InventorySlotElement.SlotSource.Stash, default);
            panel.EnsureSlotCount(stash.Count);
            for (int i = 0; i < stash.Count; i++)
            {
                var slot = panel.Slots[i];
                slot.RightIndex = i;
                slot.Bind(InventorySlotRef.BackpackSlot(i), stash[i], -1, registry);
            }
        }

        void BindLootablePanels(RaidState state, Vector3 playerPos,
            HashSet<string> wanted, Adapters.ICoreDefinitionRegistry registry)
        {
            float rangeSqr = LootSystem.LootRange * LootSystem.LootRange;
            for (int li = 0; li < state.Lootables.Count; li++)
            {
                var lootable = state.Lootables[li];
                if ((lootable.Position - playerPos).sqrMagnitude > rangeSqr) continue;
                if (lootable.Inventory == null) continue;

                var inv = lootable.Inventory;

                // Compact view (як floor/stash) — render only populated backpack
                // slots. Empty positions у corpse/container приховуємо, бо 20-cell
                // sparse grid візуально плутає. Original backpack index лишається
                // у slot.SlotRef для transfer-операцій (TryTransfer/TryMove
                // використовують SlotRef.Index, а не display position).
                int populated = 0;
                for (int i = 0; i < inv.Backpack.Length; i++)
                    if (inv.Backpack[i] != null) populated++;
                if (populated == 0) continue;

                var key = "loot:" + lootable.Id;
                wanted.Add(key);
                var panel = EnsureSubPanel(key, ResolveLootableTitle(lootable),
                    InventorySlotElement.SlotSource.Loot, lootable.Id);
                panel.EnsureSlotCount(populated);

                int displayIdx = 0;
                for (int i = 0; i < inv.Backpack.Length; i++)
                {
                    var item = inv.Backpack[i];
                    if (item == null) continue;
                    var slot = panel.Slots[displayIdx];
                    slot.RightIndex = i; // preserve original backpack index
                    slot.Bind(InventorySlotRef.BackpackSlot(i), item, -1, registry);
                    slot.SetShopPrice(lootable.IsShop ? ShopSystem.GetBuyPrice(lootable, item) : -1);
                    displayIdx++;
                }
            }
        }

        void BindFloorPanel(RaidState state, Vector3 playerPos,
            HashSet<string> wanted, Adapters.ICoreDefinitionRegistry registry)
        {
            _floorItems.Clear();
            float rangeSqr = LootSystem.LootRange * LootSystem.LootRange;
            for (int i = 0; i < state.GroundItems.Count; i++)
            {
                var gi = state.GroundItems[i];
                if ((gi.Position - playerPos).sqrMagnitude <= rangeSqr)
                    _floorItems.Add(gi);
            }
            if (_floorItems.Count == 0) return;

            const string key = "floor";
            wanted.Add(key);
            var panel = EnsureSubPanel(key, $"ON THE FLOOR ({_floorItems.Count})",
                InventorySlotElement.SlotSource.Floor, default);
            panel.EnsureSlotCount(_floorItems.Count);
            for (int i = 0; i < _floorItems.Count; i++)
            {
                var gi = _floorItems[i];
                var synth = gi.HasWeaponConfiguration
                    ? ItemState.CreateWeapon(gi.Id, gi.DefinitionId, gi.WeaponConfiguration)
                    : ItemState.Create(gi.Id, gi.DefinitionId, gi.StackCount);

                var slot = panel.Slots[i];
                slot.RightIndex = i;
                slot.Bind(InventorySlotRef.BackpackSlot(i), synth, -1, registry);
            }
        }

        LootSubPanelElement EnsureSubPanel(string key, string title,
            InventorySlotElement.SlotSource source, EId lootableId)
        {
            if (!_subPanels.TryGetValue(key, out var panel))
            {
                panel = new LootSubPanelElement(WireSlotInteractions);
                if (_iconRegistry != null) panel.SetIconRegistry(_iconRegistry);
                _subPanels[key] = panel;
                _subPanelsHost.Add(panel);
            }
            panel.SetTitle(title);
            panel.SetSourceMeta(source, lootableId);
            return panel;
        }

        void RemoveAllSubPanels()
        {
            foreach (var panel in _subPanels.Values) panel.RemoveFromHierarchy();
            _subPanels.Clear();
        }

        static bool IsBuilderOpen()
        {
            var player = App.Instance?.RaidSession?.RaidState?.PlayerEntity;
            return player != null && player.BuilderTargetId != EId.None;
        }

        static string ResolveLootableTitle(LootableContainerState lootable)
        {
            if (lootable == null) return "LOOT";
            // Shop panel surfaces the vendor name + live credit balance so players
            // see what they can afford without opening a separate UI.
            if (lootable.IsShop)
            {
                return string.IsNullOrEmpty(lootable.OwnerNpcId)
                    ? "TRADE"
                    : lootable.OwnerNpcId.ToUpperInvariant();
            }
            if (!string.IsNullOrEmpty(lootable.TypeId)
                && Constants.ContainerConstants.TryGetConfig(lootable.TypeId, out var cfg)
                && !string.IsNullOrEmpty(cfg.DisplayName))
                return cfg.DisplayName.ToUpperInvariant();
            return lootable.IsContainer ? "LOOT" : "CORPSE";
        }

        static int FindQuickSlotKey(InventoryState inventory, int backpackIndex)
        {
            var bindings = inventory.QuickSlotBindings;
            for (int q = 0; q < bindings.Length; q++)
                if (bindings[q] == backpackIndex)
                    return q + InventoryState.QuickSlotKeyOffset;
            return -1;
        }

        void HideImmediate()
        {
            if (_root == null) return;
            _isVisible = false;
            _root.style.display = DisplayStyle.None;
        }

        // ── Slot enumeration ──────────────────────────────────

        IEnumerable<InventorySlotElement> EnumerateAllSlots()
        {
            if (_weaponSlots != null)
                foreach (var s in _weaponSlots) yield return s;
            if (_helmetSlot != null) yield return _helmetSlot;
            if (_armorSlot != null)  yield return _armorSlot;
            if (_backpackSlots != null)
                foreach (var s in _backpackSlots) yield return s;
            foreach (var panel in _subPanels.Values)
                for (int i = 0; i < panel.Slots.Count; i++) yield return panel.Slots[i];
        }

        // Player-pane slots that can hold a weapon or a mod (the only slots the attachment
        // compatibility highlight touches). Helmet/armor + right-pane sources excluded.
        IEnumerable<InventorySlotElement> EnumeratePlayerItemSlots()
        {
            if (_weaponSlots != null)
                foreach (var s in _weaponSlots) if (s != null) yield return s;
            if (_backpackSlots != null)
                foreach (var s in _backpackSlots) if (s != null) yield return s;
        }

        // ── Attachment compatibility cross-highlight ──────────
        // Hovering (or dragging) a mod lights up the weapons it can install on; hovering a
        // weapon lights up the mods that fit it. Shared yellow-orange "can upgrade" accent.

        void ApplyCompatHighlight(InventorySlotElement subject)
        {
            ClearCompatHighlight();
            var item = subject?.CurrentItem;
            if (item == null) return;
            var reg = App.Instance?.CoreDefinitions;
            if (reg == null) return;

            var subjectMod = AttachmentInstallSystem.Resolve(reg, item.DefinitionId);
            if (subjectMod != null)
            {
                // Subject is a mod → highlight weapons with a FREE matching slot (can add, not swap).
                foreach (var s in EnumeratePlayerItemSlots())
                {
                    if (s == subject) continue;
                    var w = s.CurrentItem;
                    if (w != null && w.HasWeaponConfiguration && AttachmentInstallSystem.CanInstallIntoFreeSlot(w, subjectMod, reg))
                        s.SetCompatible(true);
                }
                return;
            }

            if (item.HasWeaponConfiguration)
            {
                // Subject is a weapon → highlight mods whose slot is FREE on it (can add, not swap).
                foreach (var s in EnumeratePlayerItemSlots())
                {
                    if (s == subject) continue;
                    var modItem = s.CurrentItem;
                    if (modItem == null) continue;
                    var md = AttachmentInstallSystem.Resolve(reg, modItem.DefinitionId);
                    if (md != null && AttachmentInstallSystem.CanInstallIntoFreeSlot(item, md, reg))
                        s.SetCompatible(true);
                }
            }
        }

        void ClearCompatHighlight()
        {
            foreach (var s in EnumeratePlayerItemSlots())
                s.SetCompatible(false);
        }

        // ── Drag-and-drop ─────────────────────────────────────

        void WireSlotInteractions(InventorySlotElement slot)
        {
            slot.RegisterCallback<PointerDownEvent>(evt => OnSlotPointerDown(slot, evt));
            slot.RegisterCallback<PointerMoveEvent>(evt => OnSlotPointerMove(slot, evt));
            slot.RegisterCallback<PointerUpEvent>(evt   => OnSlotPointerUp(slot, evt));
            slot.RegisterCallback<PointerCaptureOutEvent>(evt => OnSlotPointerCaptureOut(slot, evt));
            slot.RegisterCallback<PointerEnterEvent>(evt => OnSlotPointerEnter(slot, evt));
            slot.RegisterCallback<PointerLeaveEvent>(evt => OnSlotPointerLeave(slot, evt));
        }

        void OnSlotPointerEnter(InventorySlotElement slot, PointerEnterEvent evt)
        {
            _hoveredSlot = slot;
            if (_isDragging) return;
            if (slot.CurrentItem == null) return;

            // Weapon → side-by-side compare vs the equipped baseline (auto). Falls back to the
            // normal single tooltip when nothing is equipped to compare against.
            if (TryShowWeaponCompare(slot, evt.position)) return;

            var tooltip = TooltipController.Instance;
            if (tooltip == null) return;
            // Resolve shop context: if hovering a shop slot use that shop directly
            // (Buy price). Otherwise use any nearby shop (Sell price) — gives stash /
            // loot / player slots a value indicator while trading.
            LootableContainerState shopCtx = null;
            bool itemIsInShop = false;
            if (slot.Source == InventorySlotElement.SlotSource.Loot)
            {
                var lootable = ResolveLootable(slot.SourceLootableId);
                if (lootable != null && lootable.IsShop)
                {
                    shopCtx = lootable;
                    itemIsInShop = true;
                }
            }
            if (shopCtx == null) shopCtx = FindNearbyShop();

            var model = ItemTooltipBuilder.For(slot.CurrentItem,
                App.Instance?.CoreDefinitions, App.Instance?.QuestDatabase,
                shopCtx, itemIsInShop);
            tooltip.ShowFromPanel(model, evt.position);

            // Cross-highlight: hovering a mod lights up weapons it fits (and vice-versa).
            ApplyCompatHighlight(slot);
        }

        void OnSlotPointerLeave(InventorySlotElement slot, PointerLeaveEvent _)
        {
            if (_hoveredSlot == slot) _hoveredSlot = null;
            TooltipController.Instance?.Hide();
            if (!_isDragging) ClearCompatHighlight();
            HideWeaponCompare();
        }

        // Shows the two-column weapon compare (hovered vs equipped baseline) and returns true
        // when it took over from the normal tooltip. False → caller shows the normal tooltip
        // (item isn't a weapon, or nothing is equipped to compare against).
        bool TryShowWeaponCompare(InventorySlotElement slot, Vector2 panelPos)
        {
            var panel = WeaponComparePanel.Instance;
            var item = slot.CurrentItem;
            if (panel == null || item == null || !item.HasWeaponConfiguration) return false;

            var inv = App.Instance?.Player?.Inventory;
            if (inv == null) return false;
            int selected = App.Instance?.RaidSession?.RaidState?.PlayerEntity?.SelectedHotbarSlot ?? -1;

            var candidates = WeaponCompareTarget.Candidates(inv.WeaponSlots, selected, item);
            if (candidates.Count == 0) return false; // nothing equipped → fall back to normal tooltip

            _compareItem = item;
            _comparePos = panelPos;
            _compareCandidates = candidates;
            _compareSelectedSlot = selected;
            _compareHoveredTag = HoveredTag(slot);
            // If Alt is already held when the hover starts, open straight on the alternative.
            _compareAltHeld = Keyboard.current != null && Keyboard.current[Key.LeftAlt].isPressed;

            var baseline = WeaponCompareTarget.Pick(candidates, _compareAltHeld ? 1 : 0);
            TooltipController.Instance?.Hide(); // make sure the single tooltip isn't also up
            panel.Show(item, _compareHoveredTag, baseline, BaselineTag(baseline, inv, selected),
                       candidates.Count > 1, panelPos);
            return true;
        }

        // Tag for the hovered column: LOOT when it's from a lootable source, else EQUIPPED /
        // BACKPACK by which player slot it sits in.
        static string HoveredTag(InventorySlotElement slot)
        {
            if (slot.Source != InventorySlotElement.SlotSource.Player) return "LOOT";
            return slot.SlotRef.Type == SlotType.Weapon ? "EQUIPPED" : "BACKPACK";
        }

        // Tag for the baseline column: IN HAND only when it's the currently-selected weapon,
        // otherwise it's the other equipped weapon → EQUIPPED.
        static string BaselineTag(ItemState baseline, InventoryState inv, int selectedSlot)
        {
            if (inv != null && selectedSlot >= 0 && selectedSlot < inv.WeaponSlots.Length
                && ReferenceEquals(inv.WeaponSlots[selectedSlot], baseline))
                return "IN HAND";
            return "EQUIPPED";
        }

        void HideWeaponCompare()
        {
            _compareItem = null;
            _compareCandidates = null;
            WeaponComparePanel.Instance?.Hide();
        }

        // While inventory open + cursor over a player-backpack consumable,
        // pressing 3..9 binds that item to the quick slot directly (no menu).
        // Mirrors the legacy LootPopupView.HandleQuickSlotKeys path.
        void HandleQuickSlotKeys()
        {
            var hovered = _hoveredSlot;
            if (hovered == null || hovered.CurrentItem == null) return;
            if (hovered.Source != InventorySlotElement.SlotSource.Player) return;
            if (hovered.SlotRef.Type != SlotType.Backpack) return;
            if (!QuickSlotRules.IsAssignable(hovered.CurrentItem.DefinitionId)) return;

            var kb = Keyboard.current;
            if (kb == null) return;

            var inv = App.Instance?.Player?.Inventory;
            if (inv == null) return;

            for (int qi = 0; qi < QuickSlotKeys.Length; qi++)
            {
                if (!kb[QuickSlotKeys[qi]].wasPressedThisFrame) continue;

                int backpackIndex = hovered.SlotRef.Index;

                // Clear any prior binding pointing at this same backpack slot
                // so a single item can't occupy two quick slots.
                for (int i = 0; i < inv.QuickSlotBindings.Length; i++)
                    if (inv.QuickSlotBindings[i] == backpackIndex)
                        inv.QuickSlotBindings[i] = -1;

                inv.QuickSlotBindings[qi] = backpackIndex;
                inv.Version++; // hotbar badge cache invalidate
                break;
            }
        }

        void OnSlotPointerDown(InventorySlotElement slot, PointerDownEvent evt)
        {
            // Ignore input during fade-out — window's `_root` still has
            // display:Flex while opacity transitions, so pointer events can
            // technically fire. Starting a drag here would orphan the ghost
            // when Close()'s scheduled callback collapses display.
            if (!_isVisible) return;

            if (evt.button == 1)
            {
                ShowContextMenu(slot, evt.position);
                evt.StopPropagation();
                return;
            }
            if (evt.button != 0) return;
            if (slot.CurrentItem == null) return;
            if (_draggedSlot != null) return;

            _draggedSlot       = slot;
            _dragPointerId     = evt.pointerId;
            _dragStartPanelPos = evt.position;
            _isDragging        = false;
            slot.CapturePointer(evt.pointerId);
        }

        // ── Context menu ──────────────────────────────────────

        void ShowContextMenu(InventorySlotElement slot, Vector2 panelPos)
        {
            if (slot.CurrentItem == null) { _contextMenu?.Hide(); return; }
            TooltipController.Instance?.Hide();

            _scratchOptions.Clear();
            BuildContextOptions(slot, _scratchOptions);
            if (_scratchOptions.Count == 0) { _contextMenu.Hide(); return; }

            _contextMenu.Show(panelPos, _scratchOptions);
        }

        void BuildContextOptions(InventorySlotElement slot, List<ContextMenuElement.Option> opts)
        {
            switch (slot.Source)
            {
                case InventorySlotElement.SlotSource.Player:
                    BuildPlayerOptions(slot, opts);
                    break;
                case InventorySlotElement.SlotSource.Loot:
                    BuildLootOptions(slot, opts);
                    break;
                case InventorySlotElement.SlotSource.Floor:
                    opts.Add(new ContextMenuElement.Option {
                        Label = "Pick up", Hotkey = "F",
                        OnClick = () => CtxPickUpFromFloor(slot) });
                    break;
                case InventorySlotElement.SlotSource.Stash:
                    opts.Add(new ContextMenuElement.Option {
                        Label = "Take", Hotkey = "F",
                        OnClick = () => CtxTakeFromStash(slot) });
                    break;
            }
        }

        void BuildPlayerOptions(InventorySlotElement slot, List<ContextMenuElement.Option> opts)
        {
            var inv = App.Instance?.Player?.Inventory;
            if (inv == null) return;

            // Weapon attachment editing — opens the modal editor anywhere (P2.2c).
            if (slot.CurrentItem != null && slot.CurrentItem.HasWeaponConfiguration)
            {
                var weapon = slot.CurrentItem;
                opts.Add(new ContextMenuElement.Option {
                    Label = "Modify",
                    OnClick = () => View.UI.Attachments.AttachmentEditorWindow.Instance?.Open(weapon) });
            }

            // Quick-slot bind options for backpack consumables.
            if (slot.SlotRef.Type == SlotType.Backpack &&
                slot.CurrentItem != null &&
                QuickSlotRules.IsAssignable(slot.CurrentItem.DefinitionId))
            {
                int srcIdx = slot.SlotRef.Index;
                int boundAt = -1;
                for (int i = 0; i < inv.QuickSlotBindings.Length; i++)
                    if (inv.QuickSlotBindings[i] == srcIdx) { boundAt = i; break; }

                for (int qi = 0; qi < InventoryState.QuickSlotCount; qi++)
                {
                    int captured = qi;
                    int keyNum = qi + InventoryState.QuickSlotKeyOffset;
                    if (qi == boundAt)
                        opts.Add(new ContextMenuElement.Option {
                            Label = $"Unbind from {keyNum}",
                            OnClick = () => CtxUnbindQuickSlot(captured) });
                    else
                        opts.Add(new ContextMenuElement.Option {
                            Label = $"Bind to {keyNum}",
                            OnClick = () => CtxBindToQuickSlot(srcIdx, captured) });
                }
            }

            if (App.Instance.IsInHideout)
                opts.Add(new ContextMenuElement.Option {
                    Label = "Stash", Hotkey = "Del",
                    OnClick = () => CtxStashPlayer(slot) });
            else
                opts.Add(new ContextMenuElement.Option {
                    Label = "Drop", Hotkey = "Del",
                    OnClick = () => CtxDropPlayer(slot) });

            // Sell — only when a shop is in range. We show the actual sell price so
            // the player doesn't have to drag-and-guess. Picks the first in-range
            // shop; with one NPC at a time this is unambiguous.
            var item = slot.CurrentItem;
            if (item != null && slot.SlotRef.Type == SlotType.Backpack)
            {
                var shop = FindNearbyShop();
                if (shop != null)
                {
                    int price = ShopSystem.GetSellPrice(shop, item);
                    var captured = slot;
                    var capturedShop = shop;
                    opts.Add(new ContextMenuElement.Option {
                        Label = $"Sell ({price}¢)",
                        OnClick = () => CtxSellToShop(captured, capturedShop) });
                }
            }
        }

        void BuildLootOptions(InventorySlotElement slot, List<ContextMenuElement.Option> opts)
        {
            // Buy label when the source is a shop, with the actual cost. Falls back
            // to "Pick up" for free loot / corpses.
            var lootable = ResolveLootable(slot.SourceLootableId);
            if (lootable != null && lootable.IsShop)
            {
                int price = ShopSystem.GetBuyPrice(lootable, slot.CurrentItem);
                opts.Add(new ContextMenuElement.Option {
                    Label = $"Buy ({price}¢)", Hotkey = "F",
                    OnClick = () => CtxPickUpFromLoot(slot) });
                return;
            }

            opts.Add(new ContextMenuElement.Option {
                Label = "Pick up", Hotkey = "F",
                OnClick = () => CtxPickUpFromLoot(slot) });
            opts.Add(new ContextMenuElement.Option {
                Label = "Drop", Hotkey = "Del",
                OnClick = () => CtxDropFromLoot(slot) });
        }

        LootableContainerState FindNearbyShop()
        {
            var state = App.Instance?.RaidSession?.RaidState;
            var player = state?.PlayerEntity;
            if (state == null || player == null) return null;
            float rangeSqr = LootSystem.LootRange * LootSystem.LootRange;
            for (int i = 0; i < state.Lootables.Count; i++)
            {
                var l = state.Lootables[i];
                if (!l.IsShop) continue;
                if ((l.Position - player.Position).sqrMagnitude > rangeSqr) continue;
                return l;
            }
            return null;
        }

        void CtxSellToShop(InventorySlotElement slot, LootableContainerState shop)
        {
            var player = App.Instance?.Player;
            if (player == null || shop == null) return;
            ShopSystem.TrySell(player, shop, slot.SlotRef);
        }

        // ── Context actions ──────────────────────────────────

        void CtxBindToQuickSlot(int backpackIndex, int qi)
        {
            var inv = App.Instance?.Player?.Inventory;
            if (inv == null) return;
            // Clear any prior binding pointing at this same backpack slot.
            for (int i = 0; i < inv.QuickSlotBindings.Length; i++)
                if (inv.QuickSlotBindings[i] == backpackIndex)
                    inv.QuickSlotBindings[i] = -1;
            inv.QuickSlotBindings[qi] = backpackIndex;
            inv.Version++; // hotbar badge у slot view depends on bindings — invalidate cache
        }

        void CtxUnbindQuickSlot(int qi)
        {
            var inv = App.Instance?.Player?.Inventory;
            if (inv == null) return;
            if (qi < 0 || qi >= inv.QuickSlotBindings.Length) return;
            inv.QuickSlotBindings[qi] = -1;
            inv.Version++;
        }

        void CtxDropPlayer(InventorySlotElement slot)
        {
            var session = App.Instance?.RaidSession;
            var state   = session?.RaidState;
            var player  = state?.PlayerEntity;
            var inv     = App.Instance?.Player?.Inventory;
            if (session == null || state == null || player == null || inv == null) return;
            var dropPos = player.Position + player.FacingDirection * 1.5f;
            InventorySystem.TryDrop(state, inv, slot.SlotRef, dropPos, session.ConsumeEvents());
        }

        void CtxStashPlayer(InventorySlotElement slot)
        {
            var inv = App.Instance?.Player?.Inventory;
            if (inv == null) return;
            PushToStash(inv, slot.SlotRef);
        }

        void CtxPickUpFromLoot(InventorySlotElement slot)
        {
            var playerInv = App.Instance?.Player?.Inventory;
            var lootable  = ResolveLootable(slot.SourceLootableId);
            if (playerInv == null || lootable == null) return;
            int free = playerInv.FindFreeBackpackSlot();
            if (free < 0) return;
            var dst = InventorySlotRef.BackpackSlot(free);
            if (lootable.IsShop)
                ShopSystem.TryBuy(App.Instance?.Player, lootable, slot.SlotRef, dst);
            else
                LootSystem.TryTransfer(lootable.Inventory, slot.SlotRef, playerInv, dst);
        }

        void CtxDropFromLoot(InventorySlotElement slot)
        {
            var session = App.Instance?.RaidSession;
            var state   = session?.RaidState;
            var player  = state?.PlayerEntity;
            var lootInv = ResolveLootInventory(slot.SourceLootableId);
            if (session == null || state == null || player == null || lootInv == null) return;

            var dropPos = player.Position + player.FacingDirection * 1.5f;
            InventorySystem.TryDrop(state, lootInv, slot.SlotRef, dropPos,
                session.ConsumeEvents());
        }

        void CtxPickUpFromFloor(InventorySlotElement slot)
        {
            var playerInv = App.Instance?.Player?.Inventory;
            if (playerInv == null) return;
            int free = playerInv.FindFreeBackpackSlot();
            if (free < 0) return;
            PickUpFloorTo(slot.RightIndex, InventorySlotRef.BackpackSlot(free));
        }

        void CtxTakeFromStash(InventorySlotElement slot)
        {
            var playerInv = App.Instance?.Player?.Inventory;
            if (playerInv == null) return;
            int free = playerInv.FindFreeBackpackSlot();
            if (free < 0) return;
            PullFromStash(slot.RightIndex, playerInv, InventorySlotRef.BackpackSlot(free));
        }

        void OnSlotPointerMove(InventorySlotElement slot, PointerMoveEvent evt)
        {
            if (_draggedSlot != slot) return;
            if (!slot.HasPointerCapture(evt.pointerId)) return;

            if (!_isDragging)
            {
                Vector2 delta = (Vector2)evt.position - _dragStartPanelPos;
                if (delta.sqrMagnitude < DragThreshold * DragThreshold) return;
                _isDragging = true;
                TooltipController.Instance?.Hide();
                HideWeaponCompare();
                CreateGhost(slot);
                // Dragging a mod highlights the weapons it can fill; dragging a weapon highlights
                // the mods that fit its free slots. No-op for other item kinds.
                ApplyCompatHighlight(slot);
            }

            UpdateGhostPosition(evt.position);
            UpdateSlotHover(evt.position);
        }

        void OnSlotPointerUp(InventorySlotElement slot, PointerUpEvent evt)
        {
            if (_draggedSlot != slot) return;

            if (slot.HasPointerCapture(evt.pointerId))
                slot.ReleasePointer(evt.pointerId);

            if (_isDragging)
            {
                TryDropOnSlot(evt.position);
                DestroyGhost();
                ClearAllSlotHover();
            }

            _draggedSlot   = null;
            _dragPointerId = -1;
            _isDragging    = false;
        }

        void OnSlotPointerCaptureOut(InventorySlotElement slot, PointerCaptureOutEvent _)
        {
            if (_draggedSlot != slot) return;
            DestroyGhost();
            ClearAllSlotHover();
            _draggedSlot   = null;
            _dragPointerId = -1;
            _isDragging    = false;
        }

        void CancelActiveDrag()
        {
            if (_draggedSlot == null) return;
            if (_dragPointerId >= 0 && _draggedSlot.HasPointerCapture(_dragPointerId))
                _draggedSlot.ReleasePointer(_dragPointerId);
            DestroyGhost();
            ClearAllSlotHover();
            _draggedSlot   = null;
            _dragPointerId = -1;
            _isDragging    = false;
        }

        // ── Drop / hover detection ────────────────────────────

        void TryDropOnSlot(Vector2 panelPos)
        {
            if (_draggedSlot == null) return;
            var target = SlotUnder(panelPos);

            // Drop outside any slot → priority order:
            //  1. backpack→hotbar drag-bind (only when item is QuickSlotRules-assignable);
            //  2. silent cancel if drop landed on ANY UTK panel (inv body, sub-panel
            //     gap, hotbar but not over a slot, builder palette, tooltip, etc);
            //  3. drop-to-ground/stash if drop is completely outside pick-enabled UI.
            // Uses panel.Pick across all live docs so future modals automatically
            // count as "do not drop here".
            if (target == null)
            {
                Vector2 mouseScreen = UnityEngine.InputSystem.Mouse.current?.position.ReadValue() ?? Vector2.zero;

                if (_draggedSlot.Source == InventorySlotElement.SlotSource.Player &&
                    _draggedSlot.SlotRef.Type == SlotType.Backpack &&
                    HotbarOverlay.Instance != null &&
                    HotbarOverlay.Instance.TryBindFromBackpack(mouseScreen, _draggedSlot.SlotRef.Index))
                {
                    RefreshAll();
                    return;
                }

                // Drop landed inside a sub-panel's empty area (not on a slot) —
                // treat as "transfer to this panel" by auto-picking a free slot.
                var subPanel = FindSubPanelAt(panelPos);
                if (subPanel != null && TryDropOnSubPanel(subPanel))
                {
                    RefreshAll();
                    return;
                }

                if (UiPanelHitTest.IsScreenPointOverUi(mouseScreen)) return;
                DropOutsideSlot();
                return;
            }
            if (target == _draggedSlot) return;

            bool ok = false;
            if (_draggedSlot.Source == InventorySlotElement.SlotSource.Player &&
                target.Source == InventorySlotElement.SlotSource.Player)
            {
                var inv = App.Instance?.Player?.Inventory;
                var state = App.Instance?.RaidSession?.RaidState;

                // Attachment install in EITHER direction (mod→weapon or weapon→mod) — ahead of the
                // weapon-swap / plain-move paths. Same resolver + Install as CanDropOnTarget and
                // the hover/drag highlight, so the two drag directions are identical by
                // construction. Mutates the weapon's config + bumps inventory.Version, so
                // RefreshAll re-binds (pips/icons) below.
                ItemState installWeapon = null;
                string installModId = null;
                bool installMod = inv != null
                    && TryResolveAttachmentInstall(_draggedSlot, target, out installWeapon, out installModId);

                // Weapon↔weapon swap during a raid must route through HotbarWeaponSystem so the
                // equipped weapon follows to its new slot + magazines are preserved (plain TryMove
                // only swaps inventory refs → WeaponSyncSystem rebuilds → selection stuck on the
                // index + mag reset). Outside a raid (no PlayerEntity) there's no equipped weapon —
                // fall through to the plain inventory swap.
                bool weaponSwap = _draggedSlot.SlotRef.Type == SlotType.Weapon
                                  && target.SlotRef.Type == SlotType.Weapon;
                if (installMod)
                {
                    ok = AttachmentInstallSystem.Install(installWeapon,
                        App.Instance.CoreDefinitions, inv, App.Instance.AllocateEId, installModId, out _);
                }
                else if (inv != null && weaponSwap && state?.PlayerEntity != null)
                {
                    HotbarWeaponSystem.SwapWeaponSlots(state, inv,
                        _draggedSlot.SlotRef.Index, target.SlotRef.Index);
                    ok = true;
                }
                else
                {
                    ok = inv != null && InventorySystem.TryMove(inv, _draggedSlot.SlotRef, target.SlotRef);
                }
            }
            else
            {
                ok = TryCrossSourceDrop(_draggedSlot, target);
            }

            if (ok) RefreshAll();
        }

        bool TryCrossSourceDrop(InventorySlotElement src, InventorySlotElement tgt)
        {
            var playerInv = App.Instance?.Player?.Inventory;
            if (playerInv == null) return false;

            // Player → Right
            if (src.Source == InventorySlotElement.SlotSource.Player)
            {
                switch (tgt.Source)
                {
                    case InventorySlotElement.SlotSource.Loot:
                    {
                        if (!IsLootableInRange(tgt.SourceLootableId)) return false;
                        var tgtLootable = ResolveLootable(tgt.SourceLootableId);
                        if (tgtLootable == null) return false;
                        if (tgtLootable.IsShop)
                            return ShopSystem.TrySell(App.Instance?.Player, tgtLootable, src.SlotRef);
                        return LootSystem.TryTransfer(playerInv, src.SlotRef, tgtLootable.Inventory, tgt.SlotRef);
                    }
                    case InventorySlotElement.SlotSource.Stash:
                        return PushToStash(playerInv, src.SlotRef);
                    case InventorySlotElement.SlotSource.Floor:
                        return false; // floor cells are read-only as drop targets
                    default:
                        return false;
                }
            }

            // Right → Player
            if (tgt.Source == InventorySlotElement.SlotSource.Player)
            {
                switch (src.Source)
                {
                    case InventorySlotElement.SlotSource.Loot:
                    {
                        if (!IsLootableInRange(src.SourceLootableId)) return false;
                        var srcLootable = ResolveLootable(src.SourceLootableId);
                        if (srcLootable == null) return false;
                        if (srcLootable.IsShop)
                            return ShopSystem.TryBuy(App.Instance?.Player, srcLootable, src.SlotRef, tgt.SlotRef);
                        return LootSystem.TryTransfer(srcLootable.Inventory, src.SlotRef, playerInv, tgt.SlotRef);
                    }
                    case InventorySlotElement.SlotSource.Stash:
                        return PullFromStash(src.RightIndex, playerInv, tgt.SlotRef);
                    case InventorySlotElement.SlotSource.Floor:
                        return PickUpFloorTo(src.RightIndex, tgt.SlotRef);
                    default:
                        return false;
                }
            }

            // Right → Right (only meaningful inside the SAME loot container — reorder).
            if (src.Source == InventorySlotElement.SlotSource.Loot &&
                tgt.Source == InventorySlotElement.SlotSource.Loot &&
                src.SourceLootableId == tgt.SourceLootableId)
            {
                if (!IsLootableInRange(src.SourceLootableId)) return false;
                var lootInv = ResolveLootInventory(src.SourceLootableId);
                return lootInv != null && InventorySystem.TryMove(lootInv, src.SlotRef, tgt.SlotRef);
            }

            return false;
        }

        // Re-check at drop time that the lootable is still within LootRange. UI
        // sub-panels are reconciled to keep nearby-only, але mid-drag refresh
        // is skipped (RefreshAll early-out on _isDragging), тож player може
        // walk out of range while a drag is in flight. Без цієї гарантії
        // TryTransfer would succeed against an out-of-range container.
        bool IsLootableInRange(EId lootableId)
        {
            var state  = App.Instance?.RaidSession?.RaidState;
            var player = state?.PlayerEntity;
            if (state == null || player == null) return false;
            var lootable = LootSystem.GetLootable(state, lootableId);
            if (lootable == null) return false;

            var d = lootable.Position - player.Position;
            float r = LootSystem.LootRange;
            return d.sqrMagnitude <= r * r;
        }

        InventoryState ResolveLootInventory(EId lootableId)
        {
            var state = App.Instance?.RaidSession?.RaidState;
            if (state == null) return null;
            return LootSystem.GetLootable(state, lootableId)?.Inventory;
        }

        LootableContainerState ResolveLootable(EId lootableId)
        {
            var state = App.Instance?.RaidSession?.RaidState;
            return state != null ? LootSystem.GetLootable(state, lootableId) : null;
        }

        LootSubPanelElement FindSubPanelAt(Vector2 panelPos)
        {
            foreach (var p in _subPanels.Values)
                if (p.worldBound.Contains(panelPos)) return p;
            return null;
        }

        bool TryDropOnSubPanel(LootSubPanelElement subPanel)
        {
            var playerInv = App.Instance?.Player?.Inventory;
            if (playerInv == null || _draggedSlot == null) return false;
            if (_draggedSlot.Source != InventorySlotElement.SlotSource.Player) return false;

            switch (subPanel.SlotSource)
            {
                case InventorySlotElement.SlotSource.Loot:
                {
                    var lootable = ResolveLootable(subPanel.LootableId);
                    if (lootable == null) return false;
                    if (!IsLootableInRange(subPanel.LootableId)) return false;
                    if (lootable.IsShop)
                        return ShopSystem.TrySell(App.Instance.Player, lootable, _draggedSlot.SlotRef);
                    int free = lootable.Inventory.FindFreeBackpackSlot();
                    if (free < 0) return false;
                    return LootSystem.TryTransfer(playerInv, _draggedSlot.SlotRef,
                        lootable.Inventory, InventorySlotRef.BackpackSlot(free));
                }
                case InventorySlotElement.SlotSource.Stash:
                    return PushToStash(playerInv, _draggedSlot.SlotRef);
                default:
                    return false;
            }
        }

        // Thin View-side adapters that route to the System layer. State
        // mutations + event emission live у StashSystem / InventorySystem —
        // View contains no gameplay rules (CLAUDE.md §3.10).

        static bool PushToStash(InventoryState playerInv, InventorySlotRef src) =>
            StashSystem.TryDeposit(playerInv, App.Instance?.Player?.Stash, src);

        static bool PullFromStash(int stashIndex, InventoryState playerInv, InventorySlotRef tgt) =>
            StashSystem.TryWithdraw(App.Instance?.Player?.Stash, stashIndex, playerInv, tgt);

        bool PickUpFloorTo(int floorIndex, InventorySlotRef tgt)
        {
            if (floorIndex < 0 || floorIndex >= _floorItems.Count) return false;
            var gi = _floorItems[floorIndex];

            var playerInv = App.Instance?.Player?.Inventory;
            var session   = App.Instance?.RaidSession;
            var state     = session?.RaidState;
            if (playerInv == null || state == null) return false;

            return InventorySystem.TryPickUpToSlot(state, playerInv, gi.Id, tgt,
                session.ConsumeEvents());
        }

        void DropOutsideSlot()
        {
            if (_draggedSlot == null) return;
            // Only player-owned items can drop out (loot/floor/stash items stay
            // in their source if released outside any cell — silent cancel).
            if (_draggedSlot.Source != InventorySlotElement.SlotSource.Player) return;

            var playerInv = App.Instance?.Player?.Inventory;
            if (playerInv == null) return;

            if (App.Instance.IsInHideout)
            {
                if (PushToStash(playerInv, _draggedSlot.SlotRef))
                    RefreshAll();
                return;
            }

            var session = App.Instance.RaidSession;
            var state   = session?.RaidState;
            var player  = state?.PlayerEntity;
            if (session == null || state == null || player == null) return;

            var dropPos = player.Position + player.FacingDirection * 1.5f;
            if (InventorySystem.TryDrop(state, playerInv, _draggedSlot.SlotRef,
                                        dropPos, session.ConsumeEvents()))
            {
                RefreshAll();
            }
        }

        InventorySlotElement SlotUnder(Vector2 panelPos)
        {
            foreach (var s in EnumerateAllSlots())
            {
                if (s.style.display == DisplayStyle.None) continue;
                if (s.worldBound.Contains(panelPos)) return s;
            }
            return null;
        }

        void UpdateSlotHover(Vector2 panelPos)
        {
            if (_draggedSlot == null) return;

            foreach (var s in EnumerateAllSlots())
            {
                if (s.style.display == DisplayStyle.None) { s.SetDragOver(false, false); continue; }
                bool over = s.worldBound.Contains(panelPos);
                if (!over || s == _draggedSlot)
                {
                    s.SetDragOver(false, false);
                    continue;
                }
                s.SetDragOver(CanDropOnTarget(s), true);
            }
        }

        void ClearAllSlotHover()
        {
            foreach (var s in EnumerateAllSlots())
                s.SetDragOver(false, false);
            ClearCompatHighlight(); // also drop the drag-time attachment highlight
        }

        // Resolves an attachment-install from a (dragged, target) slot pair in EITHER direction —
        // a mod dropped on a weapon, or a weapon dropped on a mod. Returns the weapon ItemState +
        // the mod id to install when exactly one side is a built weapon and the other is an
        // installable mod the weapon can take. Direction-agnostic on purpose: both drop
        // directions funnel through this one resolver + AttachmentInstallSystem.Install, so the
        // behaviour is identical by construction.
        bool TryResolveAttachmentInstall(InventorySlotElement src, InventorySlotElement tgt,
                                         out ItemState weapon, out string modId)
        {
            weapon = null;
            modId  = null;
            var reg = App.Instance?.CoreDefinitions;
            if (reg == null || src?.CurrentItem == null || tgt?.CurrentItem == null) return false;

            var srcItem = src.CurrentItem;
            var tgtItem = tgt.CurrentItem;

            // src is the mod, tgt is the weapon.
            var srcMod = AttachmentInstallSystem.Resolve(reg, srcItem.DefinitionId);
            if (srcMod != null && tgtItem.HasWeaponConfiguration
                && AttachmentInstallSystem.CanInstall(tgtItem, srcMod, reg))
            {
                weapon = tgtItem; modId = srcMod.Id; return true;
            }

            // src is the weapon, tgt is the mod.
            var tgtMod = AttachmentInstallSystem.Resolve(reg, tgtItem.DefinitionId);
            if (tgtMod != null && srcItem.HasWeaponConfiguration
                && AttachmentInstallSystem.CanInstall(srcItem, tgtMod, reg))
            {
                weapon = srcItem; modId = tgtMod.Id; return true;
            }

            return false;
        }

        // Hover-preview validity. Real drop logic re-validates inside the
        // System call — this just decides green vs red highlight.
        bool CanDropOnTarget(InventorySlotElement target)
        {
            if (target == null || _draggedSlot == null || target == _draggedSlot) return false;
            var item = _draggedSlot.CurrentItem;
            if (item?.Definition == null) return false;

            // Attachment install (either direction — mod→weapon or weapon→mod), player→player.
            // Valid even though a mod's AllowedSlots is Backpack (not the weapon slot type), so
            // it's checked before the generic slot-type gate below.
            if (_draggedSlot.Source == InventorySlotElement.SlotSource.Player
                && target.Source == InventorySlotElement.SlotSource.Player
                && TryResolveAttachmentInstall(_draggedSlot, target, out _, out _))
                return true;

            var src = _draggedSlot;
            var tgtSlotType = target.SlotRef.ToItemSlotType();

            // Floor cells are read-only as drop targets.
            if (target.Source == InventorySlotElement.SlotSource.Floor) return false;

            // Target slot must accept the dragged item's type (all right-pane
            // slots use Backpack-typed refs, so this filters on AllowedSlots).
            if ((item.Definition.AllowedSlots & tgtSlotType) == 0) return false;

            bool srcPlayer = src.Source == InventorySlotElement.SlotSource.Player;
            bool tgtPlayer = target.Source == InventorySlotElement.SlotSource.Player;

            if (srcPlayer && tgtPlayer)
            {
                var inv = App.Instance?.Player?.Inventory;
                if (inv == null) return false;
                var tgtItem = inv.GetSlot(target.SlotRef);
                if (tgtItem == null) return true;
                var srcSlotType = src.SlotRef.ToItemSlotType();
                return tgtItem.Definition != null
                    && (tgtItem.Definition.AllowedSlots & srcSlotType) != 0;
            }

            // Right → Right across different sources — only allowed when both are
            // the same lootable (reorder). Other combos rejected.
            if (!srcPlayer && !tgtPlayer)
            {
                if (src.Source != target.Source) return false;
                if (src.Source == InventorySlotElement.SlotSource.Loot)
                    return src.SourceLootableId == target.SourceLootableId;
                return false;
            }

            // Cross-source (player ↔ right):
            // Loot allows swap (TryTransfer handles it); Stash player→stash always OK,
            // stash→player only if target empty; Floor → player only if target empty.
            if (target.Source == InventorySlotElement.SlotSource.Loot ||
                src.Source    == InventorySlotElement.SlotSource.Loot)
            {
                var lootInv = ResolveLootInventory(
                    target.Source == InventorySlotElement.SlotSource.Loot
                        ? target.SourceLootableId : src.SourceLootableId);
                if (lootInv == null) return false;
                var tgtItem = tgtPlayer
                    ? App.Instance?.Player?.Inventory?.GetSlot(target.SlotRef)
                    : lootInv.GetSlot(target.SlotRef);
                if (tgtItem == null) return true;
                var srcSlotType = src.SlotRef.ToItemSlotType();
                return tgtItem.Definition != null
                    && (tgtItem.Definition.AllowedSlots & srcSlotType) != 0;
            }

            // Stash / Floor: target slot must be empty when dropping player-side.
            if (tgtPlayer)
                return App.Instance?.Player?.Inventory?.GetSlot(target.SlotRef) == null;

            // Player → Stash: append behaviour, always valid as long as type accepts.
            return target.Source == InventorySlotElement.SlotSource.Stash;
        }

        // ── Ghost ─────────────────────────────────────────────

        void CreateGhost(InventorySlotElement source)
        {
            DestroyGhost();
            _dragGhost = new VisualElement { pickingMode = PickingMode.Ignore };
            _dragGhost.AddToClassList("inv-slot");
            _dragGhost.AddToClassList(source.Kind == InventorySlotElement.SlotKind.Backpack
                ? "inv-slot--bp" : "inv-slot--eq");
            _dragGhost.AddToClassList("inv-drag-ghost");

            var registry = App.Instance?.CoreDefinitions;
            var name = new Label(WeaponDisplayName.For(source.CurrentItem, registry));
            name.AddToClassList("inv-slot__name");
            name.pickingMode = PickingMode.Ignore;
            _dragGhost.Add(name);

            _root.Add(_dragGhost);
        }

        void UpdateGhostPosition(Vector2 panelPos)
        {
            if (_dragGhost == null) return;
            float w = _dragGhost.resolvedStyle.width;
            float h = _dragGhost.resolvedStyle.height;
            if (w <= 0f) w = 130f;
            if (h <= 0f) h = 130f;
            _dragGhost.style.left = panelPos.x - w * 0.5f;
            _dragGhost.style.top  = panelPos.y - h * 0.5f;
        }

        void DestroyGhost()
        {
            if (_dragGhost == null) return;
            _dragGhost.RemoveFromHierarchy();
            _dragGhost = null;
        }
    }
}
