using System.Collections.Generic;
using System.Globalization;
using System.Text;
using ApplicationCore;
using State;
using Systems;
using UnityEngine;
using UnityEngine.UIElements;

namespace View.UI.Attachments
{
    /// <summary>
    /// Modal attachment editor (Variant A — two-pane). Edit an existing weapon's
    /// attachments from anywhere (Option B, docs/ai/weapon-builder/attachments/edit-access.md).
    /// View over <see cref="AttachmentEditorPresenter"/>: slots on the left (grouped under
    /// cores), the focused slot's compatible mods + a live stat readout (green/red delta) on
    /// the right. Content is built in C#; styling from Resources/UI/Attachments/AttachmentEditor.uss.
    ///
    /// View-singleton (same pattern as WeaponBuilderWindow / TooltipController). Created on
    /// AppBootstrap; reachable via <see cref="Instance"/>.Open(weaponItem).
    /// </summary>
    [DefaultExecutionOrder(-80)]
    public class AttachmentEditorWindow : MonoBehaviour
    {
        public static AttachmentEditorWindow Instance { get; private set; }

        UIDocument _doc;
        VisualElement _root;
        Label _title;
        VisualElement _cores;
        VisualElement _slotsHost;
        VisualElement _modsHost;
        Label _modStatus;
        VisualElement _statsHost;

        AttachmentEditorPresenter _presenter;
        AttachmentSlot _focusedSlot = AttachmentSlot.Optic;
        bool _visible;

        void Awake()
        {
            Instance = this;
            BuildDocument();
            HideImmediate();
        }

        void OnDestroy()
        {
            if (_presenter != null) _presenter.StateChanged -= OnStateChanged;
            if (Instance == this) Instance = null;
        }

        // ── Public API ────────────────────────────────────────

        public void Open(ItemState weapon)
        {
            if (_root == null || weapon == null || !weapon.HasWeaponConfiguration) return;

            if (_presenter == null)
            {
                var app = App.Instance;
                System.Func<EId> alloc = null;
                if (app != null) alloc = app.AllocateEId;
                _presenter = new AttachmentEditorPresenter(
                    app?.CoreDefinitions, app?.Player?.Inventory, alloc);
                _presenter.StateChanged += OnStateChanged;
            }
            _focusedSlot = AttachmentEditorPresenter.PayloadSlots[0];
            _presenter.Load(weapon);          // fires StateChanged → RebuildAll
            _root.style.display = DisplayStyle.Flex;
            _visible = true;
        }

        public void Close()
        {
            if (_root == null || !_visible) return;
            _root.style.display = DisplayStyle.None;
            _visible = false;
        }

        public bool IsOpen => _visible;

        // ── Document / skeleton ───────────────────────────────

        void BuildDocument()
        {
            var panel = Resources.Load<PanelSettings>("UI/WeaponBuilder/WeaponBuilderPanelSettings");
            var sheet = Resources.Load<StyleSheet>("UI/Attachments/AttachmentEditor");
            if (panel == null)
            {
                Debug.LogWarning("[AttachmentEditor] Missing PanelSettings at Resources/UI/WeaponBuilder/.");
                return;
            }

            _doc = gameObject.AddComponent<UIDocument>();
            _doc.panelSettings = panel;
            _doc.sortingOrder = 200; // above inventory / builder

            _root = _doc.rootVisualElement;
            if (sheet != null && !_root.styleSheets.Contains(sheet)) _root.styleSheets.Add(sheet);

            _root.style.position = Position.Absolute;
            _root.style.left = 0; _root.style.right = 0; _root.style.top = 0; _root.style.bottom = 0;

            var backdrop = new VisualElement();
            backdrop.AddToClassList("ae-backdrop");
            backdrop.RegisterCallback<PointerDownEvent>(_ => Close());
            _root.Add(backdrop);

            var window = new VisualElement();
            window.AddToClassList("ae-window");
            window.RegisterCallback<PointerDownEvent>(e => e.StopPropagation()); // clicks inside don't close
            backdrop.Add(window);

            // Header: title + core chips
            var header = new VisualElement();
            header.AddToClassList("ae-header");
            _title = new Label { text = string.Empty };
            _title.AddToClassList("ae-title");
            _cores = new VisualElement();
            _cores.AddToClassList("ae-cores");
            header.Add(_title);
            header.Add(_cores);
            window.Add(header);

            // Body: two panes
            var body = new VisualElement();
            body.AddToClassList("ae-body");

            var left = new VisualElement();
            left.AddToClassList("ae-pane");
            left.AddToClassList("ae-pane-left");
            _slotsHost = left;
            body.Add(left);

            var right = new VisualElement();
            right.AddToClassList("ae-pane");
            right.AddToClassList("ae-pane-right");
            _modsHost = new VisualElement();
            _modStatus = new Label { text = string.Empty };
            _modStatus.AddToClassList("ae-mod-status");
            _statsHost = new VisualElement();
            right.Add(_modsHost);
            right.Add(_modStatus);
            right.Add(_statsHost);
            body.Add(right);

            window.Add(body);

            // Footer: close
            var footer = new VisualElement();
            footer.AddToClassList("ae-footer");
            var close = new Button(Close) { text = "Close" };
            close.AddToClassList("ae-close-btn");
            footer.Add(close);
            window.Add(footer);
        }

