using ApplicationCore;
using Dev;
using UnityEngine;
using UnityEngine.InputSystem;
using View.UI;

namespace View
{
    /// <summary>
    /// Per-frame "is the OS cursor over any UI Toolkit panel?" tracker.
    /// Drives three cross-cutting concerns from a single Update() pass:
    /// 1. <c>App.SetPointerOverUi(bool)</c> — broadcast flag consumed by
    ///    <see cref="UnityInputAdapter"/> for attack/ADS gating (clicks on a
    ///    UI panel must NOT fire the weapon) and by <c>CrosshairPresenter</c>
    ///    to hide the v2 reticle (OS cursor takes over).
    /// 2. <c>Cursor.visible</c> — show OS cursor in menus, when pointer over
    ///    UI Toolkit panel, or when crosshair globally disabled in cheats.
    /// 3. Pointer detection delegated to <see cref="UiPanelHitTest"/>.
    ///
    /// Lives in <see cref="AppBootstrap"/> as а component on the bootstrap GO.
    /// Replaces legacy <c>AimCursorOverlay.Update</c> (deleted with Aim Cursor v2 Stage 7);
    /// the cursor-rendering responsibility moved to <c>CrosshairPresenter</c> (uGUI + SDF).
    /// </summary>
    public class PointerOverUiTracker : MonoBehaviour
    {
        void Update()
        {
            var session = App.Instance?.RaidSession;
            var player = session?.RaidState?.PlayerEntity;
            bool inGameplay = player != null;
            bool inMenu = player != null && player.IsInMenu;

            Vector2 mouseScreen = Mouse.current?.position.ReadValue() ?? Vector2.zero;
            bool pointerOverUi = inGameplay && UiPanelHitTest.IsScreenPointOverUi(mouseScreen);
            App.Instance?.SetPointerOverUi(pointerOverUi);

            UnityEngine.Cursor.visible = !inGameplay || !DevCheats.CrosshairEnabled || inMenu || pointerOverUi;
        }
    }
}
