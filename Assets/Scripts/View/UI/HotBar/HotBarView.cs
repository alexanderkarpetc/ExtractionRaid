using System.Collections.Generic;
using ApplicationCore;
using State;
using UnityEngine;

namespace View.UI.HotBar
{
    /// <summary>
    /// DEPRECATED 2026-05-07. Replaced by <c>View.UI.Hotbar.HotbarOverlay</c>
    /// (UI Toolkit). Component is still referenced by the legacy "HotBar"
    /// GameObject in <c>UI.prefab</c> (m_IsActive: 0) — file is kept so the
    /// prefab doesn't develop a missing-script warning. Safe to delete once
    /// the legacy GameObject is removed from <c>UI.prefab</c>.
    /// </summary>
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
