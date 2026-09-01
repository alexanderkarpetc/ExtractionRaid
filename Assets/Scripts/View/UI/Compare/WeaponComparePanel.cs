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
    /// stat-diff in <see cref="WeaponStatComparison"/>. See docs/ai/weapon-builder/README.md.
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

        // Price / affordance context for the current hover — set by Show, read while building the
        // hovered column so it can match what the single tooltip would have said.
        LootableContainerState _shopContext;
        bool _hoveredIsInShop;
        bool _canModify;

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
        /// <summary>
        /// <paramref name="shopContext"/> / <paramref name="hoveredIsInShop"/> mirror the single
        /// tooltip's price handling: without them a weapon hovered at a vendor showed its generic
        /// Value, so the buy price was unreachable — the compare panel takes over from the tooltip
        /// whenever anything is equipped, which is nearly always.
        /// <paramref name="canModify"/> shows the same "right-click to modify" affordance the
        /// tooltip gives player-owned weapons.
        /// </summary>
        public void Show(ItemState hovered, string hoveredTag, ItemState baseline, string baselineTag,
                         bool hasMore, Vector2 panelPos,
                         LootableContainerState shopContext = null, bool hoveredIsInShop = false,
                         bool canModify = false)
        {
            if (_root == null || hovered == null || !hovered.HasWeaponConfiguration
                || baseline == null || !baseline.HasWeaponConfiguration)
            {
                Hide();
                return;
            }

            _shopContext     = shopContext;
            _hoveredIsInShop = hoveredIsInShop;
            _canModify       = canModify;

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

            // Both affordances can apply at once (your own spare gun with a second one equipped),
            // so they share the footer instead of one hiding the other.
            string hint = hasMore ? "Hold Alt — compare vs other weapon" : string.Empty;
            if (_canModify)
                hint = string.IsNullOrEmpty(hint) ? "Right-click to modify"
                                                  : hint + "   ·   Right-click to modify";
            _footer.text = hint;
            _footer.style.display = string.IsNullOrEmpty(hint) ? DisplayStyle.None : DisplayStyle.Flex;
        }

        VisualElement BuildHoveredColumn(ItemState item, string tag, ICoreDefinitionRegistry reg,
            System.Collections.Generic.IReadOnlyList<WeaponStatComparison.Row> diff)
        {
            var col = new VisualElement();
            col.AddToClassList("wc-col");
            AppendHeader(col, item, reg, tag);

            AppendChargeRow(col, item, reg);
            for (int i = 0; i < diff.Count; i++)
                col.Add(BuildCompareRow(diff[i]));
            AppendLoadout(col, item, reg, isHoveredColumn: true);
            return col;
        }

        VisualElement BuildBaselineColumn(ItemState item, string tag, ICoreDefinitionRegistry reg,
            System.Collections.Generic.IReadOnlyList<WeaponStatDisplay.StatDisplayRow> rows)
        {
            var col = new VisualElement();
            col.AddToClassList("wc-col");
            col.AddToClassList("wc-col--baseline");
            AppendHeader(col, item, reg, tag);

            AppendChargeRow(col, item, reg);
            for (int i = 0; i < rows.Count; i++)
                col.Add(BuildPlainRow(rows[i]));
            AppendLoadout(col, item, reg, isHoveredColumn: false);
            return col;
        }

        /// <summary>
        /// Charge time for Laser payloads — payload-specific cadence that has no place in the
        /// shared stat table, so <see cref="WeaponStatDisplay"/> never emits it. The weapon tooltip
        /// has always shown it; without this row the compare panel silently dropped the one number
        /// that separates a laser from a ballistic gun.
        /// </summary>
        void AppendChargeRow(VisualElement col, ItemState item, ICoreDefinitionRegistry reg)
        {
            if (reg == null) return;
            var cfg = item.WeaponConfiguration;
            if (!reg.TryGetPayload(cfg.Payload.DefinitionId, out var payload) || payload == null) return;
            if (!WeaponChargeResolver.RequiresChargeUp(payload)) return;

            float chargeTime = WeaponChargeResolver.GetChargeTime(payload, cfg.Payload.Rarity);

            var row = new VisualElement();
            row.AddToClassList("wc-row");
            var label = new Label("Charge");
            label.AddToClassList("wc-label");
            row.Add(label);
            var value = new Label(chargeTime.ToString("0.##", CultureInfo.InvariantCulture) + " s");
            value.AddToClassList("wc-value");
            row.Add(value);
            col.Add(row);
        }

        // Non-stat loadout footer per column: ammo type + player reserve (red when 0) + installed mods.
        void AppendLoadout(VisualElement col, ItemState item, ICoreDefinitionRegistry reg,
            bool isHoveredColumn)
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
            // Spelled out rather than "loaded / reserve": next to the Magazine stat a bare "20 / 23"
            // reads as mag/capacity, which is a different pair of numbers entirely.
            // Red only when the weapon truly can't fire — nothing chambered AND no reserve.
            var reserve = new Label($"{s.AmmoLoaded} in mag · {s.AmmoReserve} spare");
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

            // Value row — the gun's worth (sum of its parts, via ShopSystem). Matches the
            // "Value" line other item tooltips show.
            var valueRow = new VisualElement();
            valueRow.AddToClassList("wc-row");
            ResolvePrice(item, isHoveredColumn, out string priceLabel, out int price);
            var valueLabel = new Label(priceLabel);
            valueLabel.AddToClassList("wc-label");
            valueRow.Add(valueLabel);
            var valueVal = new Label(price + "¢");
            valueVal.AddToClassList("wc-value");
            valueRow.Add(valueVal);
            col.Add(valueRow);
        }

        /// <summary>
        /// Same three cases the single tooltip has: Buy for the vendor's own stock, Sell for your
        /// gear while a vendor is in reach, plain Value otherwise. Only the hovered column can be
        /// the shop's item — the baseline is always something you already own.
        /// </summary>
        void ResolvePrice(ItemState item, bool isHoveredColumn, out string label, out int price)
        {
            bool shopOpen = _shopContext != null && _shopContext.IsShop;
            if (shopOpen && isHoveredColumn && _hoveredIsInShop)
            {
                label = "Buy";
                price = ShopSystem.GetBuyPrice(_shopContext, item);
                return;
            }
            if (shopOpen)
            {
                label = "Sell";
                price = ShopSystem.GetSellPrice(_shopContext, item);
                return;
            }
            label = "Value";
            price = ShopSystem.GetGlobalSellPrice(item);
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
            // Highest of the two cores: tinting by payload alone read a Common receiver with a
            // Rare barrel as an ordinary grey gun.
            var topRarity = cfg.Delivery.Rarity > cfg.Payload.Rarity
                ? cfg.Delivery.Rarity
                : cfg.Payload.Rarity;
            title.style.color = RarityVisuals.Color(topRarity);
            head.Add(title);

            var t = new Label(tag);
            t.AddToClassList("wc-tag");
            head.Add(t);
            col.Add(head);

            // Per-core rarity, spelled out and tinted exactly like the weapon tooltip does. Without
            // it the panel dropped the single most important fact about a looted gun — and with
            // per-module rarity rolls the two cores routinely differ.
            string subtitle = item.DefinitionId;
            if (reg != null
                && reg.TryGetPayload(cfg.Payload.DefinitionId, out var p) && p != null
                && reg.TryGetDelivery(cfg.Delivery.DefinitionId, out var d) && d != null)
            {
                var pr = cfg.Payload.Rarity;
                var dr = cfg.Delivery.Rarity;
                subtitle = $"<color={RarityVisuals.Hex(pr)}>{p.DisplayName} ({pr})</color>"
                           + " · "
                           + $"<color={RarityVisuals.Hex(dr)}>{d.FormFactor} ({dr})</color>";
            }
            var sub = new Label(subtitle) { enableRichText = true };
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
