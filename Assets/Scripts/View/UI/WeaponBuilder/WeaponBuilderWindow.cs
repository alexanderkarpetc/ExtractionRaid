using System.Collections.Generic;
using ApplicationCore;
using State;
using UnityEngine;
using UnityEngine.UIElements;

namespace View.UI.WeaponBuilder
{
    /// <summary>
    /// Runtime UI Toolkit modal for the Weapon Builder. Owns a <see cref="UIDocument"/>
    /// and binds it to a <see cref="WeaponBuilderPresenter"/>. The window is hidden by
    /// default; <see cref="Open"/> / <see cref="Close"/> / <see cref="Toggle"/> drive
    /// visibility and the <see cref="PlayerEntityState.IsWeaponBuilderOpen"/> gameplay
    /// input gate.
    ///
    /// Singleton pattern for easy access from Workbench / DevCheats. Instance lives as
    /// long as the root GameObject (spawned by <c>App</c> at composition-root time).
    ///
    /// See docs/ai/weapon-builder/architecture.md §D9, §D11, §D13.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class WeaponBuilderWindow : MonoBehaviour
    {
        public static WeaponBuilderWindow Instance { get; private set; }

        // ── Deps ──────────────────────────────────────────────
        WeaponBuilderPresenter _presenter;

        // ── Runtime UI ────────────────────────────────────────
        UIDocument _doc;
        PanelSettings _panelSettings;
        VisualTreeAsset _treeAsset;
        StyleSheet _styleSheet;

        VisualElement _root;
        DropdownField _payloadDropdown;
        DropdownField _deliveryDropdown;
        Label _archetypeLabel;
        Label _chargeHint;
        VisualElement _statsGrid;
        Label _errorLabel;
        Button _closeBtn;
        Button _cancelBtn;
        Button _buildBtn;

        // Parallel lists: index-aligned id → display (dropdowns use display strings).
        readonly List<string> _payloadIds = new();
        readonly List<string> _deliveryIds = new();

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
            if (_presenter != null) _presenter.StateChanged -= RefreshPreview;
        }

        /// <summary>
        /// Called by <c>App</c> once the registry + inventory are ready. Wires the
        /// presenter to this view.
        /// </summary>
        public void Initialize(WeaponBuilderPresenter presenter)
        {
            if (_presenter != null) _presenter.StateChanged -= RefreshPreview;
            _presenter = presenter;
            if (_presenter != null) _presenter.StateChanged += RefreshPreview;
        }

        // ── Public API ────────────────────────────────────────

        public void Toggle()
        {
            if (_isVisible) Close(); else Open();
        }

        public void Open()
        {
            if (_presenter == null)
            {
                Debug.LogWarning("[WeaponBuilder] Cannot Open — presenter not initialized. " +
                                 "App.StartRaid must run first (or DevCheats invoked before composition root).");
                return;
            }
            if (_root == null) return;

            _presenter.ClearSelection();
            PopulateDropdowns();
            RefreshPreview();

            _root.style.display = DisplayStyle.Flex;
            _isVisible = true;
            SetInputGate(true);
        }

        public void Close()
        {
            if (_root == null) return;
            _root.style.display = DisplayStyle.None;
            _isVisible = false;
            SetInputGate(false);
        }

        void HideImmediate()
        {
            if (_root != null) _root.style.display = DisplayStyle.None;
            _isVisible = false;
        }

        // ── Setup ─────────────────────────────────────────────

        void BuildDocument()
        {
            _treeAsset     = Resources.Load<VisualTreeAsset>("UI/WeaponBuilder/WeaponBuilderWindow");
            _styleSheet    = Resources.Load<StyleSheet>("UI/WeaponBuilder/WeaponBuilderWindow");
            _panelSettings = Resources.Load<PanelSettings>("UI/WeaponBuilder/WeaponBuilderPanelSettings");

            if (_treeAsset == null || _panelSettings == null)
            {
                Debug.LogWarning("[WeaponBuilder] Missing UXML or PanelSettings at Resources/UI/WeaponBuilder/. " +
                                 "Check the editor bootstrap log and make sure the assets imported.");
                return;
            }

            _doc = gameObject.AddComponent<UIDocument>();
            _doc.panelSettings = _panelSettings;
            _doc.visualTreeAsset = _treeAsset;

            _root = _doc.rootVisualElement;

            if (_styleSheet != null && !_root.styleSheets.Contains(_styleSheet))
                _root.styleSheets.Add(_styleSheet);

            _root.style.flexGrow = 1;
            _root.style.flexDirection = FlexDirection.Column;
            _root.style.position = Position.Absolute;
            _root.style.left = 0;
            _root.style.right = 0;
            _root.style.top = 0;
            _root.style.bottom = 0;

            CacheElements();
            WireEvents();
        }

        void CacheElements()
        {
            _payloadDropdown  = _root.Q<DropdownField>("payloadDropdown");
            _deliveryDropdown = _root.Q<DropdownField>("deliveryDropdown");
            _archetypeLabel   = _root.Q<Label>("archetypeLabel");
            _chargeHint       = _root.Q<Label>("chargeHint");
            _statsGrid        = _root.Q<VisualElement>("statsGrid");
            _errorLabel       = _root.Q<Label>("errorLabel");
            _closeBtn         = _root.Q<Button>("closeBtn");
            _cancelBtn        = _root.Q<Button>("cancelBtn");
            _buildBtn         = _root.Q<Button>("buildBtn");
        }