        void HideImmediate()
        {
            if (_root != null) _root.style.display = DisplayStyle.None;
            _visible = false;
        }

        // ── Refresh ───────────────────────────────────────────

        void OnStateChanged()
        {
            // Bump inventory version so InventoryWindow re-binds its slots — otherwise the
            // mod-pips (and any name/icon) don't refresh after an install/remove, since the
            // editor only mutates the ItemState's config (not inventory.Version).
            var inv = App.Instance?.Player?.Inventory;
            if (inv != null) inv.Version++;
            RebuildAll();
        }

        void RebuildAll()
        {
            RefreshHeader();
            RefreshSlots();
            RefreshDetail();
        }

        void RefreshHeader()
        {
            if (_presenter == null || !_presenter.HasWeapon) { _title.text = string.Empty; _cores.Clear(); return; }
            var reg = App.Instance?.CoreDefinitions;
            _title.text = WeaponDisplayName.For(_presenter.Weapon, reg);

            _cores.Clear();
            var cfg = _presenter.Weapon.WeaponConfiguration;
            if (reg != null
                && reg.TryGetPayload(cfg.Payload.DefinitionId, out var pDef) && pDef != null
                && reg.TryGetDelivery(cfg.Delivery.DefinitionId, out var dDef) && dDef != null)
            {
                _cores.Add(CoreChip($"{pDef.DisplayName} · {cfg.Payload.Rarity}", RarityVisuals.Color(cfg.Payload.Rarity)));
                _cores.Add(CoreChip($"{dDef.FormFactor} · {cfg.Delivery.Rarity}", RarityVisuals.Color(cfg.Delivery.Rarity)));
            }
        }

        static Label CoreChip(string text, Color rarityColor)
        {
            var chip = new Label(text);
            chip.AddToClassList("ae-core-chip");
            chip.style.borderLeftColor = rarityColor;
            return chip;
        }

        void RefreshSlots()
        {
            _slotsHost.Clear();
            if (_presenter == null || !_presenter.HasWeapon) return;

            AppendSlotGroup("PAYLOAD", AttachmentEditorPresenter.PayloadSlots, first: true);
            AppendSlotGroup("DELIVERY", AttachmentEditorPresenter.DeliverySlots, first: false);
        }

        void AppendSlotGroup(string heading, AttachmentSlot[] slots, bool first)
        {
            var h = new Label(heading);
            h.AddToClassList("ae-section-heading");
            if (first) h.AddToClassList("first");
            _slotsHost.Add(h);

            foreach (var slot in slots)
            {
                var installed = _presenter.InstalledIn(slot);
                var row = new VisualElement();
                row.AddToClassList("ae-slot-row");
                if (slot == _focusedSlot) row.AddToClassList("ae-slot-row--focused");
                if (!installed.HasValue) row.AddToClassList("ae-slot-row--empty");

                var name = new Label(slot.ToString());
                name.AddToClassList("ae-slot-name");
                row.Add(name);

                var value = new Label();
                value.AddToClassList("ae-slot-value");
                if (installed.HasValue)
                {
                    value.text = ModDisplayName(installed.Value.DefinitionId);
                }
                else
                {
                    value.text = "empty";
                    value.AddToClassList("ae-slot-value--empty");
                }
                row.Add(value);

                var captured = slot;
                row.RegisterCallback<ClickEvent>(_ => FocusSlot(captured));
                _slotsHost.Add(row);
            }
        }

