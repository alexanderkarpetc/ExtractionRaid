using System.Collections.Generic;
using ApplicationCore;
using State;
using UnityEngine;
using UnityEngine.UIElements;
using View.UI.Tooltip;
using View.UI.WeaponBuilder.Elements;

namespace View.UI.WeaponBuilder
{
    /// <summary>
    /// Runtime UI Toolkit modal for the Weapon Builder. Owns a <see cref="UIDocument"/>
    /// and binds it to a <see cref="WeaponBuilderPresenter"/>. The window is hidden by
    /// default; <see cref="Open"/> / <see cref="Close"/> / <see cref="Toggle"/> drive
    /// visibility and the <see cref="PlayerEntityState.BuilderTargetId"/> gameplay
    /// input gate (also picked up by InventoryUI to open the side-by-side uGUI
    /// inventory canvas).
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
        const int  FadeDurationMs = 160;          // matches USS .wb-window transition (0.15s + 10ms safety)
        const string FadingClass  = "wb-window-fading";

        UIDocument _doc;
        VisualElement _root;
        VisualElement _window;
        int _fadeGen;                              // bumps on each Open/Close to invalidate stale timers

        // Palette / slots
        VisualElement _payloadGrid;
        VisualElement _deliveryGrid;
        VisualElement _slotsHost;
        ModuleSlotElement _payloadSlot;
        ModuleSlotElement _deliverySlot;

        // Preview
        Label _archetypeLabel;
        Label _archetypeFlavor;
        Label _chargeHint;
        VisualElement _statsGrid;
        Label _errorLabel;


        // Window chrome
        Button _closeBtn;
        Button _cancelBtn;
        Button _buildBtn;

        // Card lookup (for highlighting current selection)
        readonly List<ModuleCardElement> _payloadCards  = new();
        readonly List<ModuleCardElement> _deliveryCards = new();

        // ── Drag state ────────────────────────────────────────
        const float DragThreshold = 4f;          // pixels of movement before a drag starts
        ModuleCardElement _draggedCard;
        int _dragPointerId = -1;
        Vector2 _dragStartPanelPos;
        bool _isDragging;
        VisualElement _dragGhost;
        bool _suppressNextClick;                 // skip click that follows a successful drag

        bool _isVisible;

        public bool IsOpen => _isVisible;

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
            RefreshAll();

            // Mount the window invisible, then drop the class next frame so the USS
            // opacity transition runs (0 → 1) for a soft fade-in. Input gate flips
            // immediately — gameplay shouldn't accept input during the fade.
            int gen = ++_fadeGen;
            _root.style.display = DisplayStyle.Flex;
            if (_window != null) _window.AddToClassList(FadingClass);
            _isVisible = true;
            SetInputGate(true);

