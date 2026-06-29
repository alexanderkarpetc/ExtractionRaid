using System.Globalization;
using ApplicationCore;
using Adapters;
using State;
using Systems;
using UnityEngine;
using UnityEngine.UIElements;

namespace View.UI.Compare
{
    /// <summary>
    /// Floating two-column weapon comparison shown on hover of a loot/inventory weapon (when a
    /// weapon is equipped): left = hovered weapon with comparison bars (gold base + green/red
    /// delta segment + numeric delta, like the attachment editor); right = the baseline weapon
    /// (the one in hand) plain. Baseline pick + flip live in <see cref="WeaponCompareTarget"/>;
    /// stat-diff in <see cref="WeaponStatComparison"/>. See docs/ai/weapon-comparison-research.md.
    ///
    /// View-singleton on AppBootstrap (same pattern as TooltipController). Reuses the Tooltip
    /// PanelSettings so coords match the inventory's PointerEnter panel position.
    /// </summary>
    [DefaultExecutionOrder(-90)]
    public class WeaponComparePanel : MonoBehaviour
    {
        public static WeaponComparePanel Instance { get; private set; }

        const float CursorOffsetX = 18f;
        const float CursorOffsetY = 18f;
        const float ViewportPadding = 8f;

        UIDocument _doc;
        VisualElement _root;
        VisualElement _card;
        VisualElement _cols;
        Label _footer;
        bool _visible;
        Vector2 _pendingPos;

        void Awake()
        {
            Instance = this;
            BuildDocument();
            HideImmediate();
        }

        void OnDestroy() { if (Instance == this) Instance = null; }

        public bool IsVisible => _visible;

        // ── Public API ────────────────────────────────────────

        /// <summary>
        /// Render the hovered weapon vs the baseline weapon at panel position
        /// <paramref name="panelPos"/> (PointerEnter coords). <paramref name="hasMore"/> shows
        /// the "Alt — vs other slot" hint when more than one equipped weapon exists.
        /// </summary>
        public void Show(ItemState hovered, ItemState baseline, bool hasMore, Vector2 panelPos)
        {
            if (_root == null || hovered == null || !hovered.HasWeaponConfiguration
                || baseline == null || !baseline.HasWeaponConfiguration)
            {
                Hide();
                return;
            }

            Populate(hovered, baseline, hasMore);
            _root.style.display = DisplayStyle.Flex;
            _visible = true;
            _pendingPos = panelPos;
            _root.schedule.Execute(() => PositionCard(_pendingPos)).StartingIn(0);
        }

        public void Hide()
        {
            if (_root == null || !_visible) return;
            _root.style.display = DisplayStyle.None;
            _visible = false;
        }

        // ── Setup ─────────────────────────────────────────────

        void BuildDocument()
        {
            var panel = Resources.Load<PanelSettings>("UI/Tooltip/TooltipPanelSettings");
            var sheet = Resources.Load<StyleSheet>("UI/Compare/WeaponCompare");
            if (panel == null)
            {
                Debug.LogWarning("[WeaponCompare] Missing PanelSettings at Resources/UI/Tooltip/.");
                return;
            }

            _doc = gameObject.AddComponent<UIDocument>();
            _doc.panelSettings = panel;
            _doc.sortingOrder = 1; // share the tooltip layer; sit just above it if both ever show

            _root = _doc.rootVisualElement;
            if (sheet != null && !_root.styleSheets.Contains(sheet)) _root.styleSheets.Add(sheet);

            _root.style.position = Position.Absolute;
            _root.style.left = 0; _root.style.right = 0; _root.style.top = 0; _root.style.bottom = 0;
            _root.pickingMode = PickingMode.Ignore;

            _card = new VisualElement();
            _card.AddToClassList("wc-card");
            _card.pickingMode = PickingMode.Ignore;
            _root.Add(_card);

            _cols = new VisualElement();
            _cols.AddToClassList("wc-cols");
            _card.Add(_cols);

            _footer = new Label();
            _footer.AddToClassList("wc-footer");
            _card.Add(_footer);
        }

        void HideImmediate()
        {
            if (_root != null) _root.style.display = DisplayStyle.None;
            _visible = false;
        }

        // ── Render ────────────────────────────────────────────

        void Populate(ItemState hovered, ItemState baseline, bool hasMore)
        {
            var reg = App.Instance?.CoreDefinitions;
            _cols.Clear();

            var hStats = Compose(hovered.WeaponConfiguration, reg);
            var bStats = Compose(baseline.WeaponConfiguration, reg);
            if (hStats == null || bStats == null) { Hide(); return; }

            var hRows = WeaponStatDisplay.Build(hStats.Value);
            var bRows = WeaponStatDisplay.Build(bStats.Value);
            var diff  = WeaponStatComparison.Build(hRows, bRows);

            _cols.Add(BuildHoveredColumn(hovered, reg, diff));
            _cols.Add(BuildBaselineColumn(baseline, reg, bRows));

            _footer.text = hasMore ? "Hold Alt — compare vs other weapon" : string.Empty;
            _footer.style.display = hasMore ? DisplayStyle.Flex : DisplayStyle.None;
        }

        VisualElement BuildHoveredColumn(ItemState item, ICoreDefinitionRegistry reg,
            System.Collections.Generic.IReadOnlyList<WeaponStatComparison.Row> diff)
        {
            var col = new VisualElement();
            col.AddToClassList("wc-col");
            AppendHeader(col, item, reg, tag: "LOOT");

            for (int i = 0; i < diff.Count; i++)
                col.Add(BuildCompareRow(diff[i]));
            return col;
        }

