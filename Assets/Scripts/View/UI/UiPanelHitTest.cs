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
    /// Used by <see cref="PointerOverUiTracker"/> для cursor visibility / attack
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

        // Result memo. PointerOverUiTracker calls this every frame, and panel.Pick walks
        // the visual tree of every open panel. The answer can only change when the query
        // point moves or the set of displayed panels changes, so both are keyed on.
        static Vector2 _memoPoint = new(float.NaN, float.NaN);
        static int _memoDisplayKey;
        static bool _memoValid;
        static bool _memoResult;

        /// <summary>
        /// Drop the cached UIDocument list. Call when modals are dynamically
        /// added/removed (none in this project today). No-op if cache empty.
        /// </summary>
        public static void Invalidate()
        {
            _docsCache = null;
            _memoValid = false;
        }

        // Reload Domain is disabled in this project (EditorSettings
        // m_EnterPlayModeOptions=1), so static fields survive Play→Stop→Play.
        // Без цього коллбеку cache тримав би destroyed UIDocument refs після
        // рестарту Play Mode → IsScreenPointOverUi завжди повертав би false.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetCacheOnPlay()
        {
            _docsCache = null;
            _memoValid = false;
        }

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

            // Order-stable fingerprint of which panels are currently displayed. Reading
            // resolvedStyle per document is cheap; picking through them is not. Including
            // this means a window opening or closing under a stationary cursor still
            // re-picks instead of being served a stale answer.
            int displayKey = 17;
            foreach (var doc in _docsCache)
            {
                var root = doc != null ? doc.rootVisualElement : null;
                bool live = root != null && root.panel != null
                            && root.resolvedStyle.display != DisplayStyle.None;
                displayKey = displayKey * 31 + (live ? 1 : 0);
            }

            if (_memoValid && displayKey == _memoDisplayKey && screenPos.Equals(_memoPoint))
                return _memoResult;

            bool hit = false;
            foreach (var doc in _docsCache)
            {
                var root = doc != null ? doc.rootVisualElement : null;
                if (root == null) continue;
                if (root.resolvedStyle.display == DisplayStyle.None) continue;
                var panel = root.panel;
                if (panel == null) continue;

                Vector2 panelPos = RuntimePanelUtils.ScreenToPanel(panel,
                    new Vector2(screenPos.x, Screen.height - screenPos.y));

                if (panel.Pick(panelPos) != null) { hit = true; break; }
            }

            _memoPoint = screenPos;
            _memoDisplayKey = displayKey;
            _memoResult = hit;
            _memoValid = true;
            return hit;
        }
    }
}
