using ApplicationCore;
using State;
using UnityEngine;
using View.UI;
using View.UI.Craft;

namespace View
{
    public class CraftPresenter : MonoBehaviour
    {
        PopupManager _popupManager;
        CraftPopupView _craftPopupView;
        bool _triedFind;

        EId _lastCraftTargetId = EId.None;

        void Update()
        {
            var session = App.Instance?.RaidSession;
            var player = session?.RaidState?.PlayerEntity;
            if (player == null) return;

            if (!HasPopup()) return;

            bool popupOpen = _popupManager.IsOpen(_craftPopupView);
            bool shouldBeOpen = player.CraftTargetId != EId.None;

            if (shouldBeOpen && !popupOpen)
            {
                _popupManager.Open(_craftPopupView);
                _craftPopupView.Open();
                App.Instance.SetGameplayInputBlocked(true);
            }
            else if (!shouldBeOpen && popupOpen)
            {
                _popupManager.Close();
                App.Instance.SetGameplayInputBlocked(false);
            }

            _lastCraftTargetId = player.CraftTargetId;
        }

        bool HasPopup()
        {
            if (!_triedFind)
            {
                _triedFind = true;
                _popupManager = FindObjectOfType<PopupManager>(includeInactive: true);
                _craftPopupView = FindObjectOfType<CraftPopupView>(includeInactive: true);

                if (_craftPopupView != null)
                    _craftPopupView.Closed += OnPopupClosed;
            }
            return _popupManager != null && _craftPopupView != null;
        }

        void OnPopupClosed()
        {
            _popupManager?.Close();
            var player = App.Instance?.RaidSession?.RaidState?.PlayerEntity;
            if (player != null)
            {
                player.CraftTargetId = EId.None;
                _lastCraftTargetId = EId.None;
                App.Instance.SetGameplayInputBlocked(false);
            }
        }

        void OnDestroy()
        {
            if (_craftPopupView != null)
                _craftPopupView.Closed -= OnPopupClosed;
        }
    }
}
