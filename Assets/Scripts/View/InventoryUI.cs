using ApplicationCore;
using State;
using UnityEngine;
using UnityEngine.InputSystem;
using View.UI;

namespace View
{
    public class InventoryUI : MonoBehaviour
    {
        bool _isOpen;
        bool _openedByLoot;

        PopupManager _popupManager;
        LootPopupView _lootPopupView;
        bool _triedFindPopup;

        void Update()
        {
            var session = App.Instance?.RaidSession;
            var player = session?.RaidState?.PlayerEntity;

            var kb = Keyboard.current;
            if (kb != null && kb[Key.Tab].wasPressedThisFrame)
            {
                if (_isOpen)
                {
                    _isOpen = false;
                    _openedByLoot = false;
                    if (player != null)
                        player.LootTargetId = EId.None;
                }
                else
                {
                    _isOpen = true;
                    if (player != null)
                        player.CraftTargetId = EId.None;
                }
            }

            if (player == null) return;

            if (App.Instance.IsInHideout)
            {
                player.IsInventoryOpen = _isOpen;
                return;
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

            player.IsInventoryOpen = _isOpen;

            if (HasLootPopup())
                SyncLootPopup(session.RaidState, player);
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
                _lootPopupView.Open(state);
            }
            else if (!_isOpen && popupOpen)
            {
                _popupManager.Close();
            }
        }
    }
}
