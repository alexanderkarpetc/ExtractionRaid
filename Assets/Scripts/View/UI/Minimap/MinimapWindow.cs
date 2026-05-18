using UnityEngine;
using UnityEngine.UIElements;

namespace View.UI.Minimap
{
    /// <summary>
    /// Renders the captured environment texture + an overlay of dots for every
    /// <see cref="MinimapMarkerRegistry"/> entry. Two modes:
    ///   * Corner (HUD): small panel bottom-right.
    ///   * Expanded: large, centered overlay. Toggled by <c>MinimapPresenter</c>
    ///     when the player presses M.
    /// The window owns no game state; it's a pure visualizer.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    [RequireComponent(typeof(UIDocument))]
    public class MinimapWindow : MonoBehaviour
    {
        public static MinimapWindow Instance { get; private set; }

        // Player-centric zoom factor: viewport is this many times larger than the
        // visible frame; we then translate it so the player marker sits at the frame's
        // visual center. Tweak per-mode if you want different zoom corner vs. expanded.
        const float Zoom = 3f;

        UIDocument _doc;
        VisualElement _root;
        VisualElement _frame;
        VisualElement _viewport;  // 5× the frame; gets panned so player is centered
        VisualElement _surface;   // holds the env texture
        VisualElement _markerLayer;

        Vector2 _boundsCenterXZ;
        Vector2 _boundsSize = new Vector2(80f, 80f);
        bool _hasCapture;
        bool _isExpanded;