        void WireEvents()
        {
            _closeBtn.clicked  += Close;
            _cancelBtn.clicked += Close;

            _buildBtn.clicked += OnBuildClicked;

            _payloadDropdown.RegisterValueChangedCallback(evt =>
            {
                if (_presenter == null) return;
                int idx = _payloadDropdown.choices.IndexOf(evt.newValue);
                var id = (idx >= 0 && idx < _payloadIds.Count) ? _payloadIds[idx] : null;
                _presenter.SelectPayload(id);
            });

            _deliveryDropdown.RegisterValueChangedCallback(evt =>
            {
                if (_presenter == null) return;
                int idx = _deliveryDropdown.choices.IndexOf(evt.newValue);
                var id = (idx >= 0 && idx < _deliveryIds.Count) ? _deliveryIds[idx] : null;
                _presenter.SelectDelivery(id);
            });

            // ESC to close.
            _root.RegisterCallback<KeyDownEvent>(evt =>
            {
                if (evt.keyCode == KeyCode.Escape)
                {
                    Close();
                    evt.StopPropagation();
                }
            });
        }

        // ── Dropdown population ───────────────────────────────

        void PopulateDropdowns()
        {
            _payloadIds.Clear();
            var payloadChoices = new List<string>();
            foreach (var def in _presenter.AllPayloads)
            {
                if (def == null) continue;
                _payloadIds.Add(def.Id);
                payloadChoices.Add(FormatPayloadName(def));
            }
            _payloadDropdown.choices = payloadChoices;
            _payloadDropdown.SetValueWithoutNotify(string.Empty);

            _deliveryIds.Clear();
            var deliveryChoices = new List<string>();
            foreach (var def in _presenter.AllDeliveries)
            {
                if (def == null) continue;
                _deliveryIds.Add(def.Id);
                deliveryChoices.Add(FormatDeliveryName(def));
            }
            _deliveryDropdown.choices = deliveryChoices;
            _deliveryDropdown.SetValueWithoutNotify(string.Empty);
        }

        static string FormatPayloadName(PayloadCoreDefinition def)
            => !string.IsNullOrEmpty(def.DisplayName) ? def.DisplayName : def.Id;

        static string FormatDeliveryName(DeliveryCoreDefinition def)
            => !string.IsNullOrEmpty(def.FormFactor) ? def.FormFactor : def.Id;

        // ── Preview rendering ─────────────────────────────────

        void RefreshPreview()
        {
            if (_presenter == null) return;

            // Archetype label
            var archetype = _presenter.PreviewArchetype;
            _archetypeLabel.text = string.IsNullOrEmpty(archetype) ? "—" : archetype;

            // Charge-up hint (only shown when payload requires charging)
            if (_presenter.PreviewRequiresCharge)
            {
                _chargeHint.text = $"⚡ Requires charge — {_presenter.PreviewChargeTime:0.0}s before each shot";
                _chargeHint.style.display = DisplayStyle.Flex;
            }
            else
            {
                _chargeHint.text = string.Empty;
                _chargeHint.style.display = DisplayStyle.None;
            }

            // Stats grid
            _statsGrid.Clear();
            var stats = _presenter.PreviewStats;
            if (stats.HasValue)
            {
                AppendStatRow(_statsGrid, "Damage",         stats.Value.Damage.ToString("0.##"));
                AppendStatRow(_statsGrid, "Fire Interval",  stats.Value.FireInterval.ToString("0.##") + " s");
                AppendStatRow(_statsGrid, "Magazine",       stats.Value.MagazineSize.ToString());
                AppendStatRow(_statsGrid, "Reload Time",    stats.Value.ReloadTime.ToString("0.##") + " s");
                AppendStatRow(_statsGrid, "Projectile Speed", stats.Value.ProjectileSpeed.ToString("0.##"));
                AppendStatRow(_statsGrid, "Headshot Mult",  stats.Value.HeadshotDamageMultiplier.ToString("0.##") + "×");
                AppendStatRow(_statsGrid, "Penetration",    stats.Value.BasePenetration.ToString("0.##"));
                AppendStatRow(_statsGrid, "Projectiles/Shot", stats.Value.ProjectilesPerShot.ToString());
            }

            // Build button enabled state + disabled tooltip explaining why
            bool canBuild = _presenter.CanBuild;
            _buildBtn.SetEnabled(canBuild);
            _buildBtn.tooltip = canBuild ? string.Empty : _presenter.DisabledReason;
            _errorLabel.text = string.Empty;
        }

        static void AppendStatRow(VisualElement grid, string label, string value)
        {
            var row = new VisualElement { name = "statRow" };
            row.AddToClassList("wb-stat-row");

            var l = new Label(label);
            l.AddToClassList("wb-stat-label");

            var v = new Label(value);
            v.AddToClassList("wb-stat-value");

            row.Add(l);
            row.Add(v);
            grid.Add(row);
        }

        // ── Build action ──────────────────────────────────────

        void OnBuildClicked()
        {
            if (_presenter == null) return;
            if (_presenter.TryBuild(out var reason))
            {
                Close();
                return;
            }
            _errorLabel.text = reason ?? "Build failed.";
        }

        // ── Input gate ────────────────────────────────────────

        static void SetInputGate(bool open)
        {
            var player = App.Instance?.RaidSession?.RaidState?.PlayerEntity;
            if (player != null)
                player.IsWeaponBuilderOpen = open;
        }
    }
}
