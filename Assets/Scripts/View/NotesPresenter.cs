using ApplicationCore;
using UnityEngine;
using UnityEngine.InputSystem;
using View.UI.Notes;

namespace View
{
    /// <summary>
    /// Handles the field-notes hotkey (Key.N) and keeps
    /// PlayerEntityState.IsNotesOpen in sync with the actual popup state.
    /// Mirrors <see cref="QuestPresenter"/>. Drives the UI Toolkit
    /// <see cref="NotesWindow"/>.
    /// </summary>
    public class NotesPresenter : MonoBehaviour
    {
        NotesWindow _window;
        bool _subscribed;

        void Update()
        {
            var session = App.Instance?.RaidSession;
            var player = session?.RaidState?.PlayerEntity;

            if (player == null) return;
            if (!EnsureWindow()) return;

            bool popupOpen = _window.IsOpen;

            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb[Key.N].wasPressedThisFrame)
                {
                    if (popupOpen)
                        _window.RequestClose();
                    else if (!player.IsInMenu)
                        _window.Open();
                }
                else if (popupOpen && kb[Key.Escape].wasPressedThisFrame)
                {
                    _window.RequestClose();
                }
            }

            popupOpen = _window.IsOpen;
            player.IsNotesOpen = popupOpen;

            if (popupOpen)
                App.Instance.SetGameplayInputBlocked(true);
        }

        bool EnsureWindow()
        {
            if (!_subscribed)
            {
                _window = NotesWindow.Instance;
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
                player.IsNotesOpen = false;
                // Don't unblock input if another menu surface is still up —
                // once all are down, presenters re-enable gameplay input.
                if (!player.IsInMenu)
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
