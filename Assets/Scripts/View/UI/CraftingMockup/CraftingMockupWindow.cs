using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace View.UI.CraftingMockup
{
    /// <summary>
    /// UI Toolkit mockup of the crafting window. Visual-only — no gameplay bindings.
    /// Constructs a UIDocument at runtime and drives it off <see cref="CraftingMockupData"/>.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class CraftingMockupWindow : MonoBehaviour
    {
        public static CraftingMockupWindow Instance { get; private set; }

        /// <summary>Raised when the window goes from visible to hidden (close button, Toggle, or Hide).</summary>
        public event System.Action Closed;

        public bool IsVisible => _isVisible;

        UIDocument _doc;
        PanelSettings _panelSettings;
        VisualTreeAsset _treeAsset;
        StyleSheet _styleSheet;

        // Root refs
        VisualElement _root;
        VisualElement _tabsEl;
        VisualElement _typeFiltersEl;
        ScrollView _itemGridWrap;
        VisualElement _itemGridEl;
        Label _footerCountEl;
        TextField _searchInput;
        Toggle _craftableToggle;
        Button _closeBtn;

        Label _heroTitle, _heroSubtitle, _heroDescription, _heroEmoji, _rarityBadge;
        ScrollView _statsList;
        VisualElement _requirementGrid;
        Label _workbenchValue, _craftStatus, _amountValue;
        Button _minusBtn, _plusBtn, _maxBtn, _maxAvailableBtn, _craftBtn;

        // State
        string _activeTab = "weapons";
        string _typeFilter = "all";
        string _selectedItemId = "smg-45";
        string _search = "";
        bool _craftableOnly;
        int _amount = 1;

        bool _isVisible;

        void Awake()
        {
            Instance = this;
            BuildDocument();
            Hide();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void Update()
        {
            if (!_isVisible) return;
            var kb = Keyboard.current;
            if (kb != null && kb[Key.Escape].wasPressedThisFrame) Hide();
        }

        public void Toggle()
        {
            if (_isVisible) Hide(); else Show();
        }

        public void Show()
        {
            if (_root == null) return;
            _root.style.display = DisplayStyle.Flex;
            _isVisible = true;
            RenderAll();
        }

        public void Hide()
        {
            if (_root == null) return;
            bool wasVisible = _isVisible;
            _root.style.display = DisplayStyle.None;
            _isVisible = false;
            if (wasVisible) Closed?.Invoke();
        }

        // ── Setup ─────────────────────────────────────────────

        void BuildDocument()
        {
            _treeAsset = Resources.Load<VisualTreeAsset>("UI/Crafting/CraftingMockupWindow");
            _styleSheet = Resources.Load<StyleSheet>("UI/Crafting/CraftingMockupWindow");
            _panelSettings = Resources.Load<PanelSettings>("UI/Crafting/CraftingMockupPanelSettings");

            Debug.Log($"[CraftingMockup] assets — uxml:{(_treeAsset != null)}  uss:{(_styleSheet != null)}  panel:{(_panelSettings != null)}");

            if (_treeAsset == null || _panelSettings == null)
            {
                Debug.LogWarning("[CraftingMockup] Missing UXML or PanelSettings in Resources/UI/Crafting/. " +
                                 "Check the editor bootstrap log and make sure the assets imported.");
                return;
            }

            _doc = gameObject.AddComponent<UIDocument>();
            _doc.panelSettings = _panelSettings;
            _doc.visualTreeAsset = _treeAsset;

            _root = _doc.rootVisualElement;

            // UXML now references the USS via <Style src="..."/>. Attach as fallback in case that fails.
            if (_styleSheet != null && !_root.styleSheets.Contains(_styleSheet))
                _root.styleSheets.Add(_styleSheet);

            // Force root to fill the panel.
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
            _tabsEl = _root.Q<VisualElement>("tabs");
            _typeFiltersEl = _root.Q<VisualElement>("typeFilters");
            _itemGridWrap = _root.Q<ScrollView>("itemGridWrap");
            _itemGridEl = _root.Q<VisualElement>("itemGrid");
            _footerCountEl = _root.Q<Label>("footerCount");
            _searchInput = _root.Q<TextField>("searchInput");
            _craftableToggle = _root.Q<Toggle>("craftableOnly");
            _closeBtn = _root.Q<Button>("closeBtn");

            _heroTitle = _root.Q<Label>("heroTitle");
            _heroSubtitle = _root.Q<Label>("heroSubtitle");
            _heroDescription = _root.Q<Label>("heroDescription");
            _heroEmoji = _root.Q<Label>("heroEmoji");
            _rarityBadge = _root.Q<Label>("rarityBadge");

            _statsList = _root.Q<ScrollView>("statsList");
            _requirementGrid = _root.Q<VisualElement>("requirementGrid");
            _workbenchValue = _root.Q<Label>("workbenchValue");
            _craftStatus = _root.Q<Label>("craftStatus");

            _amountValue = _root.Q<Label>("amountValue");
            _minusBtn = _root.Q<Button>("minusBtn");
            _plusBtn = _root.Q<Button>("plusBtn");
            _maxBtn = _root.Q<Button>("maxBtn");
            _maxAvailableBtn = _root.Q<Button>("maxAvailableBtn");
            _craftBtn = _root.Q<Button>("craftBtn");
        }

        void WireEvents()
        {
            _closeBtn.clicked += Hide;

            _searchInput.RegisterValueChangedCallback(evt =>
            {
                _search = evt.newValue ?? "";
                RenderGrid();
                RenderDetails();
            });

            _craftableToggle.RegisterValueChangedCallback(evt =>
            {
                _craftableOnly = evt.newValue;
                RenderGrid();
                RenderDetails();
            });

            _minusBtn.clicked += () => { _amount = Mathf.Max(1, _amount - 1); UpdateAmountLabel(); };
            _plusBtn.clicked += () => { _amount = Mathf.Min(99, _amount + 1); UpdateAmountLabel(); };
            _maxBtn.clicked += () => { _amount = 99; UpdateAmountLabel(); };
            _maxAvailableBtn.clicked += () =>
            {
                var it = GetSelectedItem();
                if (it == null) return;
                int max = int.MaxValue;
                foreach (var r in it.Requirements)
                {
                    int count = r.Need > 0 ? r.Have / r.Need : 99;
                    if (count < max) max = count;
                }
                _amount = Mathf.Clamp(max <= 0 ? 1 : max, 1, 99);
                UpdateAmountLabel();
            };
            _craftBtn.clicked += () =>
            {
                var it = GetSelectedItem();
                if (it == null || !CraftingMockupData.CanCraft(it)) return;
                Debug.Log($"[CraftingMockup] Crafted {_amount} x {it.Title}");
            };
        }

        // ── Rendering ─────────────────────────────────────────

        void RenderAll()
        {
            RenderTabs();
            RenderTypeFilters();
            RenderGrid();
            RenderDetails();
        }

        void RenderTabs()
        {
            _tabsEl.Clear();
            foreach (var tab in CraftingMockupData.Tabs)
            {
                var btn = new Button { text = tab.Label };
                btn.AddToClassList("tab");
                if (tab.Key == _activeTab) btn.AddToClassList("tab--active");
                var key = tab.Key;
                btn.clicked += () =>
                {
                    if (_activeTab == key) return;
                    _activeTab = key;
                    _typeFilter = "all";
                    _search = "";
                    _searchInput.SetValueWithoutNotify("");
                    _amount = 1;
                    var list = GetFilteredItems();
                    _selectedItemId = list.Count > 0 ? list[0].Id : null;
                    RenderAll();
                };
                _tabsEl.Add(btn);
            }
        }

        void RenderTypeFilters()
        {
            _typeFiltersEl.Clear();
            foreach (var f in CraftingMockupData.GetTypeFilters(_activeTab))
            {
                var btn = new Button { text = f.Label };
                btn.AddToClassList("toolbar-btn");
                if (f.Key == _typeFilter) btn.AddToClassList("toolbar-btn--active");
                var key = f.Key;
                btn.clicked += () =>
                {
                    _typeFilter = key;
                    RenderTypeFilters();
                    RenderGrid();
                    RenderDetails();
                };
                _typeFiltersEl.Add(btn);
            }
        }

        void RenderGrid()
        {
            _itemGridEl.Clear();

            var tabItems = CraftingMockupData.Items.FindAll(i => i.Category == _activeTab);
            var list = GetFilteredItems();
            EnsureValidSelection(list);

            _footerCountEl.text = $"{list.Count} / {tabItems.Count} items";

            if (list.Count == 0)
            {
                var empty = new Label("No matching recipes") { pickingMode = PickingMode.Ignore };
                empty.style.color = new StyleColor(new Color(0.56f, 0.62f, 0.74f));
                empty.style.paddingTop = 24;
                empty.style.paddingLeft = 24;
                _itemGridEl.Add(empty);
                return;
            }

            foreach (var item in list)
            {
                var card = new Button();
                card.AddToClassList("item-card");
                if (item.Id == _selectedItemId) card.AddToClassList("item-card--selected");

                var count = new Label(item.Count.ToString()) { pickingMode = PickingMode.Ignore };
                count.AddToClassList("item-count");
                card.Add(count);

                var icon = new VisualElement();
                icon.AddToClassList("icon-box");
                icon.pickingMode = PickingMode.Ignore;
                var iconLabel = new Label(item.Icon) { pickingMode = PickingMode.Ignore };
                iconLabel.style.fontSize = 20;
                iconLabel.style.color = new StyleColor(new Color(0.78f, 0.82f, 0.90f));
                iconLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
                icon.Add(iconLabel);
                card.Add(icon);

                var name = new Label(item.Title) { pickingMode = PickingMode.Ignore };
                name.AddToClassList("item-name");
                card.Add(name);

                var meta = new Label("x1") { pickingMode = PickingMode.Ignore };
                meta.AddToClassList("item-meta");
                card.Add(meta);

                var id = item.Id;
                card.clicked += () =>
                {
                    _selectedItemId = id;
                    _amount = 1;
                    RenderGrid();
                    RenderDetails();
                };
                _itemGridEl.Add(card);
            }
        }

        void RenderDetails()
        {
            var item = GetSelectedItem();
            if (item == null)
            {
                _heroTitle.text = "—";
                _heroSubtitle.text = "";
                _heroDescription.text = "Select an item to see recipe details.";
                _heroEmoji.text = "?";
                _rarityBadge.text = "";
                _statsList.Clear();
                _requirementGrid.Clear();
                _workbenchValue.text = "";
                _craftStatus.text = "";
                _craftBtn.SetEnabled(false);
                return;
            }

            _heroTitle.text = item.Title;
            _heroSubtitle.text = item.Subtitle;
            _heroDescription.text = item.Description;
            _heroEmoji.text = item.Icon;

            _rarityBadge.text = item.Rarity.ToString().ToUpperInvariant();
            _rarityBadge.RemoveFromClassList("rarity-badge--common");
            _rarityBadge.RemoveFromClassList("rarity-badge--uncommon");
            _rarityBadge.RemoveFromClassList("rarity-badge--rare");
            _rarityBadge.RemoveFromClassList("rarity-badge--epic");
            _rarityBadge.AddToClassList("rarity-badge--" + item.Rarity.ToString().ToLowerInvariant());

            // Stats
            _statsList.Clear();
            foreach (var s in item.Stats)
            {
                var row = new VisualElement();
                row.AddToClassList("stat-row");
                var label = new Label(s.Label);
                label.AddToClassList("stat-label");
                var val = new Label(s.Display);
                val.AddToClassList("stat-value");
                row.Add(label);
                row.Add(val);
                _statsList.Add(row);
            }

            // Requirements
            _requirementGrid.Clear();
            foreach (var r in item.Requirements)
            {
                var card = new VisualElement();
                card.AddToClassList("req-card");
                bool ok = r.Have >= r.Need;
                if (ok) card.AddToClassList("req-card--ready");

                var iconBox = new VisualElement();
                iconBox.AddToClassList("icon-box");
                var iconLabel = new Label(r.Icon);
                iconLabel.style.fontSize = 20;
                iconLabel.style.color = new StyleColor(new Color(0.78f, 0.82f, 0.90f));
                iconLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
                iconBox.Add(iconLabel);
                card.Add(iconBox);

                var n = new Label(r.Name);
                n.AddToClassList("req-name");
                card.Add(n);

                var c = new Label($"{r.Have} / {r.Need}");
                c.AddToClassList("req-count");
                if (!ok) c.AddToClassList("req-count--missing");
                card.Add(c);

                _requirementGrid.Add(card);
            }

            bool ready = CraftingMockupData.CanCraft(item);
            _workbenchValue.text = item.Workbench;
            _craftStatus.text = ready ? "Ready to craft" : "Missing materials";
            _craftBtn.SetEnabled(ready);

            UpdateAmountLabel();
        }

        void UpdateAmountLabel()
        {
            if (_amountValue != null) _amountValue.text = _amount.ToString();
        }

        // ── Helpers ───────────────────────────────────────────

        List<CraftingMockupData.Item> GetFilteredItems()
        {
            var list = new List<CraftingMockupData.Item>();
            string q = _search.Trim().ToLowerInvariant();

            foreach (var i in CraftingMockupData.Items)
            {
                if (i.Category != _activeTab) continue;
                if (_typeFilter != "all" && i.Type != _typeFilter) continue;
                if (!string.IsNullOrEmpty(q))
                {
                    if (i.Title.ToLowerInvariant().IndexOf(q) < 0 &&
                        i.Subtitle.ToLowerInvariant().IndexOf(q) < 0 &&
                        i.Description.ToLowerInvariant().IndexOf(q) < 0)
                        continue;
                }
                if (_craftableOnly && !CraftingMockupData.CanCraft(i)) continue;
                list.Add(i);
            }
            return list;
        }

        void EnsureValidSelection(List<CraftingMockupData.Item> list)
        {
            if (list.Count == 0) { _selectedItemId = null; return; }
            foreach (var i in list)
                if (i.Id == _selectedItemId) return;
            _selectedItemId = list[0].Id;
        }

        CraftingMockupData.Item GetSelectedItem()
        {
            if (string.IsNullOrEmpty(_selectedItemId)) return null;
            return CraftingMockupData.Items.Find(i => i.Id == _selectedItemId);
        }
    }
}
