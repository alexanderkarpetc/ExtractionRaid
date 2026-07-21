using System.Collections.Generic;
using ApplicationCore;
using State;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using Prog = global::Progression;

namespace View.UI.Progression
{
    /// <summary>
    /// Runtime skill-tree screen. Reads the tree layout/content from
    /// <see cref="Prog.ProgressionTreeConfig"/> and the allocation state from
    /// <c>App.Instance.Player.Progression</c>. Allocation is permanent (no refund) and
    /// routed through <see cref="Prog.ProgressionSystem"/> — this view holds no rules.
    /// Connecting lines are drawn with <see cref="Painter2D"/>; the web pans/zooms.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    [RequireComponent(typeof(UIDocument))]
    public class ProgressionWindow : MonoBehaviour
    {
        public static ProgressionWindow Instance { get; private set; }

        // Logical layout space (matches the HTML concept). Content is transformed to fit.
        const float CX = 700f, CY = 700f, Space = 1400f;
        static readonly Color EdgeFaint = new(1f, 1f, 1f, 0.07f);

        struct Edge { public string A, B; public Color Color; }

        UIDocument _doc;
        VisualElement _root, _content, _edges, _nodesLayer, _canvasWrap;
        VisualElement _tip;
        Label _availLabel, _spentLabel, _tipType, _tipName, _tipDesc, _tipHook;

        Prog.ProgressionTreeConfig _cfg;
        readonly Dictionary<string, VisualElement> _nodeEls = new();
        readonly Dictionary<string, Color> _nodeColor = new();
        readonly Dictionary<string, Vector2> _pos = new();
        readonly List<Edge> _edgeList = new();

        static readonly Color NodeBaseBg = new(0.05f, 0.06f, 0.12f, 1f);
        static readonly Color NodeLockedBorder = new(0.4f, 0.44f, 0.5f);
        static readonly Color NodeOnGlyph = new(0.03f, 0.06f, 0.11f);
        static Texture2D _glowTex;   // radial-falloff glow, tinted per discipline

        bool _isVisible;
        bool _fitted;
        float _scale = 1f, _panX, _panY;
        bool _panning;

        public bool IsOpen => _isVisible;

        void Awake()
        {
            Instance = this;
            BuildDocument();
            if (_root == null) return;
            BuildTree();
            _root.style.display = DisplayStyle.None;
        }

        void OnDestroy() { if (Instance == this) Instance = null; }

        void BuildDocument()
        {
            var tree = Resources.Load<VisualTreeAsset>("UI/Progression/Progression");
            var styles = Resources.Load<StyleSheet>("UI/Progression/Progression");
            var panel = Resources.Load<PanelSettings>("UI/Progression/ProgressionPanelSettings");
            if (tree == null || panel == null)
            {
                Debug.LogError("[Progression] Missing UXML or PanelSettings in Resources/UI/Progression/.");
                return;
            }

            panel.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            panel.referenceResolution = new Vector2Int(1920, 1080);
            panel.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            panel.match = 0.5f;

            _doc = GetComponent<UIDocument>();
            _doc.panelSettings = panel;
            _doc.visualTreeAsset = tree;

            _root = _doc.rootVisualElement;
            if (styles != null && !_root.styleSheets.Contains(styles)) _root.styleSheets.Add(styles);
            _root.style.flexGrow = 1;

            _content = _root.Q<VisualElement>("content");
            _edges = _root.Q<VisualElement>("edges");
            _nodesLayer = _root.Q<VisualElement>("nodes");
            _canvasWrap = _root.Q<VisualElement>("canvasWrap");
            _tip = _root.Q<VisualElement>("tip");
            _availLabel = _root.Q<Label>("availLabel");
            _spentLabel = _root.Q<Label>("spentLabel");
            _tipType = _root.Q<Label>("tipType");
            _tipName = _root.Q<Label>("tipName");
            _tipDesc = _root.Q<Label>("tipDesc");
            _tipHook = _root.Q<Label>("tipHook");

            var closeBtn = _root.Q<Button>("closeBtn");
            if (closeBtn != null) closeBtn.clicked += Close;
            var dev = _root.Q<Button>("devGrantBtn");
            if (dev != null) dev.clicked += () => { GrantPoints(5); };

            _edges.generateVisualContent += OnGenerateEdges;
            _canvasWrap.RegisterCallback<WheelEvent>(OnWheel);
            _canvasWrap.RegisterCallback<PointerDownEvent>(OnCanvasPointerDown);
            _canvasWrap.RegisterCallback<PointerMoveEvent>(OnCanvasPointerMove);
            _canvasWrap.RegisterCallback<PointerUpEvent>(OnCanvasPointerUp);
            _canvasWrap.RegisterCallback<GeometryChangedEvent>(_ => FitOnce());
        }

