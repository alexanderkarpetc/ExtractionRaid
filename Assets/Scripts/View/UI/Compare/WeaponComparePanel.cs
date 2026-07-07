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
        public void Show(ItemState hovered, string hoveredTag, ItemState baseline, string baselineTag,
                         bool hasMore, Vector2 panelPos)
        {
            if (_root == null || hovered == null || !hovered.HasWeaponConfiguration
                || baseline == null || !baseline.HasWeaponConfiguration)
            {
                Hide();
                return;
            }

            Populate(hovered, hoveredTag, baseline, baselineTag, hasMore);
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

        void Populate(ItemState hovered, string hoveredTag, ItemState baseline, string baselineTag, bool hasMore)
        {
            var reg = App.Instance?.CoreDefinitions;
            _cols.Clear();

            var hStats = Compose(hovered.WeaponConfiguration, reg);
            var bStats = Compose(baseline.WeaponConfiguration, reg);
            if (hStats == null || bStats == null) { Hide(); return; }

            var hRows = WeaponStatDisplay.Build(hStats.Value);
            var bRows = WeaponStatDisplay.Build(bStats.Value);
            var diff  = WeaponStatComparison.Build(hRows, bRows);

            _cols.Add(BuildHoveredColumn(hovered, hoveredTag, reg, diff));
            _cols.Add(BuildBaselineColumn(baseline, baselineTag, reg, bRows));

            _footer.text = hasMore ? "Hold Alt — compare vs other weapon" : string.Empty;
            _footer.style.display = hasMore ? DisplayStyle.Flex : DisplayStyle.None;
        }

        VisualElement BuildHoveredColumn(ItemState item, string tag, ICoreDefinitionRegistry reg,
            System.Collections.Generic.IReadOnlyList<WeaponStatComparison.Row> diff)
        {
            var col = new VisualElement();
            col.AddToClassList("wc-col");
            AppendHeader(col, item, reg, tag);

            for (int i = 0; i < diff.Count; i++)
                col.Add(BuildCompareRow(diff[i]));
            AppendLoadout(col, item, reg);
            return col;
        }

        VisualElement BuildBaselineColumn(ItemState item, string tag, ICoreDefinitionRegistry reg,
            System.Collections.Generic.IReadOnlyList<WeaponStatDisplay.StatDisplayRow> rows)
        {
            var col = new VisualElement();
            col.AddToClassList("wc-col");
            col.AddToClassList("wc-col--baseline");
            AppendHeader(col, item, reg, tag);

            for (int i = 0; i < rows.Count; i++)
                col.Add(BuildPlainRow(rows[i]));
            AppendLoadout(col, item, reg);
            return col;
        }

        // Non-stat loadout footer per column: ammo type + player reserve (red when 0) + installed mods.
        void AppendLoadout(VisualElement col, ItemState item, ICoreDefinitionRegistry reg)
        {
            var s = WeaponLoadoutSummary.Build(item, reg, App.Instance?.Player?.Inventory, LoadedRounds(item));

            var div = new VisualElement();
            div.AddToClassList("wc-divider");
            col.Add(div);

            // Ammo row.
            var ammoRow = new VisualElement();
            ammoRow.AddToClassList("wc-row");
            var ammoLabel = new Label("Ammo");
            ammoLabel.AddToClassList("wc-label");
            ammoRow.Add(ammoLabel);
            var ammoName = new Label(string.IsNullOrEmpty(s.AmmoName) ? "—" : s.AmmoName);
            ammoName.AddToClassList("wc-value");
            ammoRow.Add(ammoName);
            // Loaded / reserve (like the HUD ammo counter); red only when the weapon truly
            // can't fire — nothing chambered AND no reserve.
            var reserve = new Label(s.AmmoLoaded + " / " + s.AmmoReserve);
            reserve.AddToClassList("wc-reserve");
            if (s.AmmoLoaded + s.AmmoReserve <= 0) reserve.AddToClassList("wc-reserve--empty");
            ammoRow.Add(reserve);
            col.Add(ammoRow);

            // Mods row.
            var modsRow = new VisualElement();
            modsRow.AddToClassList("wc-mods-row");
            var modsLabel = new Label("Mods");
            modsLabel.AddToClassList("wc-label");
            modsRow.Add(modsLabel);
            var chips = new VisualElement();
            chips.AddToClassList("wc-mods");
            if (s.Mods.Count == 0)
            {
                var none = new Label("None");
                none.AddToClassList("wc-none");
                chips.Add(none);
            }
            else
            {
                for (int i = 0; i < s.Mods.Count; i++)
                {
                    var chip = new Label($"{s.Mods[i].Slot} · {s.Mods[i].Name}");
                    chip.AddToClassList("wc-mod-chip");
                    chips.Add(chip);
                }
            }
            modsRow.Add(chips);
            col.Add(modsRow);
        }

        // Live rounds in the weapon's magazine: for an equipped weapon read the runtime Hotbar
        // entry (the config value goes stale as you fire); otherwise the config's stored count.
        static int LoadedRounds(ItemState weapon)
        {
            if (weapon == null || !weapon.HasWeaponConfiguration) return 0;
            var inv = App.Instance?.Player?.Inventory;
            var entity = App.Instance?.RaidSession?.RaidState?.PlayerEntity;
            if (inv != null && entity?.Hotbar != null)
            {
                for (int i = 0; i < inv.WeaponSlots.Length && i < entity.Hotbar.Length; i++)
                {
                    if (!ReferenceEquals(inv.WeaponSlots[i], weapon)) continue;
                    var rt = entity.Hotbar[i];
                    if (rt != null) return rt.AmmoInMagazine; // live magazine
                    break;
                }
            }
            return weapon.WeaponConfiguration.AmmoInMagazine; // loot / backpack (not equipped)
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

        // Hovered column. Bar stats are stacked (value + Δ header over a full-width comparison
        // bar); value-only stats (Headshot/Magazine) stay a single line.
        VisualElement BuildCompareRow(WeaponStatComparison.Row r)
        {
            if (!r.HasBar)
            {
                var row = new VisualElement();
                row.AddToClassList("wc-statrow");
                row.Add(StatHead(r.Label, r.Value, DeltaChip(r)));
                return row;
            }

            var sr = new VisualElement();
            sr.AddToClassList("wc-statrow");
            sr.Add(StatHead(r.Label, r.Value, DeltaChip(r)));
            sr.Add(ComparisonBar(r.HoveredBar, r.BaselineBar, r.Improved));
            return sr;
        }

        // Baseline column — same layout, no deltas (it's the reference).
        VisualElement BuildPlainRow(WeaponStatDisplay.StatDisplayRow r)
        {
            if (!r.HasBar)
            {
                var row = new VisualElement();
                row.AddToClassList("wc-statrow");
                row.Add(StatHead(r.Label, r.Value, null));
                return row;
            }

            var sr = new VisualElement();
            sr.AddToClassList("wc-statrow");
            sr.Add(StatHead(r.Label, r.Value, null));
            sr.Add(PlainBar(r.BarRatio01));
            return sr;
        }

        // Header line for a stacked stat row: label (left) + value + optional delta chip (right).
        static VisualElement StatHead(string label, string value, VisualElement delta)
        {
            var head = new VisualElement();
            head.AddToClassList("wc-stat-head");

            var lbl = new Label(label);
            lbl.AddToClassList("wc-stat-label");
            head.Add(lbl);

            var val = new Label(value);
            val.AddToClassList("wc-stat-value");
            head.Add(val);

            if (delta != null) head.Add(delta);
            return head;
        }

        // Comparison bar: gold fill up to min(hovered, baseline), then a green (improved) /
        // red (worse) segment spanning the difference.
        static VisualElement ComparisonBar(float hoveredBar, float baselineBar, bool improved)
        {
            float lo = Mathf.Min(hoveredBar, baselineBar);
            float hi = Mathf.Max(hoveredBar, baselineBar);

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
                delta.AddToClassList(improved ? "wc-bar-delta--up" : "wc-bar-delta--down");
                delta.style.width = new Length(Mathf.Clamp01(hi - lo) * 100f, LengthUnit.Percent);
                bar.Add(delta);
            }
            return bar;
        }

        static VisualElement PlainBar(float ratio)
        {
            var bar = new VisualElement();
            bar.AddToClassList("wc-bar");
            var fill = new VisualElement();
            fill.AddToClassList("wc-bar-fill");
            fill.style.width = new Length(Mathf.Clamp01(ratio) * 100f, LengthUnit.Percent);
            bar.Add(fill);
            return bar;
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
