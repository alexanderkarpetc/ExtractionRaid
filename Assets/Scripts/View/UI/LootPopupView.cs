using System.Collections.Generic;
using State;
using Systems;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace View.UI
{
    public class LootPopupView : PopupBase
    {
        static readonly HashSet<string> QuickSlotAssignable = new() { "Medkit", "Bandage", "Grenade" };

        static readonly Key[] QuickSlotKeys =
        {
            Key.Digit3, Key.Digit4, Key.Digit5,
            Key.Digit6, Key.Digit7, Key.Digit8, Key.Digit9,
        };

        [Header("Player panel")]
        [SerializeField] InventoryPanelView _playerPanel;

        [Header("Loot panel (right side)")]
        [SerializeField] Transform _lootContainerParent;
        [SerializeField] LootContainerView _containerPrefab;

        [Header("Drag ghost (InventorySlotView inside LootPopup)")]
        [SerializeField] InventorySlotView _dragGhost;
        [SerializeField] CanvasGroup _dragGhostGroup;

        [Header("Overlays (inside LootPopup)")]
        [SerializeField] ContextMenuView _contextMenu;

        readonly List<LootContainerView> _activeContainers = new();
        readonly List<LootContainerView> _containerPool = new();

        // drag state
        SlotViewBase _dragSource;
        LootContainerView _dragSourceContainer;
        Vector2 _dragOffset;

        // floor state
        InventoryState _floorInventory;
        EId[] _floorItemEIds;

        Canvas _rootCanvas;

        protected override void Awake()
        {
            base.Awake();
            _rootCanvas = GetComponentInParent<Canvas>(includeInactive: true)?.rootCanvas;
            HideDragGhost();
            _playerPanel.Init();

            _playerPanel.SlotDragStarted += OnPlayerSlotDragStarted;
            _playerPanel.SlotDragEnded += OnSlotDragEnded;
            _playerPanel.SlotDropReceived += OnPlayerSlotDropReceived;
            _playerPanel.SlotRightClicked += OnPlayerSlotRightClicked;
        }

        public override void Hide()
        {
            CancelDrag();
            if (_contextMenu != null) _contextMenu.Hide();
            base.Hide();
        }

        // ------------------------------------------------------------------
        // Public API — called by InventoryUI
        // ------------------------------------------------------------------

        public void Open(RaidState state)
        {
            _playerPanel.Bind(state.Inventory);

            RebuildLootContainers(state);
            Refresh();
        }

        public void Refresh()
        {
            _playerPanel.Refresh();
            foreach (var c in _activeContainers)
                c.Refresh();
        }

        // ------------------------------------------------------------------
        // Loot container management
        // ------------------------------------------------------------------

        void RebuildLootContainers(RaidState state)
        {
            ReturnAllContainers();

            var playerPos = state.PlayerEntity.Position;

            for (int i = 0; i < state.Lootables.Count; i++)
            {
                var lootable = state.Lootables[i];
                if (Vector3.Distance(playerPos, lootable.Position) > LootSystem.LootRange)
                    continue;

                var container = GetContainer();
                container.BindLootable(lootable);
                _activeContainers.Add(container);
            }

            RebuildFloorInventory(state, playerPos);
            bool hasFloorItems = false;
            for (int i = 0; i < InventoryState.BackpackSize; i++)
            {
                if (_floorInventory.Backpack[i] != null) { hasFloorItems = true; break; }
            }

            if (hasFloorItems)
            {
                var floorContainer = GetContainer();
                floorContainer.BindFloor(_floorInventory, _floorItemEIds);
                _activeContainers.Add(floorContainer);
            }
        }

        LootContainerView GetContainer()
        {
            LootContainerView c;
            if (_containerPool.Count > 0)
            {
                c = _containerPool[_containerPool.Count - 1];
                _containerPool.RemoveAt(_containerPool.Count - 1);
            }
            else
            {
                c = Instantiate(_containerPrefab, _lootContainerParent);
                c.Init();
            c.SlotDragStarted += OnLootSlotDragStarted;
            c.SlotDragEnded += (_, s) => OnSlotDragEnded(s);
            c.SlotDropReceived += OnLootSlotDropReceived;
            c.SlotRightClicked += OnLootSlotRightClicked;
            }

            c.gameObject.SetActive(true);
            return c;
        }

        void ReturnAllContainers()
        {
            foreach (var c in _activeContainers)
            {
                c.gameObject.SetActive(false);
                _containerPool.Add(c);
            }
            _activeContainers.Clear();
        }

        // ------------------------------------------------------------------
        // Update
        // ------------------------------------------------------------------

        void Update()
        {
            if (!IsOpen) return;

            if (_dragSource != null)
                MoveDragGhost();

            HandleQuickSlotKeys();
            HandleContextMenuDismiss();
        }

        void HandleQuickSlotKeys()
        {
            var inv = _playerPanel.BoundInventory;
            if (inv == null) return;
            var kb = Keyboard.current;
            if (kb == null) return;

            for (int qi = 0; qi < QuickSlotKeys.Length; qi++)
            {
                if (!kb[QuickSlotKeys[qi]].wasPressedThisFrame) continue;

                var hovered = FindHoveredPlayerBackpackSlot();
                if (hovered == null) continue;
                if (hovered.CurrentItem == null) continue;
                if (!QuickSlotAssignable.Contains(hovered.CurrentItem.DefinitionId)) continue;

                inv.QuickSlotBindings[qi] = hovered.SlotRef.Index;
                Refresh();
                break;
            }
        }

        InventorySlotView FindHoveredPlayerBackpackSlot()
        {
            foreach (var slot in _playerPanel.GetComponentsInChildren<InventorySlotView>())
            {
                if (slot.IsHovered && !slot.IsLoot && slot.SlotRef.Type == SlotType.Backpack)
                    return slot;
            }
            return null;
        }

        // HandleDragCancel removed — OnEndDrag on the source slot drives this now.

        void HandleContextMenuDismiss()
        {
            if (_contextMenu == null || !_contextMenu.IsVisible) return;
            var mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.wasPressedThisFrame)
                _contextMenu.Hide();
        }

        // ------------------------------------------------------------------
        // Drag & drop — player panel
        // ------------------------------------------------------------------

        void OnPlayerSlotDragStarted(SlotViewBase source)
        {
            if (_contextMenu != null && _contextMenu.IsVisible) return;
            _dragSource = source;
            _dragSourceContainer = null;
            source.SetHighlight(true);
            ShowDragGhost(source);
        }

        void OnPlayerSlotDropReceived(SlotViewBase target)
        {
            if (_dragSource == null) return;
            if (_dragSource == target) { CancelDrag(); return; }

            var session = App.App.Instance?.RaidSession;
            if (session == null) { CancelDrag(); return; }
            var state = session.RaidState;
            var playerInv = state.Inventory;

            if (_dragSourceContainer == null)
            {
                InventorySystem.TryMove(playerInv, _dragSource.SlotRef, target.SlotRef);
            }
            else if (_dragSourceContainer.IsFloorContainer)
            {
                TryPickUpFloorItem(state, session, _dragSourceContainer,
                    _dragSource.SlotRef.Index, target.SlotRef);
            }
            else
            {
                LootSystem.TryTransfer(
                    _dragSourceContainer.BoundInventory, _dragSource.SlotRef,
                    playerInv, target.SlotRef);
            }

            SyncArmorAfterTransfer();
            CancelDrag();
            Refresh();
        }

        // ------------------------------------------------------------------
        // Drag & drop — loot containers
        // ------------------------------------------------------------------

        void OnLootSlotDragStarted(LootContainerView container, SlotViewBase source)
        {
            if (_contextMenu != null && _contextMenu.IsVisible) return;
            _dragSource = source;
            _dragSourceContainer = container;
            source.SetHighlight(true);
            ShowDragGhost(source);
        }

        void OnLootSlotDropReceived(LootContainerView targetContainer, SlotViewBase target)
        {
            if (_dragSource == null) return;
            if (_dragSource == target) { CancelDrag(); return; }

            var session = App.App.Instance?.RaidSession;
            if (session == null) { CancelDrag(); return; }
            var state = session.RaidState;
            var playerInv = state.Inventory;

            if (_dragSourceContainer == null)
            {
                if (targetContainer.IsFloorContainer)
                    DropToFloor(state, session, _dragSource.SlotRef);
                else
                    LootSystem.TryTransfer(playerInv, _dragSource.SlotRef,
                        targetContainer.BoundInventory, target.SlotRef);
            }
            else
            {
                var srcInv = _dragSourceContainer.BoundInventory;
                var dstInv = targetContainer.BoundInventory;
                if (srcInv == dstInv)
                    InventorySystem.TryMove(srcInv, _dragSource.SlotRef, target.SlotRef);
                else
                    LootSystem.TryTransfer(srcInv, _dragSource.SlotRef, dstInv, target.SlotRef);
            }

            SyncArmorAfterTransfer();
            CancelDrag();
            Refresh();
        }

        // ------------------------------------------------------------------
        // Called by OnEndDrag on the source slot — fires AFTER OnDrop on the target slot.
        // If _dragSource is already null here, a valid drop was handled; do nothing.
        // ------------------------------------------------------------------

        void OnSlotDragEnded(SlotViewBase source)
        {
            if (_dragSource == null) return; // OnDrop already handled and called CancelDrag

            var mousePos = Mouse.current != null
                ? (Vector2)Mouse.current.position.ReadValue()
                : Vector2.zero;

            bool insidePopup = IsInsidePanel((RectTransform)_playerPanel.transform, mousePos)
                || IsInsidePanel((RectTransform)_lootContainerParent, mousePos);

            if (insidePopup)
            {
                // Released inside popup but on no slot — just cancel, keep item
                CancelDrag();
                return;
            }

            // Released outside popup — drop item to floor
            var session = App.App.Instance?.RaidSession;
            var state = session?.RaidState;
            if (state == null) { CancelDrag(); return; }

            if (_dragSourceContainer != null && _dragSourceContainer.IsFloorContainer)
            {
                CancelDrag();
                return;
            }

            InventoryState fromInv = _dragSourceContainer != null
                ? _dragSourceContainer.BoundInventory
                : state.Inventory;

            var dropPos = state.PlayerEntity != null
                ? state.PlayerEntity.Position + state.PlayerEntity.FacingDirection * 1.5f
                : Vector3.zero;

            var item = fromInv.GetSlot(_dragSource.SlotRef);
            if (item != null)
            {
                fromInv.SetSlot(_dragSource.SlotRef, null);
                var ground = GroundItemState.Create(item.Id, item.DefinitionId, dropPos, item.StackCount);
                state.GroundItems.Add(ground);
                session.ConsumeEvents().GroundItemSpawned(ground.Id, ground.Position, ground.DefinitionId);
            }

            SyncArmorAfterTransfer();
            CancelDrag();
            RefreshFloorContainer(state);
            Refresh();
        }

        void CancelDrag()
        {
            if (_dragSource != null)
                _dragSource.SetHighlight(false);
            _dragSource = null;
            _dragSourceContainer = null;
            HideDragGhost();
        }

        // ------------------------------------------------------------------
        // Drag ghost
        // ------------------------------------------------------------------

        void ShowDragGhost(SlotViewBase source)
        {
            if (_dragGhost == null || _dragGhostGroup == null || _rootCanvas == null) return;

            _dragGhost.Bind(source.SlotRef, source.CurrentItem, source.IsLoot, -1);

            var ghostRect = (RectTransform)_dragGhost.transform;
            var canvasRect = (RectTransform)_rootCanvas.transform;

            var mousePos = Mouse.current != null
                ? (Vector2)Mouse.current.position.ReadValue()
                : Vector2.zero;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect, mousePos, _rootCanvas.worldCamera, out var mouseCanvasPos);

            if (source is EquipmentSlotView)
            {
                // Equipment slots are large — center ghost on cursor instead
                _dragOffset = -ghostRect.rect.size * 0.5f;
            }
            else
            {
                // Preserve pickup position: ghost appears exactly where the slot was
                var sourceRect = (RectTransform)source.transform;
                var slotScreenPos = RectTransformUtility.WorldToScreenPoint(
                    _rootCanvas.worldCamera, sourceRect.position);
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    canvasRect, slotScreenPos, _rootCanvas.worldCamera, out var slotCanvasPos);
                _dragOffset = slotCanvasPos - mouseCanvasPos;
            }

            ghostRect.localPosition = mouseCanvasPos + _dragOffset;

            _dragGhostGroup.alpha = 1f;
            _dragGhostGroup.blocksRaycasts = false;
            _dragGhostGroup.interactable = false;
        }

        void HideDragGhost()
        {
            if (_dragGhostGroup == null) return;
            _dragGhostGroup.alpha = 0f;
            _dragGhostGroup.blocksRaycasts = false;
        }

        void MoveDragGhost()
        {
            if (_dragGhost == null || _rootCanvas == null) return;

            var mousePos = Mouse.current != null
                ? (Vector2)Mouse.current.position.ReadValue()
                : Vector2.zero;

            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                (RectTransform)_rootCanvas.transform,
                mousePos,
                _rootCanvas.worldCamera,
                out var mouseCanvasPos);

            ((RectTransform)_dragGhost.transform).localPosition = mouseCanvasPos + _dragOffset;
        }

        // ------------------------------------------------------------------
        // Context menu — player panel
        // ------------------------------------------------------------------

        void OnPlayerSlotRightClicked(SlotViewBase slot, PointerEventData eventData)
        {
            CancelDrag();
            var session = App.App.Instance?.RaidSession;
            var state = session?.RaidState;
            if (state == null) return;

            _contextMenu.Show(eventData.position, new[] { "Drop" }, _ =>
            {
                DropToFloor(state, session, slot.SlotRef);
                SyncArmorAfterTransfer();
                Refresh();
            });
        }

        // ------------------------------------------------------------------
        // Context menu — loot containers
        // ------------------------------------------------------------------

        void OnLootSlotRightClicked(LootContainerView container, SlotViewBase slot,
            PointerEventData eventData)
        {
            CancelDrag();
            var session = App.App.Instance?.RaidSession;
            var state = session?.RaidState;
            if (state == null) return;

            if (container.IsFloorContainer)
            {
                _contextMenu.Show(eventData.position, new[] { "Pick up" }, _ =>
                {
                    int free = state.Inventory.FindFreeBackpackSlot();
                    if (free >= 0)
                        TryPickUpFloorItem(state, session, container,
                            slot.SlotRef.Index, InventorySlotRef.BackpackSlot(free));
                    Refresh();
                });
            }
            else
            {
                _contextMenu.Show(eventData.position, new[] { "Pick up", "Drop" }, idx =>
                {
                    if (idx == 0)
                    {
                        int free = state.Inventory.FindFreeBackpackSlot();
                        if (free >= 0)
                            LootSystem.TryTransfer(container.BoundInventory, slot.SlotRef,
                                state.Inventory, InventorySlotRef.BackpackSlot(free));
                    }
                    else
                    {
                        var dropPos = state.PlayerEntity != null
                            ? state.PlayerEntity.Position + state.PlayerEntity.FacingDirection * 1.5f
                            : Vector3.zero;
                        var item = container.BoundInventory.GetSlot(slot.SlotRef);
                        if (item != null)
                        {
                            container.BoundInventory.SetSlot(slot.SlotRef, null);
                            var ground = GroundItemState.Create(item.Id, item.DefinitionId,
                                dropPos, item.StackCount);
                            state.GroundItems.Add(ground);
                            session.ConsumeEvents().GroundItemSpawned(
                                ground.Id, ground.Position, ground.DefinitionId);
                        }
                    }
                    SyncArmorAfterTransfer();
                    RefreshFloorContainer(state);
                    Refresh();
                });
            }
        }

        // ------------------------------------------------------------------
        // Floor helpers
        // ------------------------------------------------------------------

        void RebuildFloorInventory(RaidState state, Vector3 playerPos)
        {
            if (_floorInventory == null)
            {
                _floorInventory = new InventoryState();
                _floorItemEIds = new EId[InventoryState.BackpackSize];
            }

            for (int i = 0; i < InventoryState.BackpackSize; i++)
            {
                _floorInventory.Backpack[i] = null;
                _floorItemEIds[i] = EId.None;
            }

            int slot = 0;
            for (int i = 0; i < state.GroundItems.Count && slot < InventoryState.BackpackSize; i++)
            {
                if (Vector3.Distance(playerPos, state.GroundItems[i].Position) > LootSystem.LootRange)
                    continue;
                var gi = state.GroundItems[i];
                _floorInventory.Backpack[slot] = ItemState.Create(gi.Id, gi.DefinitionId, gi.StackCount);
                _floorItemEIds[slot] = gi.Id;
                slot++;
            }
        }

        void TryPickUpFloorItem(RaidState state, Session.RaidSession session,
            LootContainerView floorContainer, int floorSlotIndex, InventorySlotRef targetSlot)
        {
            if (floorSlotIndex < 0 || floorSlotIndex >= InventoryState.BackpackSize) return;
            var eids = floorContainer.FloorItemEIds;
            if (eids == null) return;
            var floorItemEId = eids[floorSlotIndex];
            if (!floorItemEId.IsValid) return;

            for (int i = 0; i < state.GroundItems.Count; i++)
            {
                if (state.GroundItems[i].Id != floorItemEId) continue;

                var gi = state.GroundItems[i];
                var def = ItemDefinition.Get(gi.DefinitionId);
                var slotType = targetSlot.ToItemSlotType();
                if (def != null && (def.AllowedSlots & slotType) == 0) return;
                if (state.Inventory.GetSlot(targetSlot) != null) return;

                state.Inventory.SetSlot(targetSlot,
                    ItemState.Create(gi.Id, gi.DefinitionId, gi.StackCount));
                state.GroundItems.RemoveAt(i);
                session.ConsumeEvents().GroundItemDespawned(gi.Id);

                var floorInv = floorContainer.BoundInventory;
                floorInv.Backpack[floorSlotIndex] = null;
                eids[floorSlotIndex] = EId.None;
                break;
            }
        }

        void DropToFloor(RaidState state, Session.RaidSession session, InventorySlotRef sourceSlot)
        {
            var item = state.Inventory.GetSlot(sourceSlot);
            if (item == null) return;

            state.Inventory.SetSlot(sourceSlot, null);

            var dropPos = state.PlayerEntity != null
                ? state.PlayerEntity.Position + state.PlayerEntity.FacingDirection * 1.5f
                : Vector3.zero;

            var ground = GroundItemState.Create(item.Id, item.DefinitionId, dropPos, item.StackCount);
            state.GroundItems.Add(ground);
            session.ConsumeEvents().GroundItemSpawned(ground.Id, ground.Position, ground.DefinitionId);

            RefreshFloorContainer(state);
        }

        void RefreshFloorContainer(RaidState state)
        {
            RebuildFloorInventory(state, state.PlayerEntity.Position);

            LootContainerView floorContainer = null;
            foreach (var c in _activeContainers)
            {
                if (c.IsFloorContainer) { floorContainer = c; break; }
            }

            if (floorContainer == null)
            {
                floorContainer = GetContainer();
                _activeContainers.Add(floorContainer);
            }

            floorContainer.BindFloor(_floorInventory, _floorItemEIds);
        }

        // ------------------------------------------------------------------
        // Armor sync
        // ------------------------------------------------------------------

        bool IsInsidePanel(RectTransform panel, Vector2 screenPos)
        {
            if (panel == null) return false;
            return RectTransformUtility.RectangleContainsScreenPoint(
                panel, screenPos, _rootCanvas?.worldCamera);
        }

        void SyncArmorAfterTransfer()
        {
            var session = App.App.Instance?.RaidSession;
            if (session == null) return;

            var state = session.RaidState;
            if (state.PlayerEntity == null) return;

            EquipmentSystem.SyncArmorFromInventory(state, state.PlayerEntity.Id, state.Inventory);

            var playerView = FindObjectOfType<PlayerView>();
            if (playerView == null) return;

            var helmetDef = state.Inventory.HelmetSlot?.Definition;
            if (helmetDef != null && !string.IsNullOrEmpty(helmetDef.ArmorPrefabId))
                playerView.SwapHelmetModel(helmetDef.ArmorPrefabId);
            else
                playerView.ClearHelmetModel();

            var armorDef = state.Inventory.BodyArmorSlot?.Definition;
            if (armorDef != null && !string.IsNullOrEmpty(armorDef.ArmorPrefabId))
                playerView.SwapArmorModel(armorDef.ArmorPrefabId);
            else
                playerView.ClearArmorModel();
        }
    }
}
