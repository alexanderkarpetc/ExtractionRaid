using System.Collections.Generic;
using ApplicationCore;
using State;
using Systems;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using View.UI.Tooltip;
using View.UI.Tooltip.Builders;

namespace View.UI.Inventory
{
    /// <summary>
    /// Runtime UI Toolkit inventory modal — UI Toolkit replacement for the
    /// legacy uGUI <c>LootPopupView</c> canvas.
    ///
    /// Layout (Stage 3 polish):
    ///   - Main window left-anchored, holds only the player pane.
    ///   - Floating <see cref="LootSubPanelElement"/> sub-panels stack to the
    ///     right of the window — one per nearby lootable / corpse / floor /
    ///     hideout stash. Mirrors the legacy multi-container view, while
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
        VisualElement _window;
        Button _closeBtn;

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

        // ── Drag state ────────────────────────────────────────
        const float DragThreshold = 4f;
        InventorySlotElement _draggedSlot;
        int _dragPointerId = -1;
        Vector2 _dragStartPanelPos;
        bool _isDragging;
        VisualElement _dragGhost;
        bool _suppressNextClick;

        // ── Hover state (for tooltip + hover-key quick-slot bind) ─
        InventorySlotElement _hoveredSlot;

        // Quick-slot keys 3..9 → bindings index 0..6.
        static readonly Key[] QuickSlotKeys =
        {
            Key.Digit3, Key.Digit4, Key.Digit5,
            Key.Digit6, Key.Digit7, Key.Digit8, Key.Digit9,
        };

        bool _isVisible;

