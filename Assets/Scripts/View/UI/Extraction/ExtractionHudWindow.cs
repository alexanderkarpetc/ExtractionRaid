using UnityEngine;
using UnityEngine.UIElements;

namespace View.UI.Extraction
{
    /// <summary>
    /// Single-widget extraction HUD. Three visual modes driven by class swaps:
    ///   <c>is-progress</c>   — cyan ring fills 0→100% from 12 o'clock, seconds remaining inside.
    ///   <c>is-interrupted</c> — red ring with "!" marker.
    ///   <c>is-complete</c>    — green ring at 100% with "✓" + "Returning to hideout…".
    /// The radial fill is drawn via <see cref="Painter2D"/> in <c>generateVisualContent</c>
    /// because USS does not (yet) support conic gradients. Caller drives all transitions
    /// through <see cref="Show"/> / <see cref="Hide"/>; no timer logic lives here.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    [RequireComponent(typeof(UIDocument))]
    public class ExtractionHudWindow : MonoBehaviour
    {
        public enum HudMode { Progress, Interrupted, Complete }

        public static ExtractionHudWindow Instance { get; private set; }

        // Ring palette — matches the HTML concept so visuals stay consistent.
        static readonly Color RingTrack       = new(1f, 1f, 1f, 0.06f);
        static readonly Color RingProgress    = new(0.53f, 0.90f, 1f, 1f);
        static readonly Color RingInterrupted = new(1f, 0.36f, 0.42f, 1f);
        static readonly Color RingComplete    = new(0.42f, 1f, 0.76f, 1f);
        const float RingThickness = 10f;

        UIDocument _doc;
        VisualElement _root;
        VisualElement _widget;
        VisualElement _ringCanvas;
        Label _seconds;
        Label _secondsSuffix;
        Label _stateLabel;
        Label _zoneName;
        Label _hint;

        HudMode _mode = HudMode.Progress;
        float _progress01;
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

        void BuildDocument()
        {
            var tree = Resources.Load<VisualTreeAsset>("UI/Extraction/ExtractionHud");
            var styles = Resources.Load<StyleSheet>("UI/Extraction/ExtractionHud");
            var panel = Resources.Load<PanelSettings>("UI/Extraction/ExtractionHudPanelSettings");

            if (tree == null || panel == null)
            {
                Debug.LogError("[ExtractionHud] Missing UXML or PanelSettings in Resources/UI/Extraction/.");
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

            _widget = _root.Q<VisualElement>("widget");
            _ringCanvas = _root.Q<VisualElement>("ringCanvas");
            _seconds = _root.Q<Label>("seconds");
            _secondsSuffix = _root.Q<Label>("secondsSuffix");
            _stateLabel = _root.Q<Label>("stateLabel");
            _zoneName = _root.Q<Label>("zoneName");
            _hint = _root.Q<Label>("hint");

            if (_ringCanvas != null)
                _ringCanvas.generateVisualContent += OnGenerateRing;
        }

        /// <summary>
        /// Show or update the widget. <paramref name="progress01"/> is 0..1 for the
        /// ring fill; <paramref name="zoneName"/> is the label under the state line.
        /// </summary>
        public void Show(HudMode mode, float progress01, float remainingSeconds, string zoneName)
        {
            if (_root == null) return;

            _mode = mode;
            _progress01 = Mathf.Clamp01(progress01);
            ApplyMode(mode);

            switch (mode)
            {
                case HudMode.Progress:
                    _seconds.text = Mathf.CeilToInt(remainingSeconds).ToString();
                    _secondsSuffix.text = "seconds";
                    _stateLabel.text = "Extracting";
                    _hint.text = "Hold position — leaving resets the timer.";
                    break;
                case HudMode.Interrupted:
                    _seconds.text = "!";
                    _secondsSuffix.text = "interrupted";
                    _stateLabel.text = "Extraction Failed";
                    _hint.text = "Step back inside the zone to retry.";
                    break;
                case HudMode.Complete:
                    _progress01 = 1f;
                    _seconds.text = "✓";
                    _secondsSuffix.text = "extracted";
                    _stateLabel.text = "Extraction Complete";
                    _hint.text = "Returning to hideout…";
                    break;
            }

            _zoneName.text = string.IsNullOrEmpty(zoneName) ? "" : zoneName;
            _ringCanvas?.MarkDirtyRepaint();

            _root.style.display = DisplayStyle.Flex;
            _isVisible = true;
        }

        public void Hide() => HideImmediate();

        public bool IsVisible => _isVisible;

        void HideImmediate()
        {
            if (_root != null) _root.style.display = DisplayStyle.None;
            _isVisible = false;
        }

        void ApplyMode(HudMode mode)
        {
            if (_widget == null) return;
            _widget.RemoveFromClassList("is-progress");
            _widget.RemoveFromClassList("is-interrupted");
            _widget.RemoveFromClassList("is-complete");
            _widget.AddToClassList(mode switch
            {
                HudMode.Interrupted => "is-interrupted",
                HudMode.Complete    => "is-complete",
                _                   => "is-progress",
            });
        }

        // Painter2D ring — drawn from 12 o'clock clockwise, sweeping the configured arc.
        // Uses the canvas element's resolved rect so it scales with USS sizing.
        void OnGenerateRing(MeshGenerationContext ctx)
        {
            var rect = _ringCanvas.contentRect;
            if (rect.width <= 0 || rect.height <= 0) return;

            float size = Mathf.Min(rect.width, rect.height);
            var center = new Vector2(rect.width * 0.5f, rect.height * 0.5f);
            float radius = size * 0.5f - RingThickness * 0.5f;

            var painter = ctx.painter2D;
            painter.lineWidth = RingThickness;
            painter.lineCap = LineCap.Butt;

            // Track (background arc — full circle, faint).
            painter.strokeColor = RingTrack;
            painter.BeginPath();
            painter.Arc(center, radius, 0f, 360f);
            painter.Stroke();

            // Active arc — color & sweep depend on mode.
            float sweep = _progress01 * 360f;
            if (_mode == HudMode.Complete) sweep = 360f;
            if (_mode == HudMode.Interrupted) sweep = Mathf.Max(sweep, 6f); // tiny stub so the red is visible
            if (sweep <= 0f) return;

            painter.strokeColor = ColorFor(_mode);
            painter.BeginPath();
            // Painter2D.Arc starts at 0° = +X (3 o'clock) and sweeps CCW with positive angles.
            // Game-feel expectation is to start at 12 o'clock and grow clockwise — shift the
            // start by -90° and use negative sweep direction by swapping start/end.
            painter.Arc(center, radius, -90f, -90f + sweep);
            painter.Stroke();
        }

        static Color ColorFor(HudMode mode) => mode switch
        {
            HudMode.Interrupted => RingInterrupted,
            HudMode.Complete    => RingComplete,
            _                   => RingProgress,
        };
    }
}