        void FocusSlot(AttachmentSlot slot)
        {
            _focusedSlot = slot;
            RefreshSlots();
            RefreshDetail();
        }

        void RefreshDetail()
        {
            _modsHost.Clear();
            if (_modStatus != null) _modStatus.text = string.Empty; // clear stale action feedback
            if (_presenter == null || !_presenter.HasWeapon) { _statsHost.Clear(); return; }

            var heading = new Label($"{_focusedSlot.ToString().ToUpperInvariant()} — CHOOSE A MOD");
            heading.AddToClassList("ae-section-heading");
            heading.AddToClassList("first");
            _modsHost.Add(heading);

            var mods = _presenter.CompatibleMods(_focusedSlot);
            var installed = _presenter.InstalledIn(_focusedSlot);

            if (mods.Count == 0)
            {
                var note = new Label("No compatible mods.");
                note.AddToClassList("ae-empty-note");
                _modsHost.Add(note);
            }
            else
            {
                for (int i = 0; i < mods.Count; i++)
                    _modsHost.Add(BuildModRow(mods[i], installed));
            }

            RefreshStats(previewModId: null);
        }

        VisualElement BuildModRow(AttachmentDefinition def, AttachmentInstance? installed)
        {
            bool isInstalled = installed.HasValue && installed.Value.DefinitionId == def.Id;

            var row = new VisualElement();
            row.AddToClassList("ae-mod-row");
            if (isInstalled) row.AddToClassList("ae-mod-row--installed");

            var name = new Label(def.DisplayName);
            name.AddToClassList("ae-mod-name");
            row.Add(name);

            // Owned-count badge ("x3"). The installed mod has left the backpack, so it
            // shows no count — its row is tagged installed instead.
            if (!isInstalled)
            {
                int owned = _presenter.CountInBackpack(def.Id);
                if (owned > 0)
                {
                    var count = new Label($"x{owned}");
                    count.AddToClassList("ae-mod-count");
                    row.Add(count);
                }
            }

            var tags = new VisualElement();
            tags.AddToClassList("ae-mod-tags");
            var mlist = def.Modifiers;
            for (int i = 0; i < mlist.Count; i++)
            {
                var t = new Label(TagText(mlist[i]));
                t.AddToClassList("ae-tag");
                t.AddToClassList(DeltaIsGood(mlist[i].Axis, mlist[i].Percent) ? "ae-tag--up" : "ae-tag--down");
                tags.Add(t);
            }
            row.Add(tags);

            var modId = def.Id;
            var slot = _focusedSlot;
            row.RegisterCallback<PointerEnterEvent>(_ => RefreshStats(previewModId: modId));
            row.RegisterCallback<PointerLeaveEvent>(_ => RefreshStats(previewModId: null));
            row.RegisterCallback<ClickEvent>(_ =>
            {
                // Success fires StateChanged → RebuildAll (which clears the status line).
                // On failure nothing rebuilds, so surface the reason inline here.
                bool ok = isInstalled ? _presenter.Remove(slot) : _presenter.Install(slot, modId);
                if (!ok && _modStatus != null) _modStatus.text = _presenter.LastError ?? string.Empty;
            });
            return row;
        }

        // ── Stats readout + live delta ────────────────────────

