using ApplicationCore;
using Constants;
using State;
using UnityEngine;
using View.UI.Extraction;

namespace View
{
    /// <summary>
    /// Drives the <see cref="ExtractionHudWindow"/> from <see cref="PlayerEntityState"/>:
    ///   * In a zone     → Progress mode, ring fills.
    ///   * Just left zone with partial progress → Interrupted flash for ~1.2s, then hide.
    ///   * Progress hits 1 → Complete mode + <see cref="App.RequestExtraction"/> (once).
    /// Lives in View because it touches App (system code can't); the system only
    /// mutates the player's extraction fields.
    /// </summary>
    public class ExtractionHudPresenter : MonoBehaviour
    {
        const float InterruptedDisplaySeconds = 1.2f;

        ExtractionHudWindow _window;
        bool _triedFind;

        // Internal tracking so we can detect transitions without polluting state.
        bool _wasInZone;
        float _lastProgress;
        bool _showingInterrupted;
        float _interruptedHideAt;
        bool _completed;
        string _lastZoneLabel = "";

        void Update()
        {
            if (!App.IsInitialized) return;
            var app = App.Instance;
            var session = app.RaidSession;
            var player = session?.RaidState?.PlayerEntity;

            // Out of raid — clear any leftover HUD state.
            if (player == null)
            {
                ClearWidget();
                return;
            }

            if (!EnsureWindow()) return;

            bool inZone = player.ActiveExtractionPointId != EId.None;
            float progress = player.ExtractionProgress01;

            // ── Completion path ────────────────────────────────
            if (!_completed && progress >= 1f)
            {
                _completed = true;
                _showingInterrupted = false;
                string label = ResolveZoneLabel(session.RaidState, player.ActiveExtractionPointId)
                               ?? _lastZoneLabel;
                _window.Show(ExtractionHudWindow.HudMode.Complete, 1f, 0f, label);
                // Hand off — RequestExtraction nulls the session, EndOfRaid screen takes over.
                app.RequestExtraction();
                return;
            }

            // ── Already extracted this raid — keep widget hidden, end-of-raid screen owns the view.
            if (_completed) return;

            // ── In zone → progress mode ────────────────────────
            if (inZone)
            {
                string label = ResolveZoneLabel(session.RaidState, player.ActiveExtractionPointId);
                if (!string.IsNullOrEmpty(label)) _lastZoneLabel = label;
                float duration = Mathf.Max(0.0001f, ExtractionConstants.ExtractDurationSeconds);
                float remaining = Mathf.Max(0f, (1f - progress) * duration);

                _showingInterrupted = false;
                _window.Show(ExtractionHudWindow.HudMode.Progress, progress, remaining, label);
            }
            else
            {
                // ── Out of zone ────────────────────────────────
                bool hadPartial = _wasInZone && _lastProgress > 0f && _lastProgress < 1f;
                if (hadPartial && !_showingInterrupted)
                {
                    _showingInterrupted = true;
                    _interruptedHideAt = Time.unscaledTime + InterruptedDisplaySeconds;
                    _window.Show(ExtractionHudWindow.HudMode.Interrupted, 0f, 0f, _lastZoneLabel);
                }
                else if (_showingInterrupted)
                {
                    if (Time.unscaledTime >= _interruptedHideAt)
                    {
                        _showingInterrupted = false;
                        _window.Hide();
                    }
                }
                else if (_window.IsVisible)
                {
                    _window.Hide();
                }
            }

            _wasInZone = inZone;
            _lastProgress = progress;
        }

        bool EnsureWindow()
        {
            if (_triedFind) return _window != null;
            _triedFind = true;
            _window = ExtractionHudWindow.Instance
                      ?? FindObjectOfType<ExtractionHudWindow>(includeInactive: true);
            return _window != null;
        }

        void ClearWidget()
        {
            _wasInZone = false;
            _lastProgress = 0f;
            _showingInterrupted = false;
            _completed = false;
            if (_window != null && _window.IsVisible) _window.Hide();
        }

        static string ResolveZoneLabel(RaidState state, EId pointId)
        {
            if (state?.ExtractionPoints == null) return null;
            for (int i = 0; i < state.ExtractionPoints.Count; i++)
                if (state.ExtractionPoints[i].Id == pointId)
                    return state.ExtractionPoints[i].Label;
            return null;
        }
    }
}
