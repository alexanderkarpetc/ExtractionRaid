using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace View.UI.Controls
{
    /// <summary>
    /// Top-right "Controls" legend. UI Toolkit port of
    /// Assets/Concepts/controls-panel-concept.html: a small always-visible pill
    /// (<c>[O] Controls</c>) that expands into a grouped keybinding list when the
    /// player presses <see cref="Key.O"/> or clicks the pill. Esc / the ✕ / O
    /// again collapses it.
    ///
    /// Passive HUD element — holds no game logic. The keybinding list is static
    /// data mirroring the real bindings in <c>UnityInputAdapter</c> and the UI
    /// presenters; update <see cref="Groups"/> if those change.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    [RequireComponent(typeof(UIDocument))]
    public class ControlsOverlay : MonoBehaviour
    {
        public static ControlsOverlay Instance { get; private set; }

        const int FadeOutMs = 180;

        UIDocument _doc;
        VisualElement _root;
        VisualElement _anchor;
        Button _toggle;
        Button _closeBtn;
        VisualElement _panel;
        ScrollView _body;

        bool _open;

        public bool IsOpen => _open;

        // ── Drag-to-scroll state ──
        const float DragThreshold = 4f;
        int _scrollPointerId = -1;
        float _dragStartY;
        float _dragStartOffsetY;
        bool _scrollDragging;

        // ── Keybinding data (mirrors UnityInputAdapter + presenters) ──
        readonly struct Binding
        {
            public readonly string Desc;
            public readonly string[] Keys;
            public Binding(string desc, params string[] keys) { Desc = desc; Keys = keys; }
        }

        readonly struct Group
        {
            public readonly string Title;
            public readonly Binding[] Rows;
            public Group(string title, Binding[] rows) { Title = title; Rows = rows; }
        }

        static readonly Group[] Groups =
        {
            new Group("Movement", new[]
            {
                new Binding("Move", "W", "A", "S", "D"),
                new Binding("Sprint", "Shift"),
                new Binding("Dodge / Roll", "Space"),
            }),
            new Group("Combat", new[]
            {
                new Binding("Fire", "LMB"),
                new Binding("Aim", "RMB"),
                new Binding("Reload", "R"),
                new Binding("Throw grenade", "G"),
            }),
            new Group("Items", new[]
            {
                new Binding("Weapon slots", "1", "2"),
                new Binding("Quick slots", "3", "…", "9"),
                new Binding("Pick up / loot", "F"),
                new Binding("Interact", "E"),
            }),
            new Group("Interface", new[]
            {
                new Binding("Inventory", "Tab"),
                new Binding("Quests", "I"),
                new Binding("Field notes", "N"),
                new Binding("Map (hold)", "M"),
                new Binding("Controls", "O"),
            }),
        };

        void Awake()
        {
            Instance = this;
            BuildDocument();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void Update()
        {
            var kb = Keyboard.current;
            if (kb == null) return;

            if (kb[Key.O].wasPressedThisFrame)
                SetOpen(!_open);
            else if (_open && kb[Key.Escape].wasPressedThisFrame)
                SetOpen(false);
        }

        // ── Build ─────────────────────────────────────────────

        void BuildDocument()
        {
            var tree = Resources.Load<VisualTreeAsset>("UI/Controls/ControlsOverlay");
            var styles = Resources.Load<StyleSheet>("UI/Controls/ControlsOverlay");
            var panel = Resources.Load<PanelSettings>("UI/Controls/ControlsPanelSettings");

            if (tree == null || panel == null)
            {
                Debug.LogError("[ControlsOverlay] Missing UXML or PanelSettings in Resources/UI/Controls/.");
                return;
            }

            // Re-apply scale config in code — Unity caches PanelSettings asset edits
            // unreliably across domain reloads (renders tiny on 4K otherwise).
            // Mirrors InventoryWindow / docs/ai/ui-styling.md.
            panel.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            panel.referenceResolution = new Vector2Int(1920, 1080);
            panel.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            panel.match = 0.5f;

            _doc = GetComponent<UIDocument>();
            _doc.panelSettings = panel;
            _doc.visualTreeAsset = tree;

            _root = _doc.rootVisualElement;
            if (_root == null) return;

            // Attach in code too — resilient to the UXML <Style src> failing to
            // resolve on first import. Loud if it's genuinely missing.
            if (styles != null)
            {
                if (!_root.styleSheets.Contains(styles))
                    _root.styleSheets.Add(styles);
            }
            else
            {
                Debug.LogError("[ControlsOverlay] StyleSheet missing at " +
                               "Resources/UI/Controls/ControlsOverlay.uss — overlay will render unstyled. " +
                               "Reimport the asset.");
            }

            _root.style.flexGrow = 1;
            // Root ignores picking so it never eats gameplay clicks; the pill and
            // panel (children) still receive their own pointer events.
            _root.pickingMode = PickingMode.Ignore;

            _anchor   = _root.Q<VisualElement>("anchor");
            _toggle   = _root.Q<Button>("toggle");
            _closeBtn = _root.Q<Button>("closeBtn");
            _panel    = _root.Q<VisualElement>("panel");
            _body     = _root.Q<ScrollView>("body");

            if (_body != null)
            {
                // Never show a horizontal bar (content fits width); vertical only
                // when the list overflows. The thin themed thumb is styled in USS.
                _body.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
                _body.verticalScrollerVisibility = ScrollerVisibility.Auto;

                // Grab-and-pull drag scrolling (UI Toolkit has no desktop drag-scroll
                // by default). Registered on the content container so dragging the
                // scrollbar thumb — which lives outside it — isn't intercepted.
                var surface = _body.contentContainer;
                surface.RegisterCallback<PointerDownEvent>(OnScrollPointerDown);
                surface.RegisterCallback<PointerMoveEvent>(OnScrollPointerMove);
                surface.RegisterCallback<PointerUpEvent>(OnScrollPointerUp);
                surface.RegisterCallback<PointerCaptureOutEvent>(_ => EndScrollDrag());
            }

            if (_toggle != null) _toggle.clicked += () => SetOpen(!_open);
            if (_closeBtn != null) _closeBtn.clicked += () => SetOpen(false);

            BuildRows();

            // Start collapsed.
            if (_panel != null) _panel.style.display = DisplayStyle.None;
        }

        void BuildRows()
        {
            if (_body == null) return;
            _body.Clear();

            foreach (var group in Groups)
            {
                var label = new Label(group.Title);
                label.AddToClassList("co-group-label");
                _body.Add(label);

                foreach (var b in group.Rows)
                    _body.Add(BuildRow(b));
            }
        }

        static VisualElement BuildRow(Binding binding)
        {
            var row = new VisualElement();
            row.AddToClassList("co-row");

            var desc = new Label(binding.Desc);
            desc.AddToClassList("co-desc");
            row.Add(desc);

            var keys = new VisualElement();
            keys.AddToClassList("co-keys");
            for (int i = 0; i < binding.Keys.Length; i++)
            {
                var k = binding.Keys[i];
                if (k == "…")
                {
                    var sep = new Label("…");
                    sep.AddToClassList("co-sep");
                    keys.Add(sep);
                    continue;
                }

                // Insert a "/" separator between consecutive key caps (but not
                // around the "…" range marker).
                if (i > 0 && binding.Keys[i - 1] != "…")
                {
                    var slash = new Label("/");
                    slash.AddToClassList("co-sep");
                    keys.Add(slash);
                }

                var cap = new Label(k);
                cap.AddToClassList("co-kbd");
                keys.Add(cap);
            }
            row.Add(keys);

            return row;
        }

        // ── Drag-to-scroll ────────────────────────────────────

        void OnScrollPointerDown(PointerDownEvent evt)
        {
            if (evt.button != 0 || _body == null) return;
            _scrollPointerId = evt.pointerId;
            _dragStartY = evt.position.y;
            _dragStartOffsetY = _body.scrollOffset.y;
            _scrollDragging = false;
            _body.contentContainer.CapturePointer(evt.pointerId);
        }

        void OnScrollPointerMove(PointerMoveEvent evt)
        {
            if (_scrollPointerId != evt.pointerId || _body == null) return;
            if (!_body.contentContainer.HasPointerCapture(evt.pointerId)) return;

            float dy = evt.position.y - _dragStartY;
            if (!_scrollDragging)
            {
                if (Mathf.Abs(dy) < DragThreshold) return;
                _scrollDragging = true;
            }

            // Clamp to the scrollable range so the drag can't over-scroll.
            float max = Mathf.Max(0f,
                _body.contentContainer.layout.height - _body.contentViewport.layout.height);
            var off = _body.scrollOffset;
            off.y = Mathf.Clamp(_dragStartOffsetY - dy, 0f, max);
            _body.scrollOffset = off;
        }

        void OnScrollPointerUp(PointerUpEvent evt)
        {
            if (_scrollPointerId != evt.pointerId) return;
            if (_body != null && _body.contentContainer.HasPointerCapture(evt.pointerId))
                _body.contentContainer.ReleasePointer(evt.pointerId);
            EndScrollDrag();
        }

        void EndScrollDrag()
        {
            _scrollPointerId = -1;
            _scrollDragging = false;
        }

        // ── Open / close ──────────────────────────────────────

        void SetOpen(bool open)
        {
            if (_panel == null) return;
            _open = open;

            if (_anchor != null)
            {
                if (open) _anchor.AddToClassList("co-anchor--open");
                else _anchor.RemoveFromClassList("co-anchor--open");
            }
            if (_toggle != null)
            {
                if (open) _toggle.AddToClassList("co-toggle--active");
                else _toggle.RemoveFromClassList("co-toggle--active");
            }

            if (open)
            {
                _panel.style.display = DisplayStyle.Flex;
                // Defer the visible-class so the opacity/scale transition has a "from" state.
                _panel.schedule.Execute(() => _panel.AddToClassList("co-panel--open")).ExecuteLater(16);
            }
            else
            {
                _panel.RemoveFromClassList("co-panel--open");
                _panel.schedule.Execute(() =>
                {
                    if (!_open) _panel.style.display = DisplayStyle.None;
                }).ExecuteLater(FadeOutMs);
            }
        }
    }
}
