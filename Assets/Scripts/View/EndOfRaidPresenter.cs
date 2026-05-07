using ApplicationCore;
using Cysharp.Threading.Tasks;
using State;
using UnityEngine;
using View.UI.EndOfRaid;

namespace View
{
    /// <summary>
    /// Watches <see cref="App.LastRaidOutcome"/> and shows the end-of-raid screen
    /// when a raid finishes (KIA or extraction). The Next button hands control
    /// back to <see cref="App.ReturnToHideout"/> which performs the scene swap.
    /// </summary>
    public class EndOfRaidPresenter : MonoBehaviour
    {
        EndOfRaidWindow _window;
        bool _shown;
        bool _triedFind;

        void Update()
        {
            if (!App.IsInitialized) return;

            var app = App.Instance;
            var outcome = app.LastRaidOutcome;

            if (outcome == RaidOutcome.None)
            {
                if (_shown)
                {
                    _window?.Hide();
                    _shown = false;
                }
                return;
            }

            if (_shown) return;
            if (!EnsureWindow()) return;

            ShowFor(outcome);
            _shown = true;

            // Block gameplay input while the result screen is up — same convention
            // as the inventory / quest popups.
            app.SetGameplayInputBlocked(true);
        }

        bool EnsureWindow()
        {
            if (_triedFind) return _window != null;
            _triedFind = true;
            _window = EndOfRaidWindow.Instance
                      ?? FindObjectOfType<EndOfRaidWindow>(includeInactive: true);
            return _window != null;
        }

        void ShowFor(RaidOutcome outcome)
        {
            string title;
            string subtitle;
            bool success;

            switch (outcome)
            {
                case RaidOutcome.Extracted:
                    title = "EXTRACTED";
                    subtitle = "You made it out alive.";
                    success = true;
                    break;
                case RaidOutcome.KIA:
                    title = "YOU DIED";
                    subtitle = "Your gear was lost.";
                    success = false;
                    break;
                default:
                    return;
            }

            _window.Show(title, subtitle, success, OnNextClicked);
        }

        void OnNextClicked()
        {
            _shown = false;
            _window?.Hide();
            App.Instance.ReturnToHideout().Forget();
        }
    }
}
