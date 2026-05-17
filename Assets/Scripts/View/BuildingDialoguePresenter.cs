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
    /// Interactable buildings now open a small dialogue (reusing <see cref="NpcDialogueWindow"/>)
    /// instead of jumping straight into a popup. The choice list depends on the
    /// building's <see cref="BuildingKind"/>:
    ///   * Crafting       → "Craft" → CraftPopupView
    ///   * WeaponBuilder  → "Build Weapon" → WeaponBuilderWindow
    ///   * Stash / Supply / MedStation / QuestTerminal → placeholder log actions until
    ///     per-kind UIs land.
    /// Each kind also gets an "Upgrade … (coming soon)" placeholder and an "Exit"
    /// choice. Picking an action that opens a modal hides the dialogue and returns
    /// to it when the modal closes (if the player is still standing at the building).
    /// "Exit" clears <see cref="PlayerEntityState.CraftTargetId"/>, tearing everything down.
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

            var choices = new List<NpcDialogueWindow.Choice>();
            string title;
            string intro;

            switch (wb.Kind)
            {
                case BuildingKind.WeaponBuilder:
                    title = "Weapon Builder";
                    intro = "Modules in, weapon out. What do you want to assemble?";
                    choices.Add(Choice("Build Weapon", OpenWeaponBuilder));
                    choices.Add(Placeholder("Upgrade / Build (coming soon)", "upgrade-weapon-builder"));
                    break;

                case BuildingKind.Stash:
                    title = "Stash";
                    intro = "Your gear, safe and sound. What needs sorting?";
                    choices.Add(Placeholder("Open Stash (coming soon)", "open-stash"));
                    choices.Add(Placeholder("Upgrade Stash (coming soon)", "upgrade-stash"));
                    break;

                case BuildingKind.SupplyTerminal:
                    title = "Supply Terminal";
                    intro = "Drop what you don't need. The market's always open.";
                    choices.Add(Placeholder("Sell Items (coming soon)", "open-supply-terminal"));
                    choices.Add(Placeholder("Upgrade Terminal (coming soon)", "upgrade-supply-terminal"));
                    break;

                case BuildingKind.MedStation:
                    title = "Med Station";
                    intro = "Patch up before your next run.";
                    choices.Add(Placeholder("Heal Up (coming soon)", "open-med-station"));
                    choices.Add(Placeholder("Upgrade Station (coming soon)", "upgrade-med-station"));
                    break;

                case BuildingKind.QuestTerminal:
                    title = "Quest Terminal";
                    intro = "Open contracts and active jobs, all in one place.";
                    choices.Add(Placeholder("Browse Quests (coming soon)", "open-quest-terminal"));
                    choices.Add(Placeholder("Upgrade Terminal (coming soon)", "upgrade-quest-terminal"));
                    break;

                default: // Crafting
                    title = "Workbench";
                    intro = "Pick a recipe and I'll fire up the bench.";
                    choices.Add(Choice("Craft", OpenCraftPopup));
                    choices.Add(Placeholder("Upgrade Workbench (coming soon)", "upgrade-workbench"));
                    break;
            }

            choices.Add(Choice("Exit", ExitDialogue));
            _window.Show(title, intro, choices);
        }

        // Small builders to keep the switch readable. Placeholder logs a stable tag so
        // future grep / analytics can pinpoint which button is being clicked before it's
        // hooked up to real behavior.
        static NpcDialogueWindow.Choice Choice(string label, System.Action onClick) =>
            new() { Label = label, OnClick = onClick };

        static NpcDialogueWindow.Choice Placeholder(string label, string tag) =>
            new()
            {
                Label = label,
                OnClick = () => Debug.Log($"[BuildingDialogue] Placeholder '{tag}' — not yet implemented."),
            };

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