        VisualElement BuildBaselineColumn(ItemState item, ICoreDefinitionRegistry reg,
            System.Collections.Generic.IReadOnlyList<WeaponStatDisplay.StatDisplayRow> rows)
        {
            var col = new VisualElement();
            col.AddToClassList("wc-col");
            col.AddToClassList("wc-col--baseline");
            AppendHeader(col, item, reg, tag: "IN HAND");

            for (int i = 0; i < rows.Count; i++)
                col.Add(BuildPlainRow(rows[i]));
            return col;
        }

        void AppendHeader(VisualElement col, ItemState item, ICoreDefinitionRegistry reg, string tag)
        {
            var head = new VisualElement();
            head.AddToClassList("wc-head");

            var title = new Label(WeaponDisplayName.For(item, reg));
            title.AddToClassList("wc-title");
            var cfg = item.WeaponConfiguration;
            title.style.color = RarityVisuals.Color(cfg.Payload.Rarity);
            head.Add(title);

            var t = new Label(tag);
            t.AddToClassList("wc-tag");
            head.Add(t);
            col.Add(head);

            string subtitle = item.DefinitionId;
            if (reg != null
                && reg.TryGetPayload(cfg.Payload.DefinitionId, out var p) && p != null
                && reg.TryGetDelivery(cfg.Delivery.DefinitionId, out var d) && d != null)
                subtitle = $"{p.DisplayName} · {d.FormFactor}";
            var sub = new Label(subtitle);
            sub.AddToClassList("wc-subtitle");
            col.Add(sub);

            var div = new VisualElement();
            div.AddToClassList("wc-divider");
            col.Add(div);
        }

        VisualElement BuildCompareRow(WeaponStatComparison.Row r)
        {
            var row = new VisualElement();
            row.AddToClassList("wc-row");

            var label = new Label(r.Label);
            label.AddToClassList("wc-label");
            row.Add(label);

            if (r.HasBar)
            {
                float lo = Mathf.Min(r.HoveredBar, r.BaselineBar);
                float hi = Mathf.Max(r.HoveredBar, r.BaselineBar);

                var bar = new VisualElement();
                bar.AddToClassList("wc-bar");

                var fill = new VisualElement();
                fill.AddToClassList("wc-bar-fill");
                fill.style.width = new Length(Mathf.Clamp01(lo) * 100f, LengthUnit.Percent);
                bar.Add(fill);

                if (hi > lo + 1e-4f)
                {
                    var delta = new VisualElement();
                    delta.AddToClassList("wc-bar-delta");
                    delta.AddToClassList(r.Improved ? "wc-bar-delta--up" : "wc-bar-delta--down");
                    delta.style.width = new Length(Mathf.Clamp01(hi - lo) * 100f, LengthUnit.Percent);
                    bar.Add(delta);
                }
                row.Add(bar);
            }
            else
            {
                var value = new Label(r.Value);
                value.AddToClassList("wc-value");
                row.Add(value);
            }

            row.Add(DeltaChip(r));
            return row;
        }

        VisualElement BuildPlainRow(WeaponStatDisplay.StatDisplayRow r)
        {
            var row = new VisualElement();
            row.AddToClassList("wc-row");

            var label = new Label(r.Label);
            label.AddToClassList("wc-label");
            row.Add(label);

            if (r.HasBar)
            {
                var bar = new VisualElement();
                bar.AddToClassList("wc-bar");
                var fill = new VisualElement();
                fill.AddToClassList("wc-bar-fill");
                fill.style.width = new Length(Mathf.Clamp01(r.BarRatio01) * 100f, LengthUnit.Percent);
                bar.Add(fill);
                row.Add(bar);
            }
            else
            {
                var value = new Label(r.Value);
                value.AddToClassList("wc-value");
                row.Add(value);
            }
            return row;
        }

        // Signed delta chip (+N / −N), green=improvement / red=worse. Empty when unchanged.
        static Label DeltaChip(WeaponStatComparison.Row r)
        {
            var lbl = new Label();
            lbl.AddToClassList("wc-delta");
            if (r.Improved || r.Worsened)
            {
                lbl.text = (r.Delta > 0f ? "+" : "") + r.Delta.ToString("0.#", CultureInfo.InvariantCulture);
                lbl.AddToClassList(r.Improved ? "wc-delta--up" : "wc-delta--down");
            }
            return lbl;
        }

        static WeaponStats? Compose(WeaponConfiguration cfg, ICoreDefinitionRegistry reg)
        {
            if (reg != null && WeaponAssemblySystem.TryAssemble(cfg, reg, out var result, out _))
                return result.Stats;
            return null;
        }

        // ── Positioning (mirrors TooltipController) ───────────

        void PositionCard(Vector2 panelPos)
        {
            if (_card == null) return;
            float panelW = _root.resolvedStyle.width;
            float panelH = _root.resolvedStyle.height;
            if (panelW <= 0f) panelW = Screen.width;
            if (panelH <= 0f) panelH = Screen.height;

            float cardW = _card.resolvedStyle.width;
            float cardH = _card.resolvedStyle.height;

            float left = panelPos.x + CursorOffsetX;
            float top  = panelPos.y + CursorOffsetY;

            if (left + cardW > panelW - ViewportPadding) left = panelPos.x - cardW - CursorOffsetX;
            if (top + cardH > panelH - ViewportPadding)  top  = panelH - cardH - ViewportPadding;
            if (left < ViewportPadding) left = ViewportPadding;
            if (top  < ViewportPadding) top  = ViewportPadding;

            _card.style.left = left;
            _card.style.top  = top;
        }
    }
}
