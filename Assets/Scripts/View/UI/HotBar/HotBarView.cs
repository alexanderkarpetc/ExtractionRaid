using System.Collections.Generic;
using ApplicationCore;
using State;
using UnityEngine;

namespace View.UI.HotBar
{
    public class HotBarView : MonoBehaviour
    {
        [SerializeField] List<HotBarItemView> _itemViews;

        void LateUpdate()
        {
            var player = App.Instance?.RaidSession?.RaidState?.PlayerEntity;
            var inventory = App.Instance?.Player?.Inventory;

            foreach (var view in _itemViews)
            {
                if (view == null) continue;

                int keyNum = view.QuickSlotKey;
                int qi = keyNum - InventoryState.QuickSlotKeyOffset;
                if (qi < 0 || inventory == null || qi >= inventory.QuickSlotBindings.Length) continue;

                int boundSlot = inventory.QuickSlotBindings[qi];
                ItemState item = boundSlot >= 0 ? inventory.Backpack[boundSlot] : null;
                var slotRef = boundSlot >= 0 ? InventorySlotRef.BackpackSlot(boundSlot) : default;
                bool isActive = player != null && player.ActiveQuickSlot == qi;

                view.RefreshQuickSlot(keyNum, slotRef, item, isActive);
            }
        }
    }
}
