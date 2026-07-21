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

        static Texture2D s_playerArrowTex;
        const int PlayerArrowSize = 18;

        static Texture2D s_extractionIconTex;
        const int ExtractionIconSize = 44;

        static Texture2D s_questIconTex;
        const int QuestIconSize = 26;

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
                if (m.Type == MinimapMarkerType.Player)
                {
                    float half = PlayerArrowSize * 0.5f;
                    dot.style.width = PlayerArrowSize;
                    dot.style.height = PlayerArrowSize;
                    dot.style.left = mx - half;
                    dot.style.top  = my - half;
                    dot.style.backgroundImage = new StyleBackground(GetPlayerArrowTexture());
                    dot.style.rotate = new StyleRotate(
                        new Rotate(new Angle(m.ResolveRotation(), AngleUnit.Degree)));
                }
                else if (m.Type == MinimapMarkerType.Extraction)
                {
                    ApplyIcon(dot, GetExtractionIconTexture(), ExtractionIconSize, mx, my, m.Type);
                }
                else if (m.Type == MinimapMarkerType.Quest)
                {
                    ApplyIcon(dot, GetQuestIconTexture(), QuestIconSize, mx, my, m.Type);
                }
                else
                {
                    dot.style.left = mx - MarkerHalfSizeFor(m.Type);
                    dot.style.top  = my - MarkerHalfSizeFor(m.Type);
                }
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

        static Texture2D GetExtractionIconTexture()
        {
            if (s_extractionIconTex != null) return s_extractionIconTex;
            s_extractionIconTex = Resources.Load<Texture2D>("UI/Minimap/exit_icon_32x32");
            return s_extractionIconTex;
        }

        // Procedurally painted quest "!" badge — a yellow disc з dark rim + dark
        // exclamation, matching the in-world NpcQuestIndicator. Generated in code (same
        // approach as the player arrow) so it needs no sprite asset and reads as a clean
        // "!" instead of the old generic pin texture. Cached statically.
        static Texture2D GetQuestIconTexture()
        {
            if (s_questIconTex != null) return s_questIconTex;

            const int S = 64;
            var tex = new Texture2D(S, S, TextureFormat.RGBA32, mipChain: false)
            {
                name = "Minimap_QuestIcon",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave,
            };

            var fill      = new Color32(255, 205, 45, 255);   // quest yellow (matches world badge)
            var dark      = new Color32(28, 22, 6, 255);      // rim + "!" glyph
            var clear     = new Color32(0, 0, 0, 0);
            var center    = new Vector2(0.5f, 0.5f);
            var dotCenter = new Vector2(0.5f, 0.74f);

            var pixels = new Color32[S * S];
            for (int y = 0; y < S; y++)
            {
                for (int x = 0; x < S; x++)
                {
                    // Texture2D y=0 is bottom; element y=0 is top. Flip so the "!" bar
                    // (semantic top) lands at the top of the element, dot below it.
                    var p = new Vector2((x + 0.5f) / S, 1f - (y + 0.5f) / S);
                    float d = Vector2.Distance(p, center);
                    Color32 c;
                    if (d > 0.5f) c = clear;
                    else if (d > 0.46f) c = dark;             // rim outline
                    else
                    {
                        bool inBar = p.x >= 0.43f && p.x <= 0.57f && p.y >= 0.22f && p.y <= 0.585f;
                        bool inDot = Vector2.Distance(p, dotCenter) <= 0.078f;
                        c = (inBar || inDot) ? dark : fill;
                    }
                    pixels[y * S + x] = c;
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply(false, false);
            s_questIconTex = tex;
            return s_questIconTex;
        }

        static void ApplyIcon(VisualElement dot, Texture2D tex, int size,
            float mx, float my, MinimapMarkerType type)
        {
            if (tex == null)
            {
                dot.style.left = mx - MarkerHalfSizeFor(type);
                dot.style.top  = my - MarkerHalfSizeFor(type);
                return;
            }
            float half = size * 0.5f;
            dot.style.width = size;
            dot.style.height = size;
            dot.style.left = mx - half;
            dot.style.top  = my - half;
            dot.style.backgroundImage = new StyleBackground(tex);
            // Icon markers use a transparent texture — clear the inherited .marker square
            // (1px border + type background-color) so only the glyph shows. Mirrors the
            // per-frame clearing that .marker--player does in USS.
            dot.style.backgroundColor = new StyleColor(new Color(0, 0, 0, 0));
            dot.style.borderTopWidth = 0;
            dot.style.borderBottomWidth = 0;
            dot.style.borderLeftWidth = 0;
            dot.style.borderRightWidth = 0;
        }

        // Procedurally painted player arrow: kite/chevron pointing up at yaw=0, with
        // a V-notch carved out of the base so it reads as a "you" arrow at small sizes.
        // Cached statically — same shape for every raid, generated on first use.
        static Texture2D GetPlayerArrowTexture()
        {
            if (s_playerArrowTex != null) return s_playerArrowTex;

            const int S = 64;
            var tex = new Texture2D(S, S, TextureFormat.RGBA32, mipChain: false)
            {
                name = "Minimap_PlayerArrow",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave,
            };

            var fill    = new Color32(135, 230, 255, 255);
            var outline = new Color32(15, 25, 35, 230);
            var clear   = new Color32(0, 0, 0, 0);

            // Arrow polygon in normalized (x,y) coords with y growing downward, apex up:
            //   apex      (0.50, 0.05)
            //   right     (0.95, 0.95)
            //   notch tip (0.50, 0.65)
            //   left      (0.05, 0.95)
            var apex   = new Vector2(0.50f, 0.05f);
            var right  = new Vector2(0.95f, 0.95f);
            var notch  = new Vector2(0.50f, 0.65f);
            var left   = new Vector2(0.05f, 0.95f);

            var pixels = new Color32[S * S];
            for (int y = 0; y < S; y++)
            {
                for (int x = 0; x < S; x++)
                {
                    // Texture2D y=0 is bottom; element y=0 is top. Flip so the
                    // polygon's apex (semantic y≈0) lands at the top of the element.
                    var p = new Vector2((x + 0.5f) / S, 1f - (y + 0.5f) / S);
                    bool inside =
                        PointInTriangle(p, apex, right, notch) ||
                        PointInTriangle(p, apex, notch, left);
                    pixels[y * S + x] = inside ? fill : clear;
                }
            }

            // Thin outline by sampling 4-neighbors: any clear pixel adjacent to a fill
            // pixel becomes outline. Keeps the arrow legible against the map texture.
            var outlined = new Color32[S * S];
            for (int y = 0; y < S; y++)
            {
                for (int x = 0; x < S; x++)
                {
                    int idx = y * S + x;
                    if (pixels[idx].a > 0) { outlined[idx] = fill; continue; }
                    bool nearFill =
                        (x > 0     && pixels[idx - 1].a > 0) ||
                        (x < S - 1 && pixels[idx + 1].a > 0) ||
                        (y > 0     && pixels[idx - S].a > 0) ||
                        (y < S - 1 && pixels[idx + S].a > 0);
                    outlined[idx] = nearFill ? outline : clear;
                }
            }

            tex.SetPixels32(outlined);
            tex.Apply(updateMipmaps: false, makeNoLongerReadable: true);
            s_playerArrowTex = tex;
            return tex;
        }

        static bool PointInTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            float d1 = Sign(p, a, b);
            float d2 = Sign(p, b, c);
            float d3 = Sign(p, c, a);
            bool hasNeg = d1 < 0f || d2 < 0f || d3 < 0f;
            bool hasPos = d1 > 0f || d2 > 0f || d3 > 0f;
            return !(hasNeg && hasPos);
        }

        static float Sign(Vector2 p1, Vector2 p2, Vector2 p3) =>
            (p1.x - p3.x) * (p2.y - p3.y) - (p2.x - p3.x) * (p1.y - p3.y);
    }
}
