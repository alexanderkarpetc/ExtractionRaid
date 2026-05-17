using UnityEngine;
using UnityEngine.UIElements;

namespace View.UI
{
    /// <summary>
    /// Centralised "is the cursor over any pick-enabled UI Toolkit element"
    /// hit-test. Iterates every live <see cref="UIDocument"/> in the scene,
    /// converts screen→panel coords, and asks the panel to <c>Pick</c>.
    /// Returns true when ANY panel reports a hit — backdrops з
    /// <c>picking-mode=Ignore</c> pass through transparently.
    ///
    /// Used by <see cref="AimCursorOverlay"/> для cursor visibility / attack
    /// gating, and by <see cref="View.UI.Inventory.InventoryWindow"/> для
    /// drop-over-UI silent-cancel logic.
    ///
    /// Doc list is lazily populated once and survives until manual
    /// invalidation. UTK hosts in this project are spawned once у AppBootstrap
    /// and never added/destroyed mid-session — call <see cref="Invalidate"/>
    /// after dynamic UIDocument lifecycle changes. Domain reload (Play stop /
    /// script change) resets the cache automatically.
    /// </summary>
    public static class UiPanelHitTest
    {
        static UIDocument[] _docsCache;

        /// <summary>
        /// Drop the cached UIDocument list. Call when modals are dynamically
        /// added/removed (none in this project today). No-op if cache empty.
        /// </summary>
        public static void Invalidate() => _docsCache = null;

        /// <summary>
        /// Screen-space (Input.mousePosition / Mouse.current.position) origin
        /// is bottom-left. UTK panels expect top-left origin so we flip Y
        /// before <see cref="RuntimePanelUtils.ScreenToPanel"/>.
        /// </summary>
        public static bool IsScreenPointOverUi(Vector2 screenPos)
        {
            if (_docsCache == null)
            {
                _docsCache = Object.FindObjectsByType<UIDocument>(
                    FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            }

            foreach (var doc in _docsCache)
            {
                var root = doc != null ? doc.rootVisualElement : null;
                if (root == null) continue;
                if (root.resolvedStyle.display == DisplayStyle.None) continue;
                var panel = root.panel;
                if (panel == null) continue;

                Vector2 panelPos = RuntimePanelUtils.ScreenToPanel(panel,
                    new Vector2(screenPos.x, Screen.height - screenPos.y));

                if (panel.Pick(panelPos) != null) return true;
            }
            return false;
        }
    }
}
