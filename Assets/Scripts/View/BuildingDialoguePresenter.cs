using System.Collections.Generic;
using ApplicationCore;
using Constants;
using Session;
using State;
using Systems;
using UnityEngine;
using View.UI.CraftingMockup;
using View.UI.Dialogue;
using View.UI.WeaponBuilder;

namespace View
{
    /// <summary>
    /// Interactable buildings now open a small dialogue (reusing <see cref="NpcDialogueWindow"/>)
    /// instead of jumping straight into a popup. The choice list depends on the
    /// building's <see cref="BuildingKind"/>:
    ///   * Crafting       → "Craft" → CraftingMockupWindow (same window the F10 / DevCheats toggle opens)
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
        CraftingMockupWindow _craftWindow;

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
            return _window != null;
        }

        void OpenDialogueFor(RaidState state, EId workbenchId)
        {
            var wb = FindWorkbench(state, workbenchId);
            if (wb == null) return;

            var player = App.Instance?.Player;
            int level = player != null ? BuildingSystem.GetLevel(player, wb.Kind) : 0;

            var choices = new List<NpcDialogueWindow.Choice>();
            string baseTitle;
            string intro;

            switch (wb.Kind)
            {
                case BuildingKind.WeaponBuilder:
                    baseTitle = "Weapon Builder";
                    intro = "Modules in, weapon out. What do you want to assemble?";
                    AddMainAction(choices, "Build Weapon", level, OpenWeaponBuilder);
                    break;

                case BuildingKind.Stash:
                    baseTitle = "Stash";
                    intro = "Your gear, safe and sound. What needs sorting?";
                    AddPlaceholderMain(choices, "Open Stash", level, "open-stash");
                    break;

                case BuildingKind.SupplyTerminal:
                    baseTitle = "Supply Terminal";
                    intro = "Drop what you don't need. The market's always open.";
                    AddPlaceholderMain(choices, "Sell Items", level, "open-supply-terminal");
                    break;

                case BuildingKind.MedStation:
                    baseTitle = "Med Station";
                    intro = "Patch up before your next run.";
                    AddPlaceholderMain(choices, "Heal Up", level, "open-med-station");
                    break;

                case BuildingKind.QuestTerminal:
                    baseTitle = "Quest Terminal";
                    intro = "Open contracts and active jobs, all in one place.";
                    AddPlaceholderMain(choices, "Browse Quests", level, "open-quest-terminal");
                    break;

                default: // Crafting
                    baseTitle = "Workbench";
                    intro = "Pick a recipe and I'll fire up the bench.";
                    AddMainAction(choices, "Craft", level, OpenCraftPopup);
                    break;
            }

            // Upgrade row — always rendered. Disabled at max level or when the recipe
            // can't be paid; label always reveals the cost so the player sees what to
            // farm for.
            AddUpgradeChoice(choices, wb.Kind, player, level);

            choices.Add(MakeChoice("Exit", ExitDialogue));
            string title = $"{baseTitle} — Lv. {level}";
            _window.Show(title, intro, choices);
        }

        // Main action at level 0 is locked. We still render the choice so the player
        // knows it exists; clicking is a no-op (Enabled = false → button disabled).
        static void AddMainAction(List<NpcDialogueWindow.Choice> choices, string label,
            int level, System.Action onClick)
        {
            if (level <= 0)
            {
                choices.Add(new NpcDialogueWindow.Choice
                {
                    Label = $"{label} (Unavailable — Upgrade to Lv. 1)",
                    OnClick = null,
                    EnabledOverride = false,
                });
            }
            else
            {
                choices.Add(MakeChoice(label, onClick));
            }
        }

        // For kinds whose real action is still a placeholder (Stash, Supply, etc.).
        // At level 0 we show "Unavailable"; at level 1+ we log the placeholder tag.
        static void AddPlaceholderMain(List<NpcDialogueWindow.Choice> choices, string label,
            int level, string tag)
        {
            if (level <= 0)
            {
                choices.Add(new NpcDialogueWindow.Choice
                {
                    Label = $"{label} (Unavailable — Upgrade to Lv. 1)",
                    OnClick = null,
                    EnabledOverride = false,
                });
            }
            else
            {
                choices.Add(new NpcDialogueWindow.Choice
                {
                    Label = $"{label} (coming soon)",
                    OnClick = () => Debug.Log($"[BuildingDialogue] Placeholder '{tag}' — not yet implemented."),
                });
            }
        }

        void AddUpgradeChoice(List<NpcDialogueWindow.Choice> choices, BuildingKind kind,
            Player player, int level)
        {
            if (level >= BuildingConstants.MaxLevel)
            {
                choices.Add(new NpcDialogueWindow.Choice
                {
                    Label = "Max Level Reached",
                    OnClick = null,
                    EnabledOverride = false,
                });
                return;
            }

            var recipe = BuildingConstants.GetUpgradeRecipe(kind, level);
            string costStr = FormatRecipe(player, recipe);
            string label = $"Upgrade to Lv. {level + 1}  —  {costStr}";

            bool canAfford = player != null && BuildingSystem.CanAffordUpgrade(player, kind);
            choices.Add(new NpcDialogueWindow.Choice
            {
                Label = label,
                EnabledOverride = canAfford,
                OnClick = canAfford ? () => OnUpgradeClicked(kind) : (System.Action)null,
            });
        }

        void OnUpgradeClicked(BuildingKind kind)
        {
            var app = App.Instance;
            if (app?.Player == null) return;
            if (!BuildingSystem.TryUpgrade(app.Player, kind)) return;

            // Re-render so the new level (and the new upgrade cost / unlocked main
            // action) are reflected immediately.
            var session = app.RaidSession;
            var player = session?.RaidState?.PlayerEntity;
            if (player != null && player.CraftTargetId != EId.None)
                OpenDialogueFor(session.RaidState, player.CraftTargetId);
        }

        // Rich-text colors picked to match the rest of the UI: cyan accents elsewhere,
        // green = "satisfied", red = "missing". UI Toolkit Labels honor <color=#rrggbb>
        // tags because TextElement.enableRichText defaults to true.
        const string GreenHex = "6affc1";
        const string RedHex   = "ff5d6c";

        /// <summary>
        /// Builds the "Name 8/10, Other 3/3, …" recipe string with per-ingredient color
        /// coding: the count we have is always green, the required count is red while
        /// short and green once satisfied. Capping at recipe count is intentional — if
        /// the player has spare materials the display still reads cleanly as "10/10".
        /// </summary>
        static string FormatRecipe(Player player, BuildingIngredient[] recipe)
        {
            if (recipe == null || recipe.Length == 0) return "free";

            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < recipe.Length; i++)
            {
                if (i > 0) sb.Append(", ");

                var def = ItemDefinition.Get(recipe[i].ItemId);
                string name = def?.DisplayName ?? recipe[i].ItemId;
                int have = player != null ? BuildingSystem.GetAvailable(player, recipe[i].ItemId) : 0;
                int need = recipe[i].Count;

                bool short_ = have < need;
                string needColor = short_ ? RedHex : GreenHex;
                int shownHave = have < need ? have : need; // cap so "15/10" doesn't visually scream surplus

                sb.Append(name).Append(' ')
                  .Append("<color=#").Append(GreenHex).Append('>').Append(shownHave).Append("</color>")
                  .Append('/')
                  .Append("<color=#").Append(needColor).Append('>').Append(need).Append("</color>");
            }
            return sb.ToString();
        }

        static NpcDialogueWindow.Choice MakeChoice(string label, System.Action onClick) =>
            new() { Label = label, OnClick = onClick };

        void OpenCraftPopup()
        {
            var craftWindow = CraftingMockupWindow.Instance;
            if (craftWindow == null)
            {
                Debug.LogWarning("[BuildingDialogue] CraftingMockupWindow not initialized — can't open craft UI.");
                return;
            }
            _expectingCraftReturn = true;
            _craftWindow = craftWindow;
            _craftWindow.Closed += OnCraftPopupClosed;
            _window.Hide();
            _craftWindow.Show();
            App.Instance.SetGameplayInputBlocked(true);
        }

        void OnCraftPopupClosed()
        {
            UnsubscribeCraftWindow();
            if (!_expectingCraftReturn) return;
            _expectingCraftReturn = false;

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

            // Unsubscribe first: Hide() raises Closed, and we don't want to re-enter the
            // "return to dialogue" path while we're tearing the whole flow down.
            var craftWindow = _craftWindow;
            UnsubscribeCraftWindow();
            if (craftWindow != null && craftWindow.IsVisible) craftWindow.Hide();

            App.Instance?.SetGameplayInputBlocked(false);
        }

        void UnsubscribeCraftWindow()
        {
            if (_craftWindow == null) return;
            _craftWindow.Closed -= OnCraftPopupClosed;
            _craftWindow = null;
        }

        static WorkbenchState FindWorkbench(RaidState state, EId id)
        {
            for (int i = 0; i < state.Workbenches.Count; i++)
                if (state.Workbenches[i].Id == id) return state.Workbenches[i];
            return null;
        }

        void OnDestroy()
        {
            UnsubscribeCraftWindow();
        }
    }
}
