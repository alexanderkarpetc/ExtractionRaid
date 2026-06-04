using ApplicationCore;
using State;
using UnityEngine;
using UnityEngine.InputSystem;
using View.UI.Quests;

namespace View
{
    /// <summary>
    /// Handles the journal hotkey (Key.I) and keeps PlayerEntityState.IsQuestLogOpen
    /// in sync with the actual popup state. NPC-driven popup opening lives in
    /// <see cref="NpcDialoguePresenter"/>. Drives the UI Toolkit
    /// <see cref="QuestsWindow"/>.
    /// </summary>
    public class QuestPresenter : MonoBehaviour
    {
        QuestsWindow _window;
        bool _subscribed;

        void Update()
        {
            var session = App.Instance?.RaidSession;
            var player = session?.RaidState?.PlayerEntity;

            if (player == null) return;
            if (!EnsureWindow()) return;

            bool popupOpen = _window.IsOpen;

            var kb = Keyboard.current;
            if (kb != null && kb[Key.I].wasPressedThisFrame)
            {
                if (popupOpen)
                    _window.RequestClose();
                else if (!player.IsInMenu)
                    _window.OpenJournal();
            }

            popupOpen = _window.IsOpen;
            player.IsQuestLogOpen = popupOpen;

            if (popupOpen)
                App.Instance.SetGameplayInputBlocked(true);
        }

        bool EnsureWindow()
        {
            if (!_subscribed)
            {
                _window = QuestsWindow.Instance;
                if (_window != null)
                {
                    _window.Closed += OnPopupClosed;
                    _subscribed = true;
                }
            }
            return _window != null;
        }

        void OnPopupClosed()
        {
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
            if (_window != null)
                _window.Closed -= OnPopupClosed;
        }
    }
}
