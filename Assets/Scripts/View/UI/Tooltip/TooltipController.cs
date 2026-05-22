using UnityEngine;
using UnityEngine.UIElements;

namespace View.UI.Tooltip
{
    /// <summary>
    /// Single overlay that renders a <see cref="TooltipModel"/> as a floating card.
    /// Lives on a child GameObject of <c>AppBootstrap</c> and is reachable via the
    /// static <see cref="Instance"/> property — same view-singleton pattern as
    /// <c>WeaponBuilderWindow</c>.
    ///
    /// Cross-stack safe: callers from uGUI (inventory) and UI Toolkit (Builder) both
    /// invoke <see cref="Show"/> with a screen position. The overlay re-uses its own
    /// <see cref="UIDocument"/> with the highest sortingOrder so it draws on top of
    /// whatever UI is open.
    ///
    /// Coordinates:
    /// <list type="bullet">
    ///   <item><see cref="Show"/> — uGUI flavour. Takes screen-space pos
    ///     (bottom-left origin, actual screen pixels — what
    ///     <c>PointerEventData.position</c> reports). Converted internally to
    ///     panel coords accounting for the active <c>PanelSettings</c> scale
    ///     (otherwise the cursor↔tooltip offset is wrong on any non-1080p
    ///     display under <see cref="PanelScaleMode.ScaleWithScreenSize"/>).</item>
    ///   <item><see cref="ShowFromPanel"/> — UI Toolkit flavour. Takes panel
    ///     coords directly (top-left origin, reference pixels — what
    ///     <c>PointerEnterEvent.position</c> reports). No conversion.</item>
    /// </list>
    /// </summary>
    [DefaultExecutionOrder(-90)]
    public class TooltipController : MonoBehaviour
    {
        public static TooltipController Instance { get; private set; }

        const float CursorOffsetX  = 16f;
        const float CursorOffsetY  = 16f;
        const float ViewportPadding = 8f;

        UIDocument _doc;
        VisualElement _root;
        VisualElement _card;
        Label _title;
        Label _subtitle;
        Label _description;
        VisualElement _sections;

        bool _isVisible;

        void Awake()
        {
            Instance = this;
            BuildDocument();
            HideImmediate();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ── Public API ────────────────────────────────────────

        /// <summary>
        /// UI-Toolkit-friendly variant. <paramref name="panelPos"/> is already in
        /// panel (reference) coordinates with top-left origin — exactly what
        /// <c>PointerEnterEvent.position</c> reports. No conversion needed.
        /// </summary>
        public void ShowFromPanel(TooltipModel model, Vector2 panelPos)
        {
            ShowAtPanelPos(model, panelPos);
        }

        /// <summary>
        /// uGUI-friendly variant. <paramref name="screenPos"/> is in actual screen
        /// pixels with bottom-left origin (Unity's <c>Input.mousePosition</c> /
        /// <c>PointerEventData.position</c>). Converted to panel coordinates
        /// inside the deferred positioning callback — under
        /// <see cref="PanelScaleMode.ScaleWithScreenSize"/> the panel uses
        /// reference pixels, so the cursor screen px ≠ panel px until divided
        /// by the active scale. The conversion has to happen *after* layout has
        /// caught up (when the tooltip is hidden, <c>_root.resolvedStyle.height</c>
        /// is 0 — no layout is computed for <c>display:None</c> elements).
        /// </summary>
        public void Show(TooltipModel model, Vector2 screenPos)
        {
            if (_root == null) return;
            if (model == null || model.IsEmpty)
            {
                Hide();
                return;
            }

            Populate(model);
            _root.style.display = DisplayStyle.Flex;
            _isVisible = true;

            _root.schedule.Execute(() =>
            {
                var panelPos = ScreenToPanel(screenPos);
                PositionCard(panelPos);
            }).StartingIn(0);
        }

        void ShowAtPanelPos(TooltipModel model, Vector2 panelPos)
        {
            if (_root == null) return;
            if (model == null || model.IsEmpty)
            {
                Hide();
                return;
            }

            Populate(model);
            _root.style.display = DisplayStyle.Flex;
            _isVisible = true;

            // Defer so the card has measured its size before edge-clamping.
            _root.schedule.Execute(() => PositionCard(panelPos)).StartingIn(0);

            // Re-position once geometry settles — first PositionCard pass sees
            // `_card.resolvedStyle.width = 0` (layout not yet computed), so right-edge
            // flip check misses. After GeometryChangedEvent we have real card width
            // and can flip correctly. Critical when tile sits near right edge
            // (e.g. status row у TR corner — без цього tooltip відплив би off-screen).
            if (_card != null) _card.RegisterCallback<GeometryChangedEvent>(OnCardGeometryChanged);
            _pendingPanelPos = panelPos;
        }

        Vector2 _pendingPanelPos;
        void OnCardGeometryChanged(GeometryChangedEvent _)
        {
            if (_card == null) return;
            _card.UnregisterCallback<GeometryChangedEvent>(OnCardGeometryChanged);
            if (_isVisible) PositionCard(_pendingPanelPos);
        }

        Vector2 ScreenToPanel(Vector2 screenPos)
        {
            float panelHeight = _root.resolvedStyle.height;
            if (panelHeight <= 0f) panelHeight = Screen.height;
            float scale = Screen.height > 0 ? Screen.height / panelHeight : 1f;
            return new Vector2(
                screenPos.x / scale,
                panelHeight - screenPos.y / scale);
        }

        public void Hide()
        {
            if (_root == null || !_isVisible) return;
            _root.style.display = DisplayStyle.None;
            _isVisible = false;
        }

        // ── Setup ─────────────────────────────────────────────

        void BuildDocument()
        {
            var tree    = Resources.Load<VisualTreeAsset>("UI/Tooltip/TooltipOverlay");
            var sheet   = Resources.Load<StyleSheet>("UI/Tooltip/TooltipOverlay");
            var panel   = Resources.Load<PanelSettings>("UI/Tooltip/TooltipPanelSettings");

            // Force responsive scale settings at runtime — keeps tooltip in
            // lockstep with the rest of the UI regardless of cached asset state.
            // See docs/ai/ui-styling.md "Resolution scaling".
            if (panel != null)
            {
                panel.scaleMode = PanelScaleMode.ScaleWithScreenSize;
                panel.referenceResolution = new Vector2Int(1920, 1080);
                panel.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
                panel.match = 0.5f;
            }

            if (tree == null || panel == null)
            {
                Debug.LogWarning("[Tooltip] Missing UXML / PanelSettings at Resources/UI/Tooltip/.");
                return;
            }

            _doc = gameObject.AddComponent<UIDocument>();
            _doc.panelSettings   = panel;
            _doc.visualTreeAsset = tree;

            _root = _doc.rootVisualElement;
            if (sheet != null && !_root.styleSheets.Contains(sheet))
                _root.styleSheets.Add(sheet);

            _root.style.flexGrow = 1;
            _root.style.position = Position.Absolute;
            _root.style.left = 0;
            _root.style.right = 0;
            _root.style.top = 0;
            _root.style.bottom = 0;
            _root.pickingMode = PickingMode.Ignore;

            _card        = _root.Q<VisualElement>("card");
            _title       = _root.Q<Label>("title");
            _subtitle    = _root.Q<Label>("subtitle");
            _description = _root.Q<Label>("description");
            _sections    = _root.Q<VisualElement>("sections");
        }

        void HideImmediate()
        {
            if (_root != null) _root.style.display = DisplayStyle.None;
            _isVisible = false;
        }

        // ── Render ────────────────────────────────────────────

        void Populate(TooltipModel model)
        {
            _title.text    = model.Title;
            _subtitle.text = model.Subtitle;
            ToggleEmptyClass(_subtitle, string.IsNullOrEmpty(model.Subtitle));

            _description.text = model.Description;
            ToggleEmptyClass(_description, string.IsNullOrEmpty(model.Description));

            _sections.Clear();
            for (int i = 0; i < model.Sections.Count; i++)
                _sections.Add(BuildSection(model.Sections[i]));
        }

        static VisualElement BuildSection(TooltipSection section)
        {
            var box = new VisualElement();
            box.AddToClassList("tt-section");

            var heading = new Label(section.Heading);
            heading.AddToClassList("tt-section-heading");
            ToggleEmptyClass(heading, string.IsNullOrEmpty(section.Heading));
            box.Add(heading);

            for (int i = 0; i < section.Rows.Count; i++)
            {
                var row = section.Rows[i];
                var rowEl = new VisualElement();
                rowEl.AddToClassList("tt-row");

                var label = new Label(row.Label);
                label.AddToClassList("tt-row-label");

                var value = new Label(row.Value);
                value.AddToClassList("tt-row-value");

                rowEl.Add(label);
                rowEl.Add(value);
                box.Add(rowEl);
            }

            return box;
        }

        static void ToggleEmptyClass(VisualElement el, bool empty)
        {
            const string cls = "is-empty";
            if (empty) el.AddToClassList(cls); else el.RemoveFromClassList(cls);
        }

        // ── Positioning ───────────────────────────────────────

        // panelPos is already in panel (reference) coordinates with top-left
        // origin. Both Show() (after screen→panel conversion) and ShowFromPanel()
        // funnel here so the math is single-sourced.
        void PositionCard(Vector2 panelPos)
        {
            if (_card == null) return;

            float panelHeight = _root.resolvedStyle.height;
            float panelWidth  = _root.resolvedStyle.width;
            if (panelHeight <= 0f) panelHeight = Screen.height;
            if (panelWidth  <= 0f) panelWidth  = Screen.width;

            float cardW = _card.resolvedStyle.width;
            float cardH = _card.resolvedStyle.height;

            float left = panelPos.x + CursorOffsetX;
            float top  = panelPos.y + CursorOffsetY;

            // Flip horizontally if the card would overflow the right edge.
            if (left + cardW > panelWidth - ViewportPadding)
                left = panelPos.x - cardW - CursorOffsetX;

            // Push up if it would overflow the bottom edge.
            if (top + cardH > panelHeight - ViewportPadding)
                top = panelHeight - cardH - ViewportPadding;

            if (left < ViewportPadding) left = ViewportPadding;
            if (top  < ViewportPadding) top  = ViewportPadding;

            _card.style.left = left;
            _card.style.top  = top;
        }
    }
}
