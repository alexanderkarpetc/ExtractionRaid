using ApplicationCore;
using State;
using UnityEngine;
using UnityEngine.InputSystem;
using View.UI.Inventory;
using View.UI.WeaponBuilder;

namespace View
{
    /// <summary>
    /// Maps player gameplay state (Tab toggle, LootTargetId, BuilderTargetId,
    /// CraftTargetId) to <see cref="InventoryWindow"/> open/close. Inventory is
    /// canonical UI Toolkit since Stage 5 — the legacy uGUI LootPopupView is
    /// gone. Inventory does NOT block gameplay input: player keeps walking +
    /// shooting (when cursor's not over UI — gated у IInputAdapter through
    /// IsPointerOverUi flag set by AimCursorOverlay).
    /// </summary>
    public class InventoryUI : MonoBehaviour
    {
        bool _isOpen;
        bool _openedByLoot;
        bool _openedByBuilder;

        void Update()
        {
            var session = App.Instance?.RaidSession;
            var player = session?.RaidState?.PlayerEntity;
            if (player == null) return;

            // UTK window's IsOpen is authoritative for user-initiated close
            // (the X button). If it dropped while we still think we're open,
            // mirror that intent here — clear LootTargetId, drop _isOpen — so
            // we don't immediately re-open the window further down.
            if (_isOpen && InventoryWindow.Instance != null && !InventoryWindow.Instance.IsOpen)
            {
                _isOpen = false;
                _openedByLoot = false;
                player.LootTargetId = EId.None;
            }

            bool builderOpen = player.BuilderTargetId != EId.None;

            var kb = Keyboard.current;
            if (kb != null && kb[Key.Tab].wasPressedThisFrame)
            {
                if (builderOpen)
                {
                    // Tab is the universal "close everything" key. While Builder
                    // is open it tears down the modal — Builder.Close clears
                    // BuilderTargetId, and the next Update sees !builderOpen and
                    // closes the inventory window naturally.
                    WeaponBuilderWindow.Instance?.Close();
                }
                else if (_isOpen)
                {
                    _isOpen = false;
                    _openedByLoot = false;
                    player.LootTargetId = EId.None;
                }
                else
                {
                    _isOpen = true;
                    player.CraftTargetId = EId.None;
                }
            }

            if (player.CraftTargetId != EId.None && _isOpen)
            {
                _isOpen = false;
                _openedByLoot = false;
            }

            if (player.LootTargetId != EId.None && !_isOpen)
            {
                _isOpen = true;
                _openedByLoot = true;
            }

            if (player.LootTargetId == EId.None && _openedByLoot)
            {
                _isOpen = false;
                _openedByLoot = false;
            }

            // Builder side-by-side: BuilderTargetId drives the inventory window
            // open/close in lockstep with the Builder modal.
            if (builderOpen && !_isOpen)
            {
                _isOpen = true;
                _openedByBuilder = true;
            }
            else if (!builderOpen && _openedByBuilder)
            {
                _isOpen = false;
                _openedByBuilder = false;
            }

            player.IsInventoryOpen = _isOpen;

            var window = InventoryWindow.Instance;
            if (window == null) return;
            if (_isOpen && !window.IsOpen)       window.Open();
            else if (!_isOpen && window.IsOpen)  window.Close();
        }
    }
}