        // ── build node/edge geometry once from the config ──────────
        void BuildTree()
        {
            _cfg = Prog.ProgressionTreeConfig.Instance;
            if (_cfg == null || _nodesLayer == null) return;

            _nodeEls.Clear(); _nodeColor.Clear(); _pos.Clear(); _edgeList.Clear();
            _nodesLayer.Clear();

            foreach (var disc in _cfg.Disciplines)
            {
                var hubId = "hub:" + disc.Id;
                var hubPos = PolarPos(disc.AngleDeg, _cfg.HubRadius);
                _pos[hubId] = hubPos;
                AddHub(disc.DisplayName, hubPos, disc.Color);

                for (int bi = 0; bi < disc.Branches.Count; bi++)
                {
                    var branch = disc.Branches[bi];
                    foreach (var node in branch.Nodes)
                    {
                        var p = NodePos(disc, bi, node);
                        _pos[node.Id] = p;
                        AddNode(disc, node, p);

                        var parents = Prog.ProgressionSystem.GetParents(branch, node);
                        if (parents.Count == 0)
                            _edgeList.Add(new Edge { A = hubId, B = node.Id, Color = disc.Color });
                        else
                            foreach (var par in parents)
                                _edgeList.Add(new Edge { A = par.Id, B = node.Id, Color = disc.Color });
                    }
                }
            }
        }

        Vector2 PolarPos(float angleDeg, float radius)
        {
            float a = angleDeg * Mathf.Deg2Rad;
            return new Vector2(CX + radius * Mathf.Cos(a), CY + radius * Mathf.Sin(a));
        }

        Vector2 NodePos(Prog.ProgressionDisciplineDef disc, int branchIndex, Prog.ProgressionNodeDef node)
        {
            float spread = (_cfg.BranchSpread != null && _cfg.BranchSpread.Length > 0)
                ? _cfg.BranchSpread[Mathf.Clamp(branchIndex, 0, _cfg.BranchSpread.Length - 1)] : 0f;
            float angle = disc.AngleDeg + spread + node.Offset * _cfg.ForkScale;
            float r = _cfg.RingBase + _cfg.RingStep * node.Ring;
            return PolarPos(angle, r);
        }

        static float NodeRadius(Prog.NodeSize size) => size switch
        {
            Prog.NodeSize.Keystone => 22f,
            Prog.NodeSize.Notable => 22f,
            _ => 15f,
        };

        void AddHub(string name, Vector2 pos, Color color)
        {
            const float radius = 26f;
            var el = new VisualElement { name = "hub", pickingMode = PickingMode.Ignore };
            el.AddToClassList("pr-hub");
            el.style.left = pos.x - radius; el.style.top = pos.y - radius;
            el.style.width = radius * 2; el.style.height = radius * 2;
            el.style.borderTopColor = el.style.borderBottomColor =
                el.style.borderLeftColor = el.style.borderRightColor = color;
            el.style.backgroundColor = Color.Lerp(NodeBaseBg, color, 0.22f);

            var label = new Label(name.ToUpperInvariant());
            label.AddToClassList("pr-hub-name");
            label.style.color = color;
            label.style.left = pos.x - 70; label.style.top = pos.y - radius - 22; label.style.width = 140;
            _nodesLayer.Add(el);
            _nodesLayer.Add(label);
        }