            _root.schedule.Execute(() =>
            {
                if (gen != _fadeGen) return;
                if (_window != null) _window.RemoveFromClassList(FadingClass);
            }).StartingIn(0);
        }

        public void Close()
        {
            if (_root == null) return;
            CancelActiveDrag();
            if (!_isVisible) return;

            // Start fade-out, then collapse display after the transition. Input gate
            // flips immediately — player should regain control without waiting for
            // the visual to finish.
            int gen = ++_fadeGen;
            _isVisible = false;
            SetInputGate(false);
            if (_window != null) _window.AddToClassList(FadingClass);

            _root.schedule.Execute(() =>
            {
                if (gen != _fadeGen) return;
                _root.style.display = DisplayStyle.None;
                if (_window != null) _window.RemoveFromClassList(FadingClass);
            }).StartingIn(FadeDurationMs);
        }

        void CancelActiveDrag()
        {
            if (_draggedCard == null) return;
            if (_dragPointerId >= 0 && _draggedCard.HasPointerCapture(_dragPointerId))
                _draggedCard.ReleasePointer(_dragPointerId);
            DestroyGhost();
            ClearSlotHover();
            _draggedCard       = null;
            _dragPointerId     = -1;
            _isDragging        = false;
            _suppressNextClick = false;
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

            // Force scale settings at runtime — Unity sometimes keeps a stale
            // cached copy of PanelSettings even after the .asset YAML is edited
            // unless the asset is explicitly reimported. Setting the properties
            // here makes the code authoritative regardless of asset state. See
            // docs/ai/ui-styling.md "Resolution scaling" for required values.
            ApplyResponsiveScale(panel);

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
            ConfigureBodyScrollView();
            BuildSlots();
            WireEvents();

            // Cap window height to the root's resolved height. With
            // ScaleWithScreenSize the root sits in *reference* (panel) space
            // — not actual screen pixels — so capping here is correct: window
            // can't exceed the reference 1080-ish ref px, and body's ScrollView
            // takes over scroll when natural content is taller. (Capping by
            // Screen.height instead would double-shrink under any scale > 1.)
            _root.RegisterCallback<GeometryChangedEvent>(_ => UpdateWindowMaxHeight());
            UpdateWindowMaxHeight();
        }

        void UpdateWindowMaxHeight()
        {
            if (_window == null) return;
            float h = _root != null ? _root.resolvedStyle.height : 0f;
            if (h <= 0f) return;
            _window.style.maxHeight = h;
        }

        // Project-wide PanelSettings configuration. Pulled from
        // docs/ai/ui-styling.md "Resolution scaling". Applied at runtime so a
        // stale cached asset cannot regress the modal's behavior. The runtime
        // override persists in the loaded ScriptableObject instance — every
        // panel using the same asset benefits.
        static void ApplyResponsiveScale(PanelSettings panel)
        {
            panel.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            panel.referenceResolution = new Vector2Int(1920, 1080);
            panel.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            panel.match = 0.5f;
        }

        // Body is a ScrollView as a defensive fallback for viewports where the
        // modal's natural reference content height exceeds 1080 ref px. The
        // ScaleWithScreenSize scale factor doesn't help with vertical overflow
        // — it only changes pixel mapping. See docs/ai/ui-styling.md
        // "Resolution scaling".
        void ConfigureBodyScrollView()
        {
            var body = _root.Q<ScrollView>("body");
            if (body == null) return;
            body.mode = ScrollViewMode.Vertical;
            body.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            body.verticalScrollerVisibility   = ScrollerVisibility.Auto;
        }

        void CacheElements()
        {
            _window          = _root.Q<VisualElement>("window");
            _payloadGrid     = _root.Q<VisualElement>("payloadGrid");
            _deliveryGrid    = _root.Q<VisualElement>("deliveryGrid");
            _slotsHost       = _root.Q<VisualElement>("slotsHost");
            _archetypeLabel  = _root.Q<Label>("archetypeLabel");
            _archetypeFlavor = _root.Q<Label>("archetypeFlavor");
            _chargeHint      = _root.Q<Label>("chargeHint");
            _statsGrid       = _root.Q<VisualElement>("statsGrid");
            _errorLabel      = _root.Q<Label>("errorLabel");
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
                WireCardInteractions(card);
                _payloadGrid.Add(card);
                _payloadCards.Add(card);
            }

            _deliveryGrid.Clear();
            _deliveryCards.Clear();
            foreach (var def in _presenter.AllDeliveries)
            {
                if (def == null) continue;
                var card = new ModuleCardElement(def);
                WireCardInteractions(card);
                _deliveryGrid.Add(card);
                _deliveryCards.Add(card);
            }
        }

        void WireCardInteractions(ModuleCardElement card)
        {
            card.RegisterCallback<PointerDownEvent>(evt => OnCardPointerDown(card, evt));
            card.RegisterCallback<PointerMoveEvent>(evt => OnCardPointerMove(card, evt));
            card.RegisterCallback<PointerUpEvent>(evt   => OnCardPointerUp(card, evt));
            card.RegisterCallback<PointerCaptureOutEvent>(evt => OnCardPointerCaptureOut(card, evt));
            card.RegisterCallback<ClickEvent>(_         => OnCardClicked(card));
        }

        // Click fallback when no drag occurred. Toggles selection on/off so the
        // player can clear a slot with a second click on the selected card.
        void OnCardClicked(ModuleCardElement card)
        {
            if (_presenter == null) return;
            if (_suppressNextClick)
            {
                _suppressNextClick = false;
                return;
            }
            DispatchSelect(card, toggle: true);
        }

        void DispatchSelect(ModuleCardElement card, bool toggle)
        {
            if (card.Kind == ModuleCardElement.ModuleKind.Payload)
            {
                bool already = toggle && _presenter.State.SelectedPayload.DefinitionId == card.DefinitionId;
                _presenter.SelectPayload(already ? null : card.DefinitionId);
            }
            else
            {
                bool already = toggle && _presenter.State.SelectedDelivery.DefinitionId == card.DefinitionId;
                _presenter.SelectDelivery(already ? null : card.DefinitionId);
            }
        }

        // ── Drag state machine ────────────────────────────────

        void OnCardPointerDown(ModuleCardElement card, PointerDownEvent evt)
        {
            if (evt.button != 0) return;          // left-click only
            if (_draggedCard != null) return;     // already tracking another pointer

            _draggedCard       = card;
            _dragPointerId     = evt.pointerId;
            _dragStartPanelPos = evt.position;
            _isDragging        = false;
            card.CapturePointer(evt.pointerId);
        }

        void OnCardPointerMove(ModuleCardElement card, PointerMoveEvent evt)
        {
            if (_draggedCard != card) return;
            if (!card.HasPointerCapture(evt.pointerId)) return;

            if (!_isDragging)
            {
                Vector2 delta = (Vector2)evt.position - _dragStartPanelPos;
                if (delta.sqrMagnitude < DragThreshold * DragThreshold) return;

                _isDragging = true;
                TooltipController.Instance?.Hide();
                CreateGhost(card);
            }

            UpdateGhostPosition(evt.position);
            UpdateSlotHover(evt.position);
        }

        void OnCardPointerUp(ModuleCardElement card, PointerUpEvent evt)
        {
            if (_draggedCard != card) return;

            if (card.HasPointerCapture(evt.pointerId))
                card.ReleasePointer(evt.pointerId);

            if (_isDragging)
            {
                TryDropOnSlot(evt.position);
                DestroyGhost();
                ClearSlotHover();
                // The ClickEvent fires after PointerUp on the originating element,
                // even when the pointer drifted off and we already handled a drop.
                // Suppress the impending click so we don't toggle the selection
                // we just set via drop.
                _suppressNextClick = true;
            }

            _draggedCard   = null;
            _dragPointerId = -1;
            _isDragging    = false;
        }

        void OnCardPointerCaptureOut(ModuleCardElement card, PointerCaptureOutEvent evt)
        {
            if (_draggedCard != card) return;
            // Capture lost (e.g. window closed mid-drag): tear down ghost cleanly.
            DestroyGhost();
            ClearSlotHover();
            _draggedCard   = null;
            _dragPointerId = -1;
            _isDragging    = false;
        }

        // ── Drop target detection ─────────────────────────────

        void TryDropOnSlot(Vector2 panelPos)
        {
            var slot = SlotUnder(panelPos);
            if (slot == null || _draggedCard == null) return;
            if (slot.Kind != _draggedCard.Kind) return; // wrong type — silent reject
            DispatchSelect(_draggedCard, toggle: false);
        }

        ModuleSlotElement SlotUnder(Vector2 panelPos)
        {
            if (_payloadSlot.worldBound.Contains(panelPos))  return _payloadSlot;
            if (_deliverySlot.worldBound.Contains(panelPos)) return _deliverySlot;
            return null;
        }

        void UpdateSlotHover(Vector2 panelPos)
        {
            if (_draggedCard == null) return;
            bool overPayload  = _payloadSlot.worldBound.Contains(panelPos);
            bool overDelivery = _deliverySlot.worldBound.Contains(panelPos);
            bool isPayloadDrag = _draggedCard.Kind == ModuleCardElement.ModuleKind.Payload;

            _payloadSlot.SetDragOver(valid: isPayloadDrag,  hovering: overPayload);
            _deliverySlot.SetDragOver(valid: !isPayloadDrag, hovering: overDelivery);
        }

        void ClearSlotHover()
        {
            _payloadSlot.SetDragOver(false, false);
            _deliverySlot.SetDragOver(false, false);
        }

        // ── Ghost element ─────────────────────────────────────

        void CreateGhost(ModuleCardElement source)
        {
            DestroyGhost();
            _dragGhost = new VisualElement { pickingMode = PickingMode.Ignore };
            _dragGhost.AddToClassList("wb-card");
            _dragGhost.AddToClassList("wb-drag-ghost");

            var title = new Label(source.GetDisplayName());
            title.AddToClassList("wb-card-title");
            _dragGhost.Add(title);

            var kind = new Label(source.Kind == ModuleCardElement.ModuleKind.Payload ? "Payload" : "Delivery");
            kind.AddToClassList("wb-card-kind");
            _dragGhost.Add(kind);

            // Adding to _root puts the ghost on top of the window in tree order
            // and uses panel-coord origin — same space as evt.position.
            _root.Add(_dragGhost);
        }

        void UpdateGhostPosition(Vector2 panelPos)
        {
            if (_dragGhost == null) return;
            float w = _dragGhost.resolvedStyle.width;
            float h = _dragGhost.resolvedStyle.height;
            // First-frame fallback: card sizing inherited from .wb-card.
            if (w <= 0f) w = 160f;
            if (h <= 0f) h = 96f;
            _dragGhost.style.left = panelPos.x - w * 0.5f;
            _dragGhost.style.top  = panelPos.y - h * 0.5f;
        }

        void DestroyGhost()
        {
            if (_dragGhost == null) return;
            _dragGhost.RemoveFromHierarchy();
            _dragGhost = null;
        }

        // ── Refresh on presenter state change ─────────────────

        void OnPresenterChanged() => RefreshAll();

        void RefreshAll()
        {
            if (_presenter == null) return;
            RefreshSlots();
            RefreshCardSelection();
            RefreshCardAvailability();
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

        // Tier 6 G4: dim palette cards whose module isn't у player backpack. Polled
        // each frame while the modal is visible — inventory state has no dedicated
        // change event, and 5 cards × 1 string compare is essentially free.
        void RefreshCardAvailability()
        {
            if (_presenter == null) return;
            foreach (var c in _payloadCards)
                c.SetAvailable(_presenter.IsModuleAvailable(c.DefinitionId));
            foreach (var c in _deliveryCards)
                c.SetAvailable(_presenter.IsModuleAvailable(c.DefinitionId));
        }

        void Update()
        {
            if (_isVisible)
                RefreshCardAvailability();
        }

        void RefreshPreview()
        {
            var archetype = _presenter.PreviewArchetype;
            _archetypeLabel.text = string.IsNullOrEmpty(archetype) ? "—" : archetype;

            // Archetype flavor sub-line ("Reliable single-shot sidearm" etc.)
            var flavor = _presenter.PreviewArchetypeFlavor;
            _archetypeFlavor.text = flavor;
            ToggleEmpty(_archetypeFlavor, string.IsNullOrEmpty(flavor));

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
                var s = stats.Value;

                // Combat — what each shot does on hit.
                AppendGroupHeading(_statsGrid, "Combat", first: true);
                AppendStatRow(_statsGrid, "Damage",      s.Damage.ToString("0.##"));
                AppendStatRow(_statsGrid, "Headshot",    s.HeadshotDamageMultiplier.ToString("0.##") + "×");
                AppendStatRow(_statsGrid, "Penetration", s.BasePenetration.ToString("0.##"));

                // Cadence — pacing of shots and reloads.
                AppendGroupHeading(_statsGrid, "Cadence");
                if (_presenter.PreviewRequiresCharge)
                    AppendStatRow(_statsGrid, "Charge",   _presenter.PreviewChargeTime.ToString("0.##") + " s");
                AppendStatRow(_statsGrid, "Fire Interval", s.FireInterval.ToString("0.##") + " s");
                AppendStatRow(_statsGrid, "Magazine",      s.MagazineSize.ToString());
                AppendStatRow(_statsGrid, "Reload Time",   s.ReloadTime.ToString("0.##") + " s");

                // Pattern — how shots travel and spread.
                AppendGroupHeading(_statsGrid, "Pattern");
                AppendStatRow(_statsGrid, "Projectile Speed",  s.ProjectileSpeed.ToString("0.##"));
                AppendStatRow(_statsGrid, "Projectiles/Shot",  s.ProjectilesPerShot.ToString());
            }

            bool canBuild = _presenter.CanBuild;
            _buildBtn.SetEnabled(canBuild);
            _buildBtn.tooltip = canBuild ? string.Empty : _presenter.DisabledReason;
            _errorLabel.text  = string.Empty;
        }

        static void AppendGroupHeading(VisualElement grid, string heading, bool first = false)
        {
            var label = new Label(heading);
            label.AddToClassList("wb-stat-group-heading");
            if (first) label.AddToClassList("first");
            grid.Add(label);
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

        static void ToggleEmpty(VisualElement el, bool empty)
        {
            const string cls = "is-empty";
            if (empty) el.AddToClassList(cls);
            else       el.RemoveFromClassList(cls);
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

        // ── Input gate + side-by-side inventory coordination ──

        // Sentinel non-zero EId used as `BuilderTargetId` value while the modal is
        // open. We don't currently need to track *which* workbench triggered the
        // Builder (Tier 6 scope) — just the boolean "Builder is open" semantic via
        // the EId pattern that matches LootTargetId / CraftTargetId / etc. Setting
        // this also drives the side-by-side uGUI inventory canvas open/close via
        // InventoryUI.Update.
        static readonly EId BuilderSentinelEId = new EId(int.MaxValue - 1);

        static void SetInputGate(bool open)
        {
            var player = App.Instance?.RaidSession?.RaidState?.PlayerEntity;
            if (player != null)
                player.BuilderTargetId = open ? BuilderSentinelEId : EId.None;
        }
    }
}
