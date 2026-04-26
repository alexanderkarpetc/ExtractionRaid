using System.Collections.Generic;
using ApplicationCore;
using State;
using UnityEngine;
using UnityEngine.UIElements;
using View.UI.WeaponBuilder.Elements;

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
    /// Layout: palette (cards) + slots (Payload + Delivery) + preview + read-only
    /// backpack context. Drag-and-drop is layered on top in Phase 2 — Phase 1
    /// uses click-to-select.
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
        VisualElement _root;

        // Palette / slots
        VisualElement _payloadGrid;
        VisualElement _deliveryGrid;
        VisualElement _slotsHost;
        ModuleSlotElement _payloadSlot;
        ModuleSlotElement _deliverySlot;

        // Preview
        Label _archetypeLabel;
        Label _chargeHint;
        VisualElement _statsGrid;
        Label _errorLabel;

        // Backpack
        VisualElement _backpackGrid;
        readonly List<BackpackItemElement> _backpackItems = new();

        // Window chrome
        Button _closeBtn;
        Button _cancelBtn;
        Button _buildBtn;

        // Card lookup (for highlighting current selection)
        readonly List<ModuleCardElement> _payloadCards  = new();
        readonly List<ModuleCardElement> _deliveryCards = new();

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
            if (_presenter != null) _presenter.StateChanged -= OnPresenterChanged;
        }

        public void Initialize(WeaponBuilderPresenter presenter)
        {
            if (_presenter != null) _presenter.StateChanged -= OnPresenterChanged;
            _presenter = presenter;
            if (_presenter != null) _presenter.StateChanged += OnPresenterChanged;
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
                Debug.LogWarning("[WeaponBuilder] Cannot Open — presenter not initialized.");
                return;
            }
            if (_root == null) return;

            _presenter.ClearSelection();
            PopulatePalette();
            RefreshBackpack();
            RefreshAll();

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
            var tree   = Resources.Load<VisualTreeAsset>("UI/WeaponBuilder/WeaponBuilderWindow");
            var sheet  = Resources.Load<StyleSheet>("UI/WeaponBuilder/WeaponBuilderWindow");
            var panel  = Resources.Load<PanelSettings>("UI/WeaponBuilder/WeaponBuilderPanelSettings");

            if (tree == null || panel == null)
            {
                Debug.LogWarning("[WeaponBuilder] Missing UXML or PanelSettings at Resources/UI/WeaponBuilder/.");
                return;
            }

            _doc = gameObject.AddComponent<UIDocument>();
            _doc.panelSettings   = panel;
            _doc.visualTreeAsset = tree;

            _root = _doc.rootVisualElement;
            if (sheet != null && !_root.styleSheets.Contains(sheet))
                _root.styleSheets.Add(sheet);

            _root.style.flexGrow = 1;
            _root.style.flexDirection = FlexDirection.Column;
            _root.style.position = Position.Absolute;
            _root.style.left = 0;
            _root.style.right = 0;
            _root.style.top = 0;
            _root.style.bottom = 0;

            CacheElements();
            BuildSlots();
            WireEvents();
        }

        void CacheElements()
        {
            _payloadGrid    = _root.Q<VisualElement>("payloadGrid");
            _deliveryGrid   = _root.Q<VisualElement>("deliveryGrid");
            _slotsHost      = _root.Q<VisualElement>("slotsHost");
            _archetypeLabel = _root.Q<Label>("archetypeLabel");
            _chargeHint     = _root.Q<Label>("chargeHint");
            _statsGrid      = _root.Q<VisualElement>("statsGrid");
            _errorLabel     = _root.Q<Label>("errorLabel");
            _backpackGrid   = _root.Q<VisualElement>("backpackGrid");
            _closeBtn       = _root.Q<Button>("closeBtn");
            _cancelBtn      = _root.Q<Button>("cancelBtn");
            _buildBtn       = _root.Q<Button>("buildBtn");
        }

        void BuildSlots()
        {
            _payloadSlot  = new ModuleSlotElement(ModuleCardElement.ModuleKind.Payload);
            _deliverySlot = new ModuleSlotElement(ModuleCardElement.ModuleKind.Delivery);
            _payloadSlot.Cleared  += _ => _presenter?.SelectPayload(null);
            _deliverySlot.Cleared += _ => _presenter?.SelectDelivery(null);
            _slotsHost.Add(_payloadSlot);
            _slotsHost.Add(_deliverySlot);
        }

        void WireEvents()
        {
            _closeBtn.clicked  += Close;
            _cancelBtn.clicked += Close;
            _buildBtn.clicked  += OnBuildClicked;

            _root.RegisterCallback<KeyDownEvent>(evt =>
            {
                if (evt.keyCode == KeyCode.Escape)
                {
                    Close();
                    evt.StopPropagation();
                }
            });
        }

        // ── Palette population ────────────────────────────────

        void PopulatePalette()
        {
            _payloadGrid.Clear();
            _payloadCards.Clear();
            foreach (var def in _presenter.AllPayloads)
            {
                if (def == null) continue;
                var card = new ModuleCardElement(def);
                card.Clicked += OnPayloadCardClicked;
                _payloadGrid.Add(card);
                _payloadCards.Add(card);
            }

            _deliveryGrid.Clear();
            _deliveryCards.Clear();
            foreach (var def in _presenter.AllDeliveries)
            {
                if (def == null) continue;
                var card = new ModuleCardElement(def);
                card.Clicked += OnDeliveryCardClicked;
                _deliveryGrid.Add(card);
                _deliveryCards.Add(card);
            }
        }

        void OnPayloadCardClicked(ModuleCardElement card)
        {
            if (_presenter == null) return;
            // Toggle: click again on the selected card to clear it.
            bool alreadySelected = _presenter.State.SelectedPayload.DefinitionId == card.DefinitionId;
            _presenter.SelectPayload(alreadySelected ? null : card.DefinitionId);
        }

        void OnDeliveryCardClicked(ModuleCardElement card)
        {
            if (_presenter == null) return;
            bool alreadySelected = _presenter.State.SelectedDelivery.DefinitionId == card.DefinitionId;
            _presenter.SelectDelivery(alreadySelected ? null : card.DefinitionId);
        }

        // ── Refresh on presenter state change ─────────────────

        void OnPresenterChanged() => RefreshAll();

        void RefreshAll()
        {
            if (_presenter == null) return;
            RefreshSlots();
            RefreshCardSelection();
            RefreshPreview();
        }

        void RefreshSlots()
        {
            // Payload slot
            if (_presenter.State.HasPayload &&
                TryGetPayloadDef(_presenter.State.SelectedPayload.DefinitionId, out var pdef))
            {
                _payloadSlot.Fill(pdef);
            }
            else
            {
                _payloadSlot.Clear();
            }

            // Delivery slot
            if (_presenter.State.HasDelivery &&
                TryGetDeliveryDef(_presenter.State.SelectedDelivery.DefinitionId, out var ddef))
            {
                _deliverySlot.Fill(ddef);
            }
            else
            {
                _deliverySlot.Clear();
            }
        }

        void RefreshCardSelection()
        {
            string payloadId  = _presenter.State.HasPayload  ? _presenter.State.SelectedPayload.DefinitionId  : null;
            string deliveryId = _presenter.State.HasDelivery ? _presenter.State.SelectedDelivery.DefinitionId : null;

            foreach (var c in _payloadCards)  c.SetSelected(c.DefinitionId == payloadId);
            foreach (var c in _deliveryCards) c.SetSelected(c.DefinitionId == deliveryId);
        }

        void RefreshPreview()
        {
            var archetype = _presenter.PreviewArchetype;
            _archetypeLabel.text = string.IsNullOrEmpty(archetype) ? "—" : archetype;

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

            _statsGrid.Clear();
            var stats = _presenter.PreviewStats;
            if (stats.HasValue)
            {
                AppendStatRow(_statsGrid, "Damage",            stats.Value.Damage.ToString("0.##"));
                AppendStatRow(_statsGrid, "Fire Interval",     stats.Value.FireInterval.ToString("0.##") + " s");
                AppendStatRow(_statsGrid, "Magazine",          stats.Value.MagazineSize.ToString());
                AppendStatRow(_statsGrid, "Reload Time",       stats.Value.ReloadTime.ToString("0.##") + " s");
                AppendStatRow(_statsGrid, "Projectile Speed",  stats.Value.ProjectileSpeed.ToString("0.##"));
                AppendStatRow(_statsGrid, "Headshot Mult",     stats.Value.HeadshotDamageMultiplier.ToString("0.##") + "×");
                AppendStatRow(_statsGrid, "Penetration",       stats.Value.BasePenetration.ToString("0.##"));
                AppendStatRow(_statsGrid, "Projectiles/Shot",  stats.Value.ProjectilesPerShot.ToString());
            }

            bool canBuild = _presenter.CanBuild;
            _buildBtn.SetEnabled(canBuild);
            _buildBtn.tooltip = canBuild ? string.Empty : _presenter.DisabledReason;
            _errorLabel.text  = string.Empty;
        }

        static void AppendStatRow(VisualElement grid, string label, string value)
        {
            var row = new VisualElement();
            row.AddToClassList("wb-stat-row");

            var l = new Label(label);
            l.AddToClassList("wb-stat-label");

            var v = new Label(value);
            v.AddToClassList("wb-stat-value");

            row.Add(l);
            row.Add(v);
            grid.Add(row);
        }

        // ── Backpack rendering ────────────────────────────────

        void RefreshBackpack()
        {
            _backpackGrid.Clear();
            _backpackItems.Clear();

            var inventory = App.Instance?.Player?.Inventory;
            var registry  = App.Instance?.CoreDefinitions;
            if (inventory == null) return;

            for (int i = 0; i < InventoryState.BackpackSize; i++)
            {
                var el = new BackpackItemElement();
                el.Bind(inventory.Backpack[i], registry);
                _backpackGrid.Add(el);
                _backpackItems.Add(el);
            }
        }

        // ── Build action ──────────────────────────────────────

        void OnBuildClicked()
        {
            if (_presenter == null) return;
            if (_presenter.TryBuild(out var reason))
            {
                // Reflect the just-added weapon + ammo grant in the in-window backpack.
                RefreshBackpack();
                Close();
                return;
            }
            _errorLabel.text = reason ?? "Build failed.";
        }

        // ── Helpers ───────────────────────────────────────────

        bool TryGetPayloadDef(string id, out PayloadCoreDefinition def)
        {
            foreach (var d in _presenter.AllPayloads)
            {
                if (d != null && d.Id == id) { def = d; return true; }
            }
            def = null;
            return false;
        }

        bool TryGetDeliveryDef(string id, out DeliveryCoreDefinition def)
        {
            foreach (var d in _presenter.AllDeliveries)
            {
                if (d != null && d.Id == id) { def = d; return true; }
            }
            def = null;
            return false;
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
