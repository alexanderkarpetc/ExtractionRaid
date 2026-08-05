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
                    // Release the input block we set when showing the screen.
                    // Without this the hideout starts with gameplay input gated → can't walk.
                    app.SetGameplayInputBlocked(false);
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
                    // A timeout death is still a KIA (same gear wipe), but reading "YOU DIED" after
                    // the clock ran out hides WHY. The KIA path leaves the session live while this
                    // screen is up, so the expired clock is still readable — no extra state needed.
                    bool timedOut = IsRaidClockExpired();
                    title = timedOut ? "TIME'S UP" : "YOU DIED";
                    subtitle = timedOut
                        ? "The raid closed with you still inside. Your gear was lost."
                        : "Your gear was lost.";
                    success = false;
                    break;
                default:
                    return;
            }

            _window.Show(title, subtitle, success, OnNextClicked);
        }

        static bool IsRaidClockExpired()
        {
            var state = App.Instance?.RaidSession?.RaidState;
            return state != null
                   && Systems.RaidTimerSystem.HasClock(state)
                   && Systems.RaidTimerSystem.TimeRemaining(state) <= 0f;
        }

        void OnNextClicked()
        {
            _shown = false;
            _window?.Hide();
            App.Instance.ReturnToHideout().Forget();
        }
    }
}
