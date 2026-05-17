using ApplicationCore;
using Dev;
using State;
using UnityEngine;
using UnityEngine.InputSystem;
using View.UI;
using View.UI.Inventory;
using View.UI.WeaponBuilder;

namespace View
{
    public class InventoryUI : MonoBehaviour
    {
        bool _isOpen;
        bool _openedByLoot;
        bool _openedByBuilder;

        PopupManager _popupManager;
        LootPopupView _lootPopupView;
        bool _triedFindPopup;

        void Update()
        {
            var session = App.Instance?.RaidSession;
            var player = session?.RaidState?.PlayerEntity;
            if (player == null) return;

            bool builderOpen = player.BuilderTargetId != EId.None;

            var kb = Keyboard.current;
            if (kb != null && kb[Key.Tab].wasPressedThisFrame)
            {
                if (builderOpen)
                {
                    // Tab is the universal "close everything" key. While Builder is
                    // open it tears down the modal — Builder.Close clears
                    // BuilderTargetId, and the next Update sees `!builderOpen` and
                    // closes the inventory popup naturally.
                    WeaponBuilderWindow.Instance?.Close();
                }
                else if (_isOpen)
                {
                    _isOpen = false;
                    _openedByLoot = false;
                    player.LootTargetId = EId.None;
                }
                else
                {
                    _isOpen = true;
                    player.CraftTargetId = EId.None;
                }
            }

            if (player.CraftTargetId != EId.None && _isOpen)
            {
                _isOpen = false;
                _openedByLoot = false;
            }

            if (player.LootTargetId != EId.None && !_isOpen)
            {
                _isOpen = true;
                _openedByLoot = true;
            }

            if (player.LootTargetId == EId.None && _openedByLoot)
            {
                _isOpen = false;
                _openedByLoot = false;
            }

            // Builder side-by-side: BuilderTargetId drives the inventory popup
            // open/close in lockstep with the Builder modal.
            if (builderOpen && !_isOpen)
            {
                _isOpen = true;
                _openedByBuilder = true;
            }
            else if (!builderOpen && _openedByBuilder)
            {
                _isOpen = false;
                _openedByBuilder = false;
            }

            player.IsInventoryOpen = _isOpen;
            App.Instance.SetGameplayInputBlocked(_isOpen);

            // Migration switch — when on, the new UI Toolkit InventoryWindow drives
            // visibility and the legacy uGUI popup стай closed. When off, the legacy
            // path remains canonical. Stage 0 = skeleton-only on the UTK side.
            if (DevCheats.UseUiToolkitInventory)
                SyncUiToolkitWindow();
            else if (HasLootPopup())
                SyncLootPopup(session.RaidState, player);
        }

        void SyncUiToolkitWindow()
        {
            var window = InventoryWindow.Instance;
            if (window == null) return;

            // Make sure the legacy popup is shut while UTK path owns the screen,
            // в т.ч. на момент перемикання тогла наживо.
            if (HasLootPopup() && _popupManager.IsOpen(_lootPopupView))
                _popupManager.Close();

            if (_isOpen && !window.IsOpen)       window.Open();
            else if (!_isOpen && window.IsOpen)  window.Close();
        }

        bool HasLootPopup()
        {
            if (!_triedFindPopup)
            {
                _triedFindPopup = true;
                _popupManager = FindObjectOfType<PopupManager>(includeInactive: true);
                _lootPopupView = FindObjectOfType<LootPopupView>(includeInactive: true);
            }
            return _popupManager != null && _lootPopupView != null;
        }

        void SyncLootPopup(RaidState state, PlayerEntityState player)
        {
            bool popupOpen = _popupManager.IsOpen(_lootPopupView);

            if (_isOpen && !popupOpen)
            {
                _popupManager.Open(_lootPopupView);
                if (_openedByBuilder)
                    _lootPopupView.OpenForBuilder();
                else if (App.Instance.IsInHideout)
                    _lootPopupView.OpenForHideout();
                else
                    _lootPopupView.Open(state);
            }
            else if (!_isOpen && popupOpen)
            {
                _popupManager.Close();
            }
        }
    }
}
