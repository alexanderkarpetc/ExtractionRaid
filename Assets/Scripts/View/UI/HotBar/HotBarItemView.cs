using ApplicationCore;
using State;
using UnityEngine;
using UnityEngine.EventSystems;

namespace View.UI.HotBar
{
    public class HotBarItemView : MonoBehaviour, IDropHandler
    {
        [SerializeField] InventorySlotView _slotView;
        [SerializeField] GameObject _emptyRoot;
        [SerializeField] int _quickSlotKey = 3;

        public int QuickSlotKey => _quickSlotKey;

        public void RefreshQuickSlot(int keyNum, InventorySlotRef slotRef, ItemState item, bool isActive)
        {
            bool hasBind = item != null;

            if (_emptyRoot != null)
                _emptyRoot.SetActive(!hasBind);

            if (_slotView != null)
            {
                _slotView.gameObject.SetActive(hasBind);
                if (hasBind)
                {
                    _slotView.Bind(slotRef, item, false, keyNum);
                    _slotView.SetHighlight(isActive);
                }
            }
        }

        public void OnDrop(PointerEventData eventData)
        {
            var source = eventData.pointerDrag != null
                ? eventData.pointerDrag.GetComponent<SlotViewBase>()
                : null;

            if (source == null || source.IsLoot) return;
            if (source.SlotRef.Type != SlotType.Backpack) return;
            if (source.CurrentItem == null) return;

            var inventory = App.Instance?.Player?.Inventory;
            if (inventory == null) return;

            int qi = _quickSlotKey - InventoryState.QuickSlotKeyOffset;
            if (qi < 0 || qi >= inventory.QuickSlotBindings.Length) return;

            inventory.QuickSlotBindings[qi] = source.SlotRef.Index;

            var lootPopup = FindObjectOfType<LootPopupView>(includeInactive: true);
            lootPopup?.OnExternalDropHandled();
        }
    }
}