        public bool IsOpen => _isVisible;

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
        }

        // ── Public API ────────────────────────────────────────

        public void Toggle()
        {
            if (_isVisible) Close(); else Open();
        }

        public void Open()
        {
            if (_root == null) return;
            _isVisible = true;
            _root.style.display = DisplayStyle.Flex;
            RefreshAll();
        }

        public void Close()
        {
            if (_root == null) return;
            CancelActiveDrag();
            _isVisible = false;
            _root.style.display = DisplayStyle.None;
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

            _window         = _root.Q<VisualElement>("window");
            _closeBtn       = _root.Q<Button>("closeBtn");
            _equipmentRow   = _root.Q<VisualElement>("equipmentRow");
            _backpackGrid   = _root.Q<VisualElement>("backpackGrid");
            _subPanelsHost  = _root.Q<VisualElement>("subPanels");

            if (_closeBtn != null)
                _closeBtn.clicked += Close;
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
            }
        }

        // ── Refresh ───────────────────────────────────────────

        void RefreshAll()
        {
            if (_isDragging) return; // Stale slot refs would break if we reconcile mid-drag.

            var inventory = App.Instance?.Player?.Inventory;
            var registry  = App.Instance?.CoreDefinitions;

            if (inventory == null)
            {
                ClearPlayerSlots();
                RemoveAllSubPanels();
                return;
            }

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

            RefreshSubPanels(registry);
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

            var wanted = new HashSet<string>();

            if (App.Instance != null && App.Instance.IsInHideout)
            {
                BindStashPanel(wanted, registry);
            }
            else
            {
                var state  = App.Instance?.RaidSession?.RaidState;
                var player = state?.PlayerEntity;
                if (state != null && player != null)
                {
                    BindLootablePanels(state, player.Position, wanted, registry);
                    BindFloorPanel(state, player.Position, wanted, registry);
                }
            }

            // Sweep out sub-panels for sources that disappeared (player walked
            // away, container got cleaned up, hideout returned to raid, etc).
            _scratchRemoveKeys.Clear();
            foreach (var key in _subPanels.Keys)
                if (!wanted.Contains(key)) _scratchRemoveKeys.Add(key);
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
            for (int li = 0; li < state.Lootables.Count; li++)
            {
                var lootable = state.Lootables[li];
                if (Vector3.Distance(playerPos, lootable.Position) > LootSystem.LootRange) continue;
                if (lootable.Inventory == null) continue;

                var key = "loot:" + lootable.Id;
                wanted.Add(key);
                var panel = EnsureSubPanel(key, ResolveLootableTitle(lootable),
                    InventorySlotElement.SlotSource.Loot, lootable.Id);

                var inv = lootable.Inventory;
                panel.EnsureSlotCount(inv.Backpack.Length);
                for (int i = 0; i < inv.Backpack.Length; i++)
                {
                    var slot = panel.Slots[i];
                    slot.RightIndex = i;
                    slot.Bind(InventorySlotRef.BackpackSlot(i), inv.Backpack[i], -1, registry);
                }
            }
        }

        void BindFloorPanel(RaidState state, Vector3 playerPos,
            HashSet<string> wanted, Adapters.ICoreDefinitionRegistry registry)
        {
            _floorItems.Clear();
            for (int i = 0; i < state.GroundItems.Count; i++)
            {
                var gi = state.GroundItems[i];
                if (Vector3.Distance(playerPos, gi.Position) <= LootSystem.LootRange)
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
                panel.SetSourceKey(key);
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

        static string ResolveLootableTitle(LootableContainerState lootable)
        {
            if (lootable == null) return "LOOT";
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
            var tooltip = TooltipController.Instance;
            if (tooltip == null) return;
            var model = ItemTooltipBuilder.For(slot.CurrentItem,
                App.Instance?.CoreDefinitions, App.Instance?.QuestDatabase);
            tooltip.ShowFromPanel(model, evt.position);
        }

        void OnSlotPointerLeave(InventorySlotElement slot, PointerLeaveEvent _)
        {
            if (_hoveredSlot == slot) _hoveredSlot = null;
            TooltipController.Instance?.Hide();
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
                break;
            }
        }

        void OnSlotPointerDown(InventorySlotElement slot, PointerDownEvent evt)
        {
            if (evt.button != 0) return;
            if (slot.CurrentItem == null) return;
            if (_draggedSlot != null) return;

            _draggedSlot       = slot;
            _dragPointerId     = evt.pointerId;
            _dragStartPanelPos = evt.position;
            _isDragging        = false;
            slot.CapturePointer(evt.pointerId);
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
                CreateGhost(slot);
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
                _suppressNextClick = true;
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
            _draggedSlot       = null;
            _dragPointerId     = -1;
            _isDragging        = false;
            _suppressNextClick = false;
        }

        // ── Drop / hover detection ────────────────────────────

        void TryDropOnSlot(Vector2 panelPos)
        {
            if (_draggedSlot == null) return;
            var target = SlotUnder(panelPos);

            // Drop outside any slot → drop-to-ground (raid) or add-to-stash (hideout).
            if (target == null)
            {
                DropOutsideSlot();
                return;
            }
            if (target == _draggedSlot) return;

            bool ok = false;
            if (_draggedSlot.Source == InventorySlotElement.SlotSource.Player &&
                target.Source == InventorySlotElement.SlotSource.Player)
            {
                var inv = App.Instance?.Player?.Inventory;
                ok = inv != null && InventorySystem.TryMove(inv, _draggedSlot.SlotRef, target.SlotRef);
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
                        var lootInv = ResolveLootInventory(tgt.SourceLootableId);
                        return lootInv != null &&
                               LootSystem.TryTransfer(playerInv, src.SlotRef, lootInv, tgt.SlotRef);
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
                        var lootInv = ResolveLootInventory(src.SourceLootableId);
                        return lootInv != null &&
                               LootSystem.TryTransfer(lootInv, src.SlotRef, playerInv, tgt.SlotRef);
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
                var lootInv = ResolveLootInventory(src.SourceLootableId);
                return lootInv != null && InventorySystem.TryMove(lootInv, src.SlotRef, tgt.SlotRef);
            }

            return false;
        }

        InventoryState ResolveLootInventory(EId lootableId)
        {
            var state = App.Instance?.RaidSession?.RaidState;
            if (state == null) return null;
            return LootSystem.GetLootable(state, lootableId)?.Inventory;
        }

        bool PushToStash(InventoryState playerInv, InventorySlotRef src)
        {
            var item = playerInv.GetSlot(src);
            if (item == null) return false;
            var stash = App.Instance?.Player?.Stash;
            if (stash == null) return false;
            playerInv.SetSlot(src, null);
            stash.Add(item);
            return true;
        }

        bool PullFromStash(int stashIndex, InventoryState playerInv, InventorySlotRef tgt)
        {
            var stash = App.Instance?.Player?.Stash;
            if (stash == null || stashIndex < 0 || stashIndex >= stash.Count) return false;
            var item = stash[stashIndex];
            if (item?.Definition == null) return false;
            if ((item.Definition.AllowedSlots & tgt.ToItemSlotType()) == 0) return false;
            if (playerInv.GetSlot(tgt) != null) return false;
            playerInv.SetSlot(tgt, item);
            stash.RemoveAt(stashIndex);
            return true;
        }

        bool PickUpFloorTo(int floorIndex, InventorySlotRef tgt)
        {
            if (floorIndex < 0 || floorIndex >= _floorItems.Count) return false;
            var gi = _floorItems[floorIndex];
            var def = ItemDefinition.Get(gi.DefinitionId);
            if (def == null || (def.AllowedSlots & tgt.ToItemSlotType()) == 0) return false;

            var playerInv = App.Instance?.Player?.Inventory;
            var session   = App.Instance?.RaidSession;
            var state     = session?.RaidState;
            if (playerInv == null || state == null) return false;
            if (playerInv.GetSlot(tgt) != null) return false;

            var item = gi.HasWeaponConfiguration
                ? ItemState.CreateWeapon(gi.Id, gi.DefinitionId, gi.WeaponConfiguration)
                : ItemState.Create(gi.Id, gi.DefinitionId, gi.StackCount);
            playerInv.SetSlot(tgt, item);

            for (int i = 0; i < state.GroundItems.Count; i++)
            {
                if (state.GroundItems[i].Id != gi.Id) continue;
                state.GroundItems.RemoveAt(i);
                break;
            }
            session.ConsumeEvents().GroundItemDespawned(gi.Id);
            return true;
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
        }

        // Hover-preview validity. Real drop logic re-validates inside the
        // System call — this just decides green vs red highlight.
        bool CanDropOnTarget(InventorySlotElement target)
        {
            if (target == null || _draggedSlot == null || target == _draggedSlot) return false;
            var item = _draggedSlot.CurrentItem;
            if (item?.Definition == null) return false;

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
