using ApplicationCore;
using State;
using UnityEngine;
using UnityEngine.InputSystem;
using View.UI.Inventory;
using View.UI.WeaponBuilder;

namespace View
{
    /// <summary>
    /// Bridges player gameplay state to <see cref="InventoryWindow"/> open/close.
    /// Inventory is canonical UI Toolkit since Stage 5; the legacy uGUI popup is
    /// gone. Inventory does NOT block gameplay input — player keeps walking and
    /// (when cursor's off UI) shooting. Attack/ADS gating happens у
    /// IInputAdapter through the IsPointerOverUi flag set by PointerOverUiTracker.
    ///
    /// State machine:
    ///   isOpen = !craft && (openedByTab || lootActive || builderOpen)
    /// — single boolean (`_openedByTab`) tracks user-intent open; loot/builder
    /// pull authoritative open-reasons from PlayerEntityState; craft is
    /// mutually-exclusive and forces close.
    /// </summary>
    public class InventoryUI : MonoBehaviour
    {
        // User-intent flag set by Tab toggle. Cleared by Tab (close), X-button
        // (external close via InventoryWindow), or craft starting.
        bool _openedByTab;

        // Snapshot of last frame's effective open state — needed to detect when
        // the window closed externally (e.g. user clicked X) so we mirror that
        // back into our state (clear loot/tab flags) instead of re-opening.
        bool _wasOpen;

        void Update()
        {
            var session = App.Instance?.RaidSession;
            var player  = session?.RaidState?.PlayerEntity;
            if (player == null) return;

            var window      = InventoryWindow.Instance;
            bool windowOpen = window != null && window.IsOpen;
            bool builderOpen = player.BuilderTargetId != EId.None;
            bool lootActive  = player.LootTargetId   != EId.None;
            bool craftActive = player.CraftTargetId  != EId.None;

            // External close (X button on window): drop all our open-reasons
            // so the derived isOpen below evaluates to false and we don't
            // immediately re-open the window. Also clear LootTargetId — closing
            // inv via X cancels the looting session, same as Tab close.
            if (_wasOpen && !windowOpen)
            {
                _openedByTab = false;
                player.LootTargetId = EId.None;
                lootActive = false;
            }

            // Tab / Esc handling. Suppressed while paused so the pause overlay
            // (which runs earlier and owns Esc) isn't fought over and nothing opens
            // behind it.
            var kb = Keyboard.current;
            bool paused = player.IsPaused;
            bool tabPressed = !paused && kb != null && kb[Key.Tab].wasPressedThisFrame;
            bool escPressed = !paused && kb != null && kb[Key.Escape].wasPressedThisFrame;
            if (tabPressed)
            {
                if (View.UI.Attachments.AttachmentEditorWindow.Instance != null
                    && View.UI.Attachments.AttachmentEditorWindow.Instance.IsOpen)
                {
                    // Tab while the attachment editor is open closes it first (inventory stays).
                    View.UI.Attachments.AttachmentEditorWindow.Instance.Close();
                }
                else if (builderOpen)
                {
                    // Tab while Builder open = "close everything" — closing
                    // Builder clears BuilderTargetId → builderOpen=false next
                    // frame → inv derives closed naturally.
                    WeaponBuilderWindow.Instance?.Close();
                }
                else if (_openedByTab || lootActive)
                {
                    // Currently open (any reason except builder) — Tab closes.
                    _openedByTab = false;
                    player.LootTargetId = EId.None;
                    lootActive = false;
                }
                else
                {
                    // Closed — Tab opens. Cancel craft (legacy: Tab cancels craft).
                    _openedByTab = true;
                    player.CraftTargetId = EId.None;
                    craftActive = false;
                }
            }
            else if (escPressed)
            {
                // Esc mirrors Tab's close path but never opens — so it closes the
                // topmost inventory surface, and the pause menu only opens once
                // nothing is left to close.
                if (View.UI.Attachments.AttachmentEditorWindow.Instance != null
                    && View.UI.Attachments.AttachmentEditorWindow.Instance.IsOpen)
                {
                    View.UI.Attachments.AttachmentEditorWindow.Instance.Close();
                }
                else if (builderOpen)
                {
                    WeaponBuilderWindow.Instance?.Close();
                }
                else if (_openedByTab || lootActive)
                {
                    _openedByTab = false;
                    player.LootTargetId = EId.None;
                    lootActive = false;
                }
            }

            // Craft is mutually-exclusive — cancels every open-reason. If craft
            // started externally while inv was open, inv closes.
            if (craftActive)
            {
                _openedByTab = false;
                player.LootTargetId = EId.None;
                lootActive = false;
            }

            // Derive authoritative open state.
            bool isOpen = !craftActive && (_openedByTab || lootActive || builderOpen);
            _wasOpen = isOpen;

            player.IsInventoryOpen = isOpen;

            if (window == null) return;
            if (isOpen && !windowOpen)       window.Open();
            else if (!isOpen && windowOpen)  window.Close();
        }
    }
}