        void AddNode(Prog.ProgressionDisciplineDef disc, Prog.ProgressionNodeDef node, Vector2 pos)
        {
            float r = NodeRadius(node.Size);
            float wrap = r * 2f + 22f;   // extra room for the soft glow
            var el = new VisualElement { name = node.Id };
            el.AddToClassList("pr-node");
            if (node.Size == Prog.NodeSize.Keystone) el.AddToClassList("pr-node--keystone");
            el.style.left = pos.x - wrap / 2f; el.style.top = pos.y - wrap / 2f;
            el.style.width = wrap; el.style.height = wrap;
            _nodeColor[node.Id] = disc.Color;

            // Soft radial glow filling the wrapper (behind everything).
            var glow = new VisualElement { pickingMode = PickingMode.Ignore };
            glow.AddToClassList("pr-glow");
            glow.style.backgroundImage = new StyleBackground(GlowTexture());
            glow.style.width = wrap; glow.style.height = wrap;
            glow.style.display = DisplayStyle.None;
            el.Add(glow);

            // Crisp accent ring shown only on allocated nodes.
            var halo = new VisualElement { pickingMode = PickingMode.Ignore };
            halo.AddToClassList("pr-halo");
            float haloSize = r * 2f + 12f;
            float ho = (wrap - haloSize) / 2f;
            halo.style.left = ho; halo.style.top = ho; halo.style.width = haloSize; halo.style.height = haloSize;
            halo.style.display = DisplayStyle.None;
            el.Add(halo);

            var core = new VisualElement { pickingMode = PickingMode.Ignore };
            core.AddToClassList("pr-node-core");
            float coreOff = (wrap - r * 2f) / 2f;
            core.style.left = coreOff; core.style.top = coreOff;
            core.style.width = r * 2f; core.style.height = r * 2f;
            el.Add(core);

            var glyph = new Label(GlyphFor(node)) { pickingMode = PickingMode.Ignore };
            glyph.AddToClassList("pr-glyph");
            glyph.style.fontSize = node.Size == Prog.NodeSize.Minor ? 18 : 16;
            if (node.Size == Prog.NodeSize.Minor) glyph.style.translate = new Translate(0, -1, 0);
            core.Add(glyph);

            var id = node.Id;
            el.RegisterCallback<ClickEvent>(_ => OnNodeClicked(id));
            el.RegisterCallback<PointerEnterEvent>(e => ShowTip(disc, node, e.position));
            el.RegisterCallback<PointerMoveEvent>(e => MoveTip(e.position));
            el.RegisterCallback<PointerLeaveEvent>(_ => HideTip());
            _nodesLayer.Add(el);
            _nodeEls[node.Id] = el;

            if (!string.IsNullOrEmpty(node.DisplayName))
            {
                var name = new Label(node.DisplayName);
                name.AddToClassList("pr-name");
                name.style.left = pos.x - 70; name.style.top = pos.y + r + 6; name.style.width = 140;
                _nodesLayer.Add(name);
            }
        }

