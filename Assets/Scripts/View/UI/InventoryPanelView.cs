using System;
using System.Collections.Generic;
using State;
using UnityEngine;
using UnityEngine.EventSystems;

namespace View.UI
{
    public class InventoryPanelView : MonoBehaviour
    {
        [Header("Equipment slots (manually placed)")]
        [SerializeField] EquipmentSlotView _weapon1Slot;
        [SerializeField] EquipmentSlotView _weapon2Slot;
        [SerializeField] EquipmentSlotView _helmetSlot;
        [SerializeField] EquipmentSlotView _armorSlot;

        [Header("Backpack")]
        [SerializeField] Transform _backpackGrid;
        [SerializeField] InventorySlotView _slotPrefab;

        readonly List<InventorySlotView> _backpackSlots = new();
        InventoryState _inventory;

        public InventoryState BoundInventory => _inventory;

        public event Action<SlotViewBase> SlotDragStarted;
        public event Action<SlotViewBase> SlotDropReceived;
        public event Action<SlotViewBase, PointerEventData> SlotRightClicked;

        public void Init()
        {
            EnsureBackpackSlots();
            WireSlot(_weapon1Slot);
            WireSlot(_weapon2Slot);
            WireSlot(_helmetSlot);
            WireSlot(_armorSlot);
        }

        public void Bind(InventoryState inventory)
        {
            _inventory = inventory;
        }

        public void Refresh()
        {
            if (_inventory == null) return;
            RefreshEquipSlots();
            RefreshBackpack();
        }

        void RefreshEquipSlots()
        {
            BindEquip(_weapon1Slot, InventorySlotRef.Weapon(0), _inventory.WeaponSlots[0]);
            BindEquip(_weapon2Slot, InventorySlotRef.Weapon(1), _inventory.WeaponSlots[1]);
            BindEquip(_helmetSlot, InventorySlotRef.Helmet(), _inventory.HelmetSlot);
            BindEquip(_armorSlot, InventorySlotRef.BodyArmor(), _inventory.BodyArmorSlot);
        }

        void BindEquip(EquipmentSlotView view, InventorySlotRef slotRef, ItemState item)
        {
            if (view != null)
                view.Bind(slotRef, item, false);
        }

        void RefreshBackpack()
        {
            for (int i = 0; i < InventoryState.BackpackSize; i++)
            {
                var item = _inventory.Backpack[i];
                int quickKey = GetQuickSlotKey(i);
                _backpackSlots[i].Bind(InventorySlotRef.BackpackSlot(i), item, false, quickKey);
            }
        }

        int GetQuickSlotKey(int backpackIndex)
        {
            if (_inventory == null) return -1;
            for (int qi = 0; qi < InventoryState.QuickSlotCount; qi++)
            {
                if (_inventory.QuickSlotBindings[qi] == backpackIndex)
                    return qi + InventoryState.QuickSlotKeyOffset;
            }
            return -1;
        }

        void EnsureBackpackSlots()
        {
            if (_backpackSlots.Count >= InventoryState.BackpackSize) return;
            if (_slotPrefab == null || _backpackGrid == null) return;

            for (int i = _backpackSlots.Count; i < InventoryState.BackpackSize; i++)
            {
                var slot = Instantiate(_slotPrefab, _backpackGrid);
                slot.gameObject.SetActive(true);
                WireSlot(slot);
                _backpackSlots.Add(slot);
            }
        }

        void WireSlot(SlotViewBase slot)
        {
            if (slot == null) return;
            slot.DragStarted += s => SlotDragStarted?.Invoke(s);
            slot.DroppedOnSlot += s => SlotDropReceived?.Invoke(s);
            slot.RightClicked += (s, e) => SlotRightClicked?.Invoke(s, e);
        }
    }
}
