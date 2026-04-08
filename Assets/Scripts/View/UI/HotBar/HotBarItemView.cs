using ApplicationCore;
using State;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace View.UI.HotBar
{
    public class HotBarItemView : MonoBehaviour, IDropHandler
    {
        [SerializeField] InventorySlotView _slotView;
        [SerializeField] GameObject _emptyRoot;
        [SerializeField] int _quickSlotKey = 3;

        public int QuickSlotKey => _quickSlotKey;

        void Awake()
        {
            // _slotView is display-only in the hotbar; disabling raycast targets on its
            // graphics ensures drops always reach HotBarItemView.OnDrop, not SlotViewBase.OnDrop.
            if (_slotView != null)
                foreach (var g in _slotView.GetComponentsInChildren<Graphic>(true))
                    g.raycastTarget = false;
        }

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

            // Clear any existing binding that already points to this backpack slot
            for (int i = 0; i < inventory.QuickSlotBindings.Length; i++)
                if (inventory.QuickSlotBindings[i] == source.SlotRef.Index)
                    inventory.QuickSlotBindings[i] = -1;

            inventory.QuickSlotBindings[qi] = source.SlotRef.Index;

            var lootPopup = FindObjectOfType<LootPopupView>(includeInactive: true);
            lootPopup?.OnExternalDropHandled();
        }
    }
}