        void Awake()
        {
            Instance = this;
            BuildDocument();
            // Start hidden — first SetCapture call from the presenter will reveal us.
            if (_root != null) _root.style.display = DisplayStyle.None;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void BuildDocument()
        {
            var tree = Resources.Load<VisualTreeAsset>("UI/Minimap/Minimap");
            var styles = Resources.Load<StyleSheet>("UI/Minimap/Minimap");
            var panel = Resources.Load<PanelSettings>("UI/Minimap/MinimapPanelSettings");
            if (tree == null || panel == null)
            {
                Debug.LogError("[Minimap] Missing UXML or PanelSettings in Resources/UI/Minimap/.");
                return;
            }

            _doc = GetComponent<UIDocument>();
            _doc.panelSettings = panel;
            _doc.visualTreeAsset = tree;

            _root = _doc.rootVisualElement;
            if (styles != null && !_root.styleSheets.Contains(styles))
                _root.styleSheets.Add(styles);
            _root.style.flexGrow = 1;
            _root.pickingMode = PickingMode.Ignore;

            _frame = _root.Q<VisualElement>("frame");
            _viewport = _root.Q<VisualElement>("viewport");
            _surface = _root.Q<VisualElement>("surface");
            _markerLayer = _root.Q<VisualElement>("markerLayer");
        }

        /// <summary>
        /// Hand the captured env texture and the bounds rectangle to the window so
        /// it can render the background and resolve world→UI coordinates for markers.
        /// </summary>
        public void SetCapture(RenderTexture envTexture, Vector2 boundsCenterXZ, Vector2 boundsSize)
        {
            if (_surface == null) return;
            _boundsCenterXZ = boundsCenterXZ;
            _boundsSize = new Vector2(Mathf.Max(0.01f, boundsSize.x), Mathf.Max(0.01f, boundsSize.y));
            _hasCapture = envTexture != null;
            _surface.style.backgroundImage = envTexture != null
                ? new StyleBackground(Background.FromRenderTexture(envTexture))
                : new StyleBackground(StyleKeyword.None);

            // Hide the whole frame when we don't have a capture — the minimap should
            // be invisible in the main menu / between raids.
            if (_root != null)
                _root.style.display = envTexture != null ? DisplayStyle.Flex : DisplayStyle.None;
        }

        public void SetExpanded(bool expanded)
        {
            if (_frame == null) return;
            _isExpanded = expanded;
            if (expanded) _frame.AddToClassList("expanded");
            else          _frame.RemoveFromClassList("expanded");
        }

        public bool IsExpanded => _isExpanded;

        /// <summary>
        /// Sizes the viewport to Zoom × frame, translates it so the player marker
        /// sits at the frame's visual center, and rebuilds marker dots in viewport
        /// coordinates. Cheap enough to run every frame for the marker counts we
        /// expect (a few dozen).
        /// </summary>
        public void RefreshMarkers()
        {
            if (_markerLayer == null || _viewport == null || _frame == null) return;
            _markerLayer.Clear();
            if (!_hasCapture) return;

            float frameW = _frame.contentRect.width;
            float frameH = _frame.contentRect.height;
            if (frameW <= 0f || frameH <= 0f) return;

            float viewW = frameW * Zoom;
            float viewH = frameH * Zoom;
            _viewport.style.width = viewW;
            _viewport.style.height = viewH;

            // Center the viewport on the player marker if one is registered; fall back
            // to centering the world bounds if not (so the minimap is still useful
            // when no player exists yet — e.g. between raids).
            Vector3 cameraTargetWorld;
            if (TryGetPlayerWorldPos(out var playerWorld))
                cameraTargetWorld = playerWorld;
            else
                cameraTargetWorld = new Vector3(_boundsCenterXZ.x, 0f, _boundsCenterXZ.y);

            // Player's pixel position inside the (un-translated) viewport.
            WorldToViewport(cameraTargetWorld, viewW, viewH, out var cx, out var cy);

            // Translate so that pixel ends up at (frameW/2, frameH/2).
            _viewport.style.left = (frameW * 0.5f) - cx;
            _viewport.style.top  = (frameH * 0.5f) - cy;

            // Render markers in viewport coords. Stuff outside the frame is clipped
            // by the parent's overflow:hidden — no need to filter here.
            foreach (var m in MinimapMarkerRegistry.Markers)
            {
                var world = m.ResolvePosition();
                WorldToViewport(world, viewW, viewH, out var mx, out var my);

                var dot = new VisualElement();
                dot.AddToClassList("marker");
                dot.AddToClassList(ClassFor(m.Type));
                dot.style.left = mx - MarkerHalfSizeFor(m.Type);
                dot.style.top  = my - MarkerHalfSizeFor(m.Type);
                if (!string.IsNullOrEmpty(m.Tooltip)) dot.tooltip = m.Tooltip;
                _markerLayer.Add(dot);
            }
        }

        // ── World → viewport mapping ─────────────────────────────────
        // Camera looks down -Y, so XZ plane maps to image XY with Z growing "up" in
        // the image. UI Toolkit's top-left origin means we flip the v coordinate.
        // Note: no off-screen filter here — the parent frame clips with overflow:hidden,
        // so off-viewport markers are simply not visible.
        void WorldToViewport(Vector3 worldPos, float viewW, float viewH,
            out float px, out float py)
        {
            float minX = _boundsCenterXZ.x - _boundsSize.x * 0.5f;
            float minZ = _boundsCenterXZ.y - _boundsSize.y * 0.5f;
            float u = (worldPos.x - minX) / _boundsSize.x;
            float v = (worldPos.z - minZ) / _boundsSize.y;
            px = u * viewW;
            py = (1f - v) * viewH;
        }

        static bool TryGetPlayerWorldPos(out Vector3 pos)
        {
            foreach (var m in MinimapMarkerRegistry.Markers)
            {
                if (m.Type != MinimapMarkerType.Player) continue;
                pos = m.ResolvePosition();
                return true;
            }
            pos = default;
            return false;
        }

        static string ClassFor(MinimapMarkerType t) => t switch
        {
            MinimapMarkerType.Player     => "marker--player",
            MinimapMarkerType.Npc        => "marker--npc",
            MinimapMarkerType.Extraction => "marker--extract",
            MinimapMarkerType.Quest      => "marker--quest",
            _                            => "marker--custom",
        };

        static float MarkerHalfSizeFor(MinimapMarkerType t) =>
            t == MinimapMarkerType.Player ? 6f : 5f;
    }
}