        void RefreshStats(string previewModId)
        {
            _statsHost.Clear();
            if (_presenter == null || _presenter.CurrentStats == null) return;

            var div = new VisualElement();
            div.AddToClassList("ae-divider");
            _statsHost.Add(div);

            var current = _presenter.CurrentStats.Value;
            var preview = previewModId != null
                ? (_presenter.PreviewWith(_focusedSlot, previewModId) ?? current)
                : current;

            var baseRows = WeaponStatDisplay.Build(current);
            var showRows = WeaponStatDisplay.Build(preview);

            for (int i = 0; i < showRows.Count; i++)
            {
                var r = showRows[i];
                var row = new VisualElement();
                row.AddToClassList("ae-stat-row");

                var label = new Label(r.Label);
                label.AddToClassList("ae-stat-label");
                row.Add(label);

                if (r.HasBar)
                {
                    // Comparison bar: gold base fill up to min(base, preview), then a
                    // green (improvement) / red (worse) segment spanning the difference.
                    float baseR = baseRows[i].BarRatio01;
                    float prevR = r.BarRatio01;
                    float lo = Mathf.Min(baseR, prevR);
                    float hi = Mathf.Max(baseR, prevR);

                    var bar = new VisualElement();
                    bar.AddToClassList("ae-stat-bar");

                    var fill = new VisualElement();
                    fill.AddToClassList("ae-stat-bar-fill");
                    fill.style.width = new Length(Mathf.Clamp01(lo) * 100f, LengthUnit.Percent);
                    bar.Add(fill);

                    if (hi > lo + 0.0001f)
                    {
                        var delta = new VisualElement();
                        delta.AddToClassList("ae-stat-bar-delta");
                        delta.AddToClassList(prevR > baseR ? "ae-stat-bar-delta--up" : "ae-stat-bar-delta--down");
                        delta.style.width = new Length(Mathf.Clamp01(hi - lo) * 100f, LengthUnit.Percent);
                        bar.Add(delta);
                    }
                    row.Add(bar);
                }
                else
                {
                    var value = new Label(r.Value);
                    value.AddToClassList("ae-stat-value");
                    row.Add(value);
                }

                row.Add(BuildDeltaLabel(previewModId != null ? baseRows[i].Value : r.Value, r.Value));
                _statsHost.Add(row);
            }
        }

        // Signed delta between two display values (higher displayed value = better for every
        // WeaponStatDisplay row). Empty when unchanged / unparseable.
        static Label BuildDeltaLabel(string baseValue, string previewValue)
        {
            var lbl = new Label();
            lbl.AddToClassList("ae-stat-delta");
            if (baseValue != previewValue && TryNum(baseValue, out var b) && TryNum(previewValue, out var p))
            {
                float d = p - b;
                if (!Mathf.Approximately(d, 0f))
                {
                    lbl.text = (d > 0f ? "+" : "") + d.ToString("0.#", CultureInfo.InvariantCulture);
                    lbl.AddToClassList(d > 0f ? "ae-stat-delta--up" : "ae-stat-delta--down");
                }
            }
            return lbl;
        }

        static bool TryNum(string s, out float v)
        {
            v = 0f;
            if (string.IsNullOrEmpty(s)) return false;
            var sb = new StringBuilder();
            foreach (var c in s)
            {
                if (char.IsDigit(c) || c == '.' || (c == '-' && sb.Length == 0)) sb.Append(c);
                else break;
            }
            return sb.Length > 0 &&
                   float.TryParse(sb.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out v);
        }

        // ── helpers ───────────────────────────────────────────

        static readonly Dictionary<WeaponStatAxis, string> AxisShort = new()
        {
            { WeaponStatAxis.Damage, "Dmg" },
            { WeaponStatAxis.RateOfFire, "RoF" },
            { WeaponStatAxis.MagazineSize, "Mag" },
            { WeaponStatAxis.ReloadTime, "Reload" },
            { WeaponStatAxis.Recoil, "Recoil" },
            { WeaponStatAxis.Spread, "Spread" },
            { WeaponStatAxis.Ergonomics, "Ergo" },
        };

        static string TagText(StatDelta d)
        {
            var name = AxisShort.TryGetValue(d.Axis, out var s) ? s : d.Axis.ToString();
            var sign = d.Percent > 0f ? "+" : "";
            return $"{name} {sign}{d.Percent.ToString("0.#", CultureInfo.InvariantCulture)}";
        }

        // Higher-is-better axes improve when the percent is positive; lower-is-better worsen.
        // Single-sourced with the item tooltip via AttachmentStatDisplay.
        static bool DeltaIsGood(WeaponStatAxis axis, float percent) =>
            AttachmentStatDisplay.DeltaIsGood(axis, percent);

        string ModDisplayName(string id)
        {
            var reg = App.Instance?.CoreDefinitions;
            if (reg != null && reg.TryGetAttachment(id, out var def) && def != null)
                return def.DisplayName;
            return id;
        }
    }
}
