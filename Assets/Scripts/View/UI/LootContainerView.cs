using Constants;
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
        [Header("Header")] [SerializeField] TMP_Text _headerText;
        [SerializeField] Button _sortButton;

        string _containerName;

        [Header("Items")] [SerializeField] Transform _itemGrid;
        [SerializeField] InventorySlotView _slotPrefab;

        [Header("Openable container")] [SerializeField]
        GameObject _gridRoot;

        [SerializeField] Button _openButton;

        public static int ContainerSlots => ContainerConstants.LootSlots;

        readonly List<InventorySlotView> _slots = new();
        InventoryState _inventory;

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
            _openButton.onClick.AddListener(OnOpenClicked);
        }

        public void BindLootable(LootableContainerState lootable)
        {
            Lootable = lootable;
            IsFloorContainer = false;
            _inventory = lootable.Inventory;

            _containerName = lootable.TypeId;
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

            _containerName = "On the Floor";
            SetOpened(true);
            EnsureSlots();
            Refresh();
        }

        public void Refresh()
        {
            if (_inventory == null) return;

            if (IsFloorContainer)
                RefreshFloor();
            else
                RefreshFixed();
        }

        void RefreshFixed()
        {
            int itemCount = 0;
            for (int i = 0; i < ContainerSlots; i++)
            {
                var item = i < InventoryState.BackpackSize ? _inventory.Backpack[i] : null;
                if (i < _slots.Count)
                {
                    _slots[i].Bind(InventorySlotRef.BackpackSlot(i), item, true, -1);
                    _slots[i].gameObject.SetActive(true);
                }

                if (item != null) itemCount++;
            }

            if (_headerText != null)
                _headerText.text = $"{_containerName} ({itemCount}/{ContainerSlots})";
        }

        void RefreshFloor()
        {
            int itemCount = 0;
            for (int i = 0; i < InventoryState.BackpackSize; i++)
            {
                if (_inventory.Backpack[i] != null) itemCount++;
            }

            EnsureSlots(itemCount);

            for (int i = 0; i < _slots.Count; i++)
            {
                bool hasItem = i < InventoryState.BackpackSize && _inventory.Backpack[i] != null;
                _slots[i].Bind(InventorySlotRef.BackpackSlot(i), hasItem ? _inventory.Backpack[i] : null, true, -1);
                _slots[i].gameObject.SetActive(hasItem);
            }

            if (_headerText != null)
                _headerText.text = $"{_containerName} ({itemCount})";
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

        void EnsureSlots(int count = -1)
        {
            int target = count < 0 ? ContainerSlots : count;
            if (_slots.Count >= target) return;
            if (_slotPrefab == null || _itemGrid == null) return;

            for (int i = _slots.Count; i < target; i++)
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