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
    /// Coordinates: <see cref="Show"/> takes a screen-space position with origin at
    /// the bottom-left (Unity's standard for <c>Input.mousePosition</c> /
    /// <c>PointerEventData.position</c>) and flips Y internally for the UI Toolkit
    /// panel which uses top-left origin.
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
        /// UI-Toolkit-friendly variant. Takes a position in panel coordinates
        /// (top-left origin — what <c>PointerEnterEvent.position</c> reports) and
        /// flips Y internally to the bottom-left form <see cref="Show"/> expects.
        /// </summary>
        public void ShowFromPanel(TooltipModel model, Vector2 panelPos)
        {
            Show(model, new Vector2(panelPos.x, Screen.height - panelPos.y));
        }

        /// <summary>
        /// Show the tooltip at <paramref name="screenPos"/> (bottom-left origin).
        /// No-op when <paramref name="model"/> is null/empty.
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

            // Defer position so the card has measured its size before clamping.
            _root.schedule.Execute(() => PositionCard(screenPos)).StartingIn(0);
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

            if (tree == null || panel == null)
            {
                Debug.LogWarning("[Tooltip] Missing UXML / PanelSettings at Resources/UI/Tooltip/. " +
                                 "Domain reload should have created them via TooltipAssetsBootstrap.");
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

            _card     = _root.Q<VisualElement>("card");
            _title    = _root.Q<Label>("title");
            _subtitle = _root.Q<Label>("subtitle");
            _sections = _root.Q<VisualElement>("sections");
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

        void PositionCard(Vector2 screenPos)
        {
            if (_card == null) return;

            // Convert from bottom-left screen origin to UI Toolkit's top-left.
            float panelHeight = _root.resolvedStyle.height;
            if (panelHeight <= 0f) panelHeight = Screen.height;

            float panelWidth = _root.resolvedStyle.width;
            if (panelWidth <= 0f) panelWidth = Screen.width;

            float cardW = _card.resolvedStyle.width;
            float cardH = _card.resolvedStyle.height;

            float left = screenPos.x + CursorOffsetX;
            float top  = (panelHeight - screenPos.y) + CursorOffsetY;

            // Flip horizontally if the card would overflow the right edge.
            if (left + cardW > panelWidth - ViewportPadding)
                left = screenPos.x - cardW - CursorOffsetX;

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
