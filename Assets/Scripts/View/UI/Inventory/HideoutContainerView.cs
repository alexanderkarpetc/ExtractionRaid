using System;
using System.Collections.Generic;
using State;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace View.UI.Inventory
{
    public class HideoutContainerView : MonoBehaviour
    {
        [SerializeField] Button _sortButton;

        [Header("Items")]
        [SerializeField] Transform _itemGrid;
        [SerializeField] InventorySlotView _slotPrefab;
        [SerializeField] int _slotsPerPage = 24;

        [Header("Navigation")]
        [SerializeField] InventoryNavigationButtonView _navigationButtonPrefab;
        [SerializeField] Transform _navigationGrid;

        readonly List<InventorySlotView> _slots = new();
        readonly List<InventoryNavigationButtonView> _navButtons = new();
        List<ItemState> _stash;
        int _currentPage;

        public List<ItemState> Stash => _stash;
        public int CurrentPage => _currentPage;
        public int SlotsPerPage => _slotsPerPage;

        public event Action<HideoutContainerView, SlotViewBase> SlotDragStarted;
        public event Action<HideoutContainerView, SlotViewBase> SlotDragEnded;
        public event Action<HideoutContainerView, SlotViewBase> SlotDropReceived;
        public event Action<HideoutContainerView, SlotViewBase, PointerEventData> SlotRightClicked;

        public void Init()
        {
            if (_sortButton != null)
                _sortButton.onClick.AddListener(OnSortClicked);
        }

        public void Bind(List<ItemState> stash)
        {
            _stash = stash;
            _currentPage = 0;
            EnsureSlots();
            Refresh();
        }

        public void Refresh()
        {
            if (_stash == null) return;

            int totalPages = Mathf.Max(1,
                Mathf.CeilToInt(_stash.Count / (float)_slotsPerPage));
            if (_currentPage >= totalPages) _currentPage = totalPages - 1;
            if (_currentPage < 0) _currentPage = 0;

            RefreshNavigation(totalPages);
            RefreshSlots();
        }

        void RefreshSlots()
        {
            int pageOffset = _currentPage * _slotsPerPage;
            for (int i = 0; i < _slotsPerPage; i++)
            {
                int globalIndex = pageOffset + i;
                bool inRange = globalIndex < _stash.Count;
                var item = inRange ? _stash[globalIndex] : null;
                _slots[i].Bind(InventorySlotRef.BackpackSlot(globalIndex), item, true, -1);
                _slots[i].gameObject.SetActive(true);
            }
        }

        void RefreshNavigation(int totalPages)
        {
            _navigationGrid.gameObject.SetActive(totalPages > 1);

            for (int i = _navButtons.Count; i < totalPages; i++)
            {
                var btn = Instantiate(_navigationButtonPrefab, _navigationGrid);
                int captured = i;
                btn.Bind(captured + 1, captured == _currentPage, () => OnNavigationClicked(captured));
                _navButtons.Add(btn);
            }

            for (int i = 0; i < _navButtons.Count; i++)
            {
                bool visible = i < totalPages;
                _navButtons[i].gameObject.SetActive(visible);
                if (visible)
                {
                    _navButtons[i].SetActiveState(i == _currentPage);
                    _navButtons[i].SetNumber(i + 1);
                }
            }
        }

        void OnNavigationClicked(int page)
        {
            if (_currentPage == page) return;
            _currentPage = page;
            Refresh();
        }

        void OnSortClicked()
        {
            if (_stash == null) return;
            _stash.Sort((a, b) =>
            {
                if (a == null && b == null) return 0;
                if (a == null) return 1;
                if (b == null) return -1;
                int cmp = string.CompareOrdinal(a.DefinitionId, b.DefinitionId);
                if (cmp != 0) return cmp;
                return b.StackCount.CompareTo(a.StackCount);
            });
            Refresh();
        }

        void EnsureSlots()
        {
            if (_slots.Count >= _slotsPerPage) return;
            if (_slotPrefab == null || _itemGrid == null) return;

            for (int i = _slots.Count; i < _slotsPerPage; i++)
            {
                var slot = Instantiate(_slotPrefab, _itemGrid);
                slot.gameObject.SetActive(true);
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
