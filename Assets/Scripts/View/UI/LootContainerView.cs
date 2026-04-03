using System;
using System.Collections.Generic;
using State;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace View.UI
{
    public class LootContainerView : MonoBehaviour
    {
        [Header("Header")]
        [SerializeField] TMP_Text _headerText;
        [SerializeField] TMP_Text _itemCountText;
        [SerializeField] Button _sortButton;

        [Header("Items")]
        [SerializeField] Transform _itemGrid;
        [SerializeField] InventorySlotView _slotPrefab;

        [Header("Openable container")]
        [SerializeField] GameObject _gridRoot;
        [SerializeField] Button _openButton;

        readonly List<InventorySlotView> _slots = new();
        InventoryState _inventory;
        int _usedSlotCount;

        public LootableContainerState Lootable { get; private set; }
        public bool IsFloorContainer { get; private set; }
        public InventoryState BoundInventory => _inventory;

        public EId[] FloorItemEIds { get; private set; }

        public event Action<LootContainerView, SlotViewBase> SlotDragStarted;
        public event Action<LootContainerView, SlotViewBase> SlotDragEnded;
        public event Action<LootContainerView, SlotViewBase> SlotDropReceived;
        public event Action<LootContainerView, SlotViewBase, PointerEventData> SlotRightClicked;

        public void Init()
        {
            if (_openButton != null)
                _openButton.onClick.AddListener(OnOpenClicked);
        }

        public void BindLootable(LootableContainerState lootable)
        {
            Lootable = lootable;
            IsFloorContainer = false;
            _inventory = lootable.Inventory;

            if (_headerText != null)
                _headerText.text = lootable.TypeId;

            SetOpened(!lootable.IsContainer);
            EnsureSlots();
            Refresh();
        }

        public void BindFloor(InventoryState floorInventory, EId[] floorEIds)
        {
            Lootable = null;
            IsFloorContainer = true;
            _inventory = floorInventory;
            FloorItemEIds = floorEIds;

            if (_headerText != null)
                _headerText.text = "On the Floor";

            SetOpened(true);
            EnsureSlots();
            Refresh();
        }

        public void Refresh()
        {
            if (_inventory == null) return;

            int itemCount = 0;
            for (int i = 0; i < InventoryState.BackpackSize; i++)
            {
                var item = _inventory.Backpack[i];
                if (i < _slots.Count)
                {
                    _slots[i].Bind(InventorySlotRef.BackpackSlot(i), item, true, -1);
                    _slots[i].gameObject.SetActive(item != null);
                }
                if (item != null) itemCount++;
            }

            if (_itemCountText != null)
                _itemCountText.text = $"({itemCount}/{InventoryState.BackpackSize})";
        }

        void SetOpened(bool opened)
        {
            if (_gridRoot != null)
                _gridRoot.SetActive(opened);
            if (_openButton != null)
                _openButton.gameObject.SetActive(!opened);
        }

        void OnOpenClicked()
        {
            SetOpened(true);
            Refresh();
        }

        void EnsureSlots()
        {
            if (_slots.Count >= InventoryState.BackpackSize) return;
            if (_slotPrefab == null || _itemGrid == null) return;

            for (int i = _slots.Count; i < InventoryState.BackpackSize; i++)
            {
                var slot = Instantiate(_slotPrefab, _itemGrid);
                slot.gameObject.SetActive(false);
                WireSlot(slot);
                _slots.Add(slot);
            }
        }

        void WireSlot(InventorySlotView slot)
        {
            slot.DragStarted += s => SlotDragStarted?.Invoke(this, s);
            slot.DragEnded += s => SlotDragEnded?.Invoke(this, s);
            slot.DroppedOnSlot += s => SlotDropReceived?.Invoke(this, s);
            slot.RightClicked += (s, e) => SlotRightClicked?.Invoke(this, s, e);
        }
    }
}