        // Radial white-to-transparent texture; tinted per discipline for a soft glow.
        static Texture2D GlowTexture()
        {
            if (_glowTex != null) return _glowTex;
            const int s = 64;
            var t = new Texture2D(s, s, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "pr-glow" };
            var px = new Color32[s * s];
            float c = (s - 1) / 2f;
            for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / c; // 0 at center
                    float a = Mathf.Clamp01(1f - d);
                    a *= a;   // soft falloff
                    px[y * s + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            t.SetPixels32(px); t.Apply();
            return _glowTex = t;
        }

        static string GlyphFor(Prog.ProgressionNodeDef node) => node.Size switch
        {
            Prog.NodeSize.Keystone => "◆",   // ◆
            Prog.NodeSize.Notable => "●",    // ●
            _ => node.Magnitude < 0 ? "−" : "+",
        };

        // ── allocation ─────────────────────────────────────────────
        void OnNodeClicked(string id)
        {
            var state = ProgressionState;
            if (state == null || _cfg == null) return;
            if (Prog.ProgressionSystem.Allocate(_cfg, state, id))
                Refresh();
        }

        PlayerProgressionState ProgressionState => App.Instance?.Player?.Progression;

        void GrantPoints(int n)
        {
            var state = ProgressionState;
            if (state == null) return;
            state.AvailablePoints += n;
            Refresh();
        }

        // ── refresh visual state ───────────────────────────────────
        void Refresh()
        {
            if (_cfg == null) return;
            var state = ProgressionState;

            foreach (var disc in _cfg.Disciplines)
                for (int bi = 0; bi < disc.Branches.Count; bi++)
                {
                    var branch = disc.Branches[bi];
                    foreach (var node in branch.Nodes)
                    {
                        if (!_nodeEls.TryGetValue(node.Id, out var el)) continue;
                        var core = el.Q<VisualElement>(className: "pr-node-core");
                        if (core == null) continue;
                        var glow = el.Q<VisualElement>(className: "pr-glow");
                        var halo = el.Q<VisualElement>(className: "pr-halo");
                        Color accent = _nodeColor.TryGetValue(node.Id, out var c) ? c : Color.white;
                        bool on = state != null && Prog.ProgressionSystem.IsAllocated(state, node.Id);
                        bool open = !on && state != null && Prog.ProgressionSystem.CanAllocate(_cfg, state, node.Id);

                        if (on)
                        {
                            core.style.backgroundColor = accent;
                            SetBorder(core, Color.white);
                            core.style.opacity = 1f;
                            ShowGlow(glow, accent, 0.9f);
                            ShowHalo(halo, accent, 0.9f);
                            SetGlyphColor(el, NodeOnGlyph);
                        }
                        else if (open)
                        {
                            core.style.backgroundColor = NodeBaseBg;
                            SetBorder(core, accent);
                            core.style.opacity = 1f;
                            ShowGlow(glow, accent, 0.5f);
                            HideEl(halo);
                            SetGlyphColor(el, accent);
                        }
                        else
                        {
                            core.style.backgroundColor = NodeBaseBg;
                            SetBorder(core, NodeLockedBorder);
                            core.style.opacity = 1f;
                            HideEl(glow); HideEl(halo);
                            SetGlyphColor(el, NodeLockedBorder);
                        }
                    }
                }

            _edges?.MarkDirtyRepaint();

            int avail = state?.AvailablePoints ?? 0;
            int spent = state != null ? Prog.ProgressionSystem.SpentPoints(_cfg, state) : 0;
            if (_availLabel != null) _availLabel.text = avail.ToString();
            if (_spentLabel != null) _spentLabel.text = $"{spent} / {_cfg.NodeCount}";
        }

        // ── edges (Painter2D) ───────────────────────────────────────
        void OnGenerateEdges(MeshGenerationContext ctx)
        {
            if (_edgeList.Count == 0) return;
            var state = ProgressionState;
            var painter = ctx.painter2D;
            painter.lineCap = LineCap.Round;

            foreach (var e in _edgeList)
            {
                if (!_pos.TryGetValue(e.A, out var a) || !_pos.TryGetValue(e.B, out var b)) continue;
                bool live = IsOnOrFree(state, e.A) && IsOnOrFree(state, e.B);
                painter.strokeColor = live ? e.Color : EdgeFaint;
                painter.lineWidth = live ? 4f : 3f;
                painter.BeginPath();
                painter.MoveTo(a);
                painter.LineTo(b);
                painter.Stroke();
            }
        }

        static bool IsOnOrFree(PlayerProgressionState state, string id)
        {
            if (id == "core" || id.StartsWith("hub:")) return true;
            return state != null && state.AllocatedNodeIds.Contains(id);
        }

        static void SetBorder(VisualElement el, Color c)
        {
            el.style.borderTopColor = c; el.style.borderBottomColor = c;
            el.style.borderLeftColor = c; el.style.borderRightColor = c;
        }

        static void SetGlyphColor(VisualElement el, Color c)
        {
            var g = el.Q<Label>(className: "pr-glyph");
            if (g != null) g.style.color = c;
        }

        static void ShowGlow(VisualElement g, Color c, float alpha)
        {
            if (g == null) return;
            g.style.display = DisplayStyle.Flex;
            g.style.unityBackgroundImageTintColor = new Color(c.r, c.g, c.b, alpha);
        }

        static void ShowHalo(VisualElement h, Color c, float alpha)
        {
            if (h == null) return;
            h.style.display = DisplayStyle.Flex;
            SetBorder(h, new Color(c.r, c.g, c.b, alpha));
        }

        static void HideEl(VisualElement e) { if (e != null) e.style.display = DisplayStyle.None; }

        // ── tooltip ─────────────────────────────────────────────────
        void ShowTip(Prog.ProgressionDisciplineDef disc, Prog.ProgressionNodeDef node, Vector2 panelPos)
        {
            if (_tip == null) return;
            _tipType.text = $"{disc.DisplayName} · {node.Size.ToString().ToUpperInvariant()}";
            _tipType.style.color = disc.Color;
            _tipName.text = string.IsNullOrEmpty(node.DisplayName) ? node.StatLabel : node.DisplayName;
            if (!string.IsNullOrEmpty(node.Description))
                _tipDesc.text = node.Description;
            else
            {
                string sign = node.Magnitude > 0 ? "+" : "";
                _tipDesc.text = $"{sign}{node.Magnitude:0.##}{node.Unit}  {node.StatLabel}";
            }
            _tipHook.text = string.IsNullOrEmpty(node.DevHook) ? "" : "hook → " + node.DevHook;
            _tip.style.display = DisplayStyle.Flex;
            MoveTip(panelPos);
        }

        void MoveTip(Vector2 panelPos)
        {
            if (_tip == null || _tip.style.display == DisplayStyle.None) return;
            var local = _root.WorldToLocal(panelPos);
            var r = _root.contentRect;
            const float w = 380f, h = 170f;
            float x = local.x + 16, y = local.y + 16;
            if (x + w > r.width) x = local.x - w - 16;
            if (y + h > r.height) y = Mathf.Max(8f, r.height - h - 8f);
            _tip.style.left = x;
            _tip.style.top = y;
        }

        void HideTip() { if (_tip != null) _tip.style.display = DisplayStyle.None; }

        // ── pan / zoom ──────────────────────────────────────────────
        void FitOnce()
        {
            if (_fitted) return;
            var r = _canvasWrap.contentRect;
            if (r.width <= 1 || r.height <= 1) return;
            _scale = Mathf.Min(r.width, r.height) / Space * 0.92f;
            _panX = (r.width - Space * _scale) * 0.5f;
            _panY = (r.height - Space * _scale) * 0.5f;
            _fitted = true;
            ApplyTransform();
        }

        void ApplyTransform()
        {
            if (_content == null) return;
            _content.style.translate = new Translate(_panX, _panY, 0);
            _content.style.scale = new Scale(new Vector2(_scale, _scale));
        }

        void OnWheel(WheelEvent evt)
        {
            var p = _canvasWrap.WorldToLocal(evt.mousePosition);
            float factor = evt.delta.y > 0 ? 1f / 1.12f : 1.12f;
            float newScale = Mathf.Clamp(_scale * factor, 0.3f, 2.6f);
            _panX = p.x - (p.x - _panX) * (newScale / _scale);
            _panY = p.y - (p.y - _panY) * (newScale / _scale);
            _scale = newScale;
            ApplyTransform();
            evt.StopPropagation();
        }

        void OnCanvasPointerDown(PointerDownEvent evt)
        {
            var t = evt.target as VisualElement;
            while (t != null && t != _canvasWrap) { if (t.ClassListContains("pr-node")) return; t = t.parent; }
            _panning = true;
            _canvasWrap.CapturePointer(evt.pointerId);
        }

        void OnCanvasPointerMove(PointerMoveEvent evt)
        {
            if (!_panning) return;
            _panX += evt.deltaPosition.x;
            _panY += evt.deltaPosition.y;
            ApplyTransform();
        }

        void OnCanvasPointerUp(PointerUpEvent evt)
        {
            if (!_panning) return;
            _panning = false;
            if (_canvasWrap.HasPointerCapture(evt.pointerId)) _canvasWrap.ReleasePointer(evt.pointerId);
        }

        // ── open / close ────────────────────────────────────────────
        public void Toggle() { if (_isVisible) Close(); else Open(); }

        public void Open()
        {
            if (_root == null) return;
            _isVisible = true;
            _root.style.display = DisplayStyle.Flex;
            App.Instance?.SetGameplayInputBlocked(true);
            Refresh();
            _fitted = false;
            _root.schedule.Execute(FitOnce).StartingIn(0);
        }

        public void Close()
        {
            if (_root == null) return;
            _isVisible = false;
            _root.style.display = DisplayStyle.None;
            HideTip();
            App.Instance?.SetGameplayInputBlocked(false);
        }

        void Update()
        {
            if (!_isVisible) return;
            var kb = Keyboard.current;
            if (kb != null && kb[Key.Escape].wasPressedThisFrame) Close();
        }
    }

}
