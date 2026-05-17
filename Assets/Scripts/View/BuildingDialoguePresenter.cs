using System.Collections.Generic;
using ApplicationCore;
using State;
using UnityEngine;
using View.UI;
using View.UI.Craft;
using View.UI.Dialogue;
using View.UI.WeaponBuilder;

namespace View
{
    /// <summary>
    /// Workbenches now open a small dialogue (reusing <see cref="NpcDialogueWindow"/>)
    /// instead of jumping straight into the recipe / builder popup. Choices depend on
    /// <see cref="WorkbenchKind"/>:
    ///   * Crafting     → "Craft", "Upgrade (coming soon)", "Exit"
    ///   * WeaponBuilder → "Build Weapon", "Upgrade (coming soon)", "Exit"
    /// Picking an action hides the dialogue and opens the matching modal; closing the
    /// modal returns to the dialogue if the player is still standing at the workbench.
    /// Exit clears <see cref="PlayerEntityState.CraftTargetId"/> which tears everything down.
    /// </summary>
    public class BuildingDialoguePresenter : MonoBehaviour
    {
        NpcDialogueWindow _window;
        PopupManager _popupManager;
        CraftPopupView _craftPopupView;

        bool _triedFind;
        EId _lastCraftTargetId = EId.None;

        // When we hand off to a sub-popup (Craft / Weapon Builder), we hide the dialogue
        // and watch for the sub-popup's close to re-show it. The flag tells us "we own
        // this popup-close event" — so other systems closing the popup don't reset our flow.
        bool _expectingCraftReturn;
        bool _expectingBuilderReturn;

        void Update()
        {
            var session = App.Instance?.RaidSession;
            var player = session?.RaidState?.PlayerEntity;
            if (player == null) return;
            if (!EnsureRefs()) return;

            // Detect transitions on CraftTargetId (= "I'm standing at a workbench").
            if (player.CraftTargetId != _lastCraftTargetId)
            {
                _lastCraftTargetId = player.CraftTargetId;
                if (player.CraftTargetId != EId.None)
                    OpenDialogueFor(session.RaidState, player.CraftTargetId);
                else
                    CloseEverything();
            }

            // While a sub-popup is up the dialogue is hidden. Detect its close and re-show.
            if (_expectingBuilderReturn && WeaponBuilderWindow.Instance != null
                && player.BuilderTargetId == EId.None)
            {
                _expectingBuilderReturn = false;
                if (player.CraftTargetId != EId.None)
                    OpenDialogueFor(session.RaidState, player.CraftTargetId);
            }

            // Keep gameplay input blocked while either UI surface is up.
            if (_window != null && _window.IsVisible)
                App.Instance.SetGameplayInputBlocked(true);
        }

        bool EnsureRefs()
        {
            if (_triedFind) return _window != null;
            _triedFind = true;
            _window = NpcDialogueWindow.Instance
                      ?? FindObjectOfType<NpcDialogueWindow>(includeInactive: true);
            _popupManager = FindObjectOfType<PopupManager>(includeInactive: true);
            _craftPopupView = FindObjectOfType<CraftPopupView>(includeInactive: true);

            if (_craftPopupView != null)
                _craftPopupView.Closed += OnCraftPopupClosed;

            return _window != null;
        }

        void OpenDialogueFor(RaidState state, EId workbenchId)
        {
            var wb = FindWorkbench(state, workbenchId);
            if (wb == null) return;

            string title = wb.Kind switch
            {
                WorkbenchKind.WeaponBuilder => "Weapon Builder",
                _                           => "Workbench",
            };
            string intro = wb.Kind switch
            {
                WorkbenchKind.WeaponBuilder => "Modules in, weapon out. What do you want to assemble?",
                _                           => "Pick a recipe and I'll fire up the bench.",
            };

            var choices = new List<NpcDialogueWindow.Choice>();

            if (wb.Kind == WorkbenchKind.WeaponBuilder)
            {
                choices.Add(new NpcDialogueWindow.Choice
                {
                    Label = "Build Weapon",
                    OnClick = OpenWeaponBuilder,
                });
            }
            else
            {
                choices.Add(new NpcDialogueWindow.Choice
                {
                    Label = "Craft",
                    OnClick = OpenCraftPopup,
                });
            }

            // Placeholder for the next step — wired-up later. Logs so it's obvious in tests.
            choices.Add(new NpcDialogueWindow.Choice
            {
                Label = "Upgrade / Build (coming soon)",
                OnClick = () => Debug.Log("[BuildingDialogue] Upgrade/Build placeholder — not yet implemented."),
            });

            choices.Add(new NpcDialogueWindow.Choice
            {
                Label = "Exit",
                OnClick = ExitDialogue,
            });

            _window.Show(title, intro, choices);
        }

        void OpenCraftPopup()
        {
            if (_popupManager == null || _craftPopupView == null)
            {
                Debug.LogWarning("[BuildingDialogue] Craft popup missing in scene — can't open.");
                return;
            }
            _expectingCraftReturn = true;
            _window.Hide();
            _popupManager.Open(_craftPopupView);
            _craftPopupView.Open();
            App.Instance.SetGameplayInputBlocked(true);
        }

        void OnCraftPopupClosed()
        {
            if (!_expectingCraftReturn) return;
            _expectingCraftReturn = false;

            _popupManager?.Close();
            var player = App.Instance?.RaidSession?.RaidState?.PlayerEntity;
            if (player == null || player.CraftTargetId == EId.None)
            {
                App.Instance?.SetGameplayInputBlocked(false);
                return;
            }
            OpenDialogueFor(App.Instance.RaidSession.RaidState, player.CraftTargetId);
        }

        void OpenWeaponBuilder()
        {
            var builder = WeaponBuilderWindow.Instance;
            if (builder == null)
            {
                Debug.LogWarning("[BuildingDialogue] WeaponBuilderWindow not initialized — " +
                                 "CoreDefinitionDatabase may be missing. Falling back to no-op.");
                return;
            }
            _expectingBuilderReturn = true;
            _window.Hide();
            builder.Open();
        }

        void ExitDialogue()
        {
            var player = App.Instance?.RaidSession?.RaidState?.PlayerEntity;
            if (player == null) return;
            player.CraftTargetId = EId.None; // triggers CloseEverything next Update
        }

        void CloseEverything()
        {
            _expectingCraftReturn = false;
            _expectingBuilderReturn = false;
            if (_window != null) _window.Hide();
            if (_popupManager != null && _craftPopupView != null && _popupManager.IsOpen(_craftPopupView))
                _popupManager.Close();
            App.Instance?.SetGameplayInputBlocked(false);
        }

        static WorkbenchState FindWorkbench(RaidState state, EId id)
        {
            for (int i = 0; i < state.Workbenches.Count; i++)
                if (state.Workbenches[i].Id == id) return state.Workbenches[i];
            return null;
        }

        void OnDestroy()
        {
            if (_craftPopupView != null)
                _craftPopupView.Closed -= OnCraftPopupClosed;
        }
    }
}
