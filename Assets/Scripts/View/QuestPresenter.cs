using ApplicationCore;
using State;
using UnityEngine;
using UnityEngine.InputSystem;
using View.UI;
using View.UI.Quests;

namespace View
{
    /// <summary>
    /// Handles the journal hotkey (Key.I) and keeps PlayerEntityState.IsQuestLogOpen
    /// in sync with the actual popup state. NPC-driven popup opening lives in
    /// <see cref="NpcDialoguePresenter"/>.
    /// </summary>
    public class QuestPresenter : MonoBehaviour
    {
        PopupManager _popupManager;
        QuestsPopupView _questsPopupView;
        bool _triedFind;

        void Update()
        {
            var session = App.Instance?.RaidSession;
            var player = session?.RaidState?.PlayerEntity;

            if (player == null) return;

            bool popupOpen = HasPopup() && _popupManager.IsOpen(_questsPopupView);

            var kb = Keyboard.current;
            if (kb != null && kb[Key.I].wasPressedThisFrame)
            {
                if (popupOpen)
                    _questsPopupView.RequestClose();
                else if (!player.IsInMenu && HasPopup())
                {
                    _popupManager.Open(_questsPopupView);
                    _questsPopupView.OpenJournal();
                }
            }

            popupOpen = HasPopup() && _popupManager.IsOpen(_questsPopupView);
            player.IsQuestLogOpen = popupOpen;

            if (popupOpen)
                App.Instance.SetGameplayInputBlocked(true);
        }

        bool HasPopup()
        {
            if (!_triedFind)
            {
                _triedFind = true;
                _popupManager = FindObjectOfType<PopupManager>(includeInactive: true);
                _questsPopupView = FindObjectOfType<QuestsPopupView>(includeInactive: true);

                if (_questsPopupView != null)
                    _questsPopupView.Closed += OnPopupClosed;
            }
            return _popupManager != null && _questsPopupView != null;
        }

        void OnPopupClosed()
        {
            _popupManager?.Close();
            var player = App.Instance?.RaidSession?.RaidState?.PlayerEntity;
            if (player != null)
            {
                player.IsQuestLogOpen = false;
                // Don't unblock input here — NpcDialoguePresenter may still want it
                // blocked (dialogue still up after returning from the quest popup).
                // Once both UIs are down, presenters re-enable gameplay input.
                if (player.NpcTargetId == EId.None)
                    App.Instance.SetGameplayInputBlocked(false);
            }
        }

        void OnDestroy()
        {
            if (_questsPopupView != null)
                _questsPopupView.Closed -= OnPopupClosed;
        }
    }
}
