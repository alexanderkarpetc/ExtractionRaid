using System;
using System.Collections.Generic;
using ApplicationCore;
using Constants;
using State;
using Systems;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using View.UI.Common;

namespace View.UI.Craft
{
    public class CraftPopupView : PopupBase
    {
        [SerializeField] private Button _closeButton;
        [SerializeField] private TabItemView _weaponTabButton;
        [SerializeField] private TabItemView _medsTabButton;
        [SerializeField] private TabItemView _modsTabButton;
        [SerializeField] private TabItemView _ammoTabButton;
        [SerializeField] private TMP_InputField _searchInputField;
        [SerializeField] private Button _searchResetButton;

        [SerializeField] private CraftItemView _craftItemViewPrefab;
        [SerializeField] private Transform _craftItemsContainer;
        [SerializeField] private CraftRequiredItemView _craftRequiredItemViewPrefab;
        [SerializeField] private Transform _craftRequirementItemsContainer;
        [SerializeField] private ParameterItemView _parameterPrefab;
        [SerializeField] private Transform _parametersContainer;

        [SerializeField] private TMP_Text _craftItemName;
        [SerializeField] private TMP_Text _craftItemCategory;
        [SerializeField] private TMP_Text _craftItemDescription;

        [SerializeField] private TMP_InputField _craftCountInputField;
        [SerializeField] private Button _craftButton;
        [SerializeField] private Button _craftCountAddButton;
        [SerializeField] private Button _craftCountSubtractButton;
        [SerializeField] private Button _craftMaxButton;

        public event Action Closed;

        private CraftCategory _selectedCategory = CraftCategory.Meds;
        private string _selectedRecipeId;
        private int _craftCount = 1;
        private string _searchText = string.Empty;

        private readonly List<CraftItemView> _craftItemViews = new();
        private readonly List<CraftRequiredItemView> _requirementViews = new();
        private readonly List<ParameterItemView> _parameterViews = new();

        protected override void Awake()
        {
            base.Awake();

            _closeButton.onClick.AddListener(RequestClose);

            _weaponTabButton.Button.onClick.AddListener(() => SelectCategory(CraftCategory.Weapons));
            _medsTabButton.Button.onClick.AddListener(() => SelectCategory(CraftCategory.Meds));
            _modsTabButton.Button.onClick.AddListener(() => SelectCategory(CraftCategory.WeaponMods));
            _ammoTabButton.Button.onClick.AddListener(() => SelectCategory(CraftCategory.Ammo));

            _searchInputField.onValueChanged.AddListener(OnSearchChanged);
            _searchResetButton.onClick.AddListener(ClearSearch);

            _craftButton.onClick.AddListener(OnCraftClicked);
            _craftCountAddButton.onClick.AddListener(() => ChangeCraftCount(1));
            _craftCountSubtractButton.onClick.AddListener(() => ChangeCraftCount(-1));
            _craftMaxButton.onClick.AddListener(SetCraftCountToMax);

            _craftCountInputField.onEndEdit.AddListener(OnCraftCountEdited);
        }

        public void Open()
        {
            _craftCount = 1;
            _selectedRecipeId = null;
            SelectFirstRecipe();
            RefreshAll();
        }

        public override void Hide()
        {
            base.Hide();
            Closed?.Invoke();
        }

        public void RequestClose()
        {
            Hide();
        }

        void Update()
        {
            if (!IsOpen) return;
            RefreshDynamicData();
        }

        void SelectCategory(CraftCategory category)
        {
            _selectedCategory = category;
            _selectedRecipeId = null;
            SelectFirstRecipe();
            RefreshAll();
        }

        void SelectFirstRecipe()
        {
            var recipes = GetFilteredRecipes();
            _selectedRecipeId = recipes.Count > 0 ? recipes[0].RecipeId : null;
        }

        void OnSearchChanged(string value)
        {
            _searchText = value ?? string.Empty;
            _selectedRecipeId = null;
            SelectFirstRecipe();
            RefreshAll();
        }

        void ClearSearch()
        {
            _searchText = string.Empty;
            _searchInputField.SetTextWithoutNotify(string.Empty);
            _selectedRecipeId = null;
            SelectFirstRecipe();
            RefreshAll();
        }

        void ChangeCraftCount(int delta)
        {
            _craftCount = Mathf.Max(1, _craftCount + delta);
            SyncCraftCountField();
        }

        void SetCraftCountToMax()
        {
            var inv = App.Instance?.Player?.Inventory;
            if (inv == null || !CraftConstants.TryGet(_selectedRecipeId ?? string.Empty, out var recipe))
                return;

            int max = ComputeMaxCraftCount(inv, in recipe);
            _craftCount = Mathf.Max(1, max);
            SyncCraftCountField();
        }

        void OnCraftCountEdited(string value)
        {
            if (int.TryParse(value, out int parsed))
                _craftCount = Mathf.Max(1, parsed);
            SyncCraftCountField();
        }

        void OnCraftClicked()
        {
            var session = App.Instance?.RaidSession;
            if (session == null) return;
            if (!CraftConstants.TryGet(_selectedRecipeId ?? string.Empty, out var recipe)) return;

            var inv = App.Instance.Player.Inventory;
            int times = Mathf.Min(_craftCount, ComputeMaxCraftCount(inv, in recipe));
            for (int i = 0; i < times; i++)
            {
                if (!CraftingSystem.CanCraft(inv, in recipe)) break;
                session.RequestCraft(recipe.RecipeId);
            }

            RefreshAll();
        }

        void RefreshAll()
        {
            RefreshTabs();
            RebuildRecipeList();
            RebuildDetailPanel();
            SyncCraftCountField();
        }

        void RefreshDynamicData()
        {
            var inv = App.Instance?.Player?.Inventory;
            if (inv == null) return;

            foreach (var view in _craftItemViews)
                view.Refresh(inv);

            foreach (var view in _requirementViews)
                view.Refresh(inv);

            RefreshCraftButton(inv);
        }

        void RefreshTabs()
        {
            _medsTabButton.SetSelected(_selectedCategory == CraftCategory.Meds);
            _weaponTabButton.SetSelected(_selectedCategory == CraftCategory.Weapons);
            _ammoTabButton.SetSelected(_selectedCategory == CraftCategory.Ammo);
            _modsTabButton.SetSelected(_selectedCategory == CraftCategory.WeaponMods);
        }

        void RebuildRecipeList()
        {
            foreach (var v in _craftItemViews)
                Destroy(v.gameObject);
            _craftItemViews.Clear();

            var inv = App.Instance?.Player?.Inventory;
            var recipes = GetFilteredRecipes();

            foreach (var recipe in recipes)
            {
                var view = Instantiate(_craftItemViewPrefab, _craftItemsContainer);
                view.Bind(recipe);
                if (inv != null) view.Refresh(inv);
                view.SetSelected(recipe.RecipeId == _selectedRecipeId);
                view.Clicked += OnRecipeClicked;
                _craftItemViews.Add(view);
            }
        }

        void OnRecipeClicked(CraftItemView view)
        {
            _selectedRecipeId = view.Recipe.RecipeId;
            _craftCount = 1;

            foreach (var v in _craftItemViews)
                v.SetSelected(v == view);

            RebuildDetailPanel();
            SyncCraftCountField();
        }

        void RebuildDetailPanel()
        {
            foreach (var v in _requirementViews)
                Destroy(v.gameObject);
            _requirementViews.Clear();

            foreach (var v in _parameterViews)
                Destroy(v.gameObject);
            _parameterViews.Clear();

            if (!CraftConstants.TryGet(_selectedRecipeId ?? string.Empty, out var recipe))
            {
                _craftItemName.text = string.Empty;
                _craftItemCategory.text = string.Empty;
                _craftItemDescription.text = string.Empty;
                _craftButton.interactable = false;
                return;
            }

            _craftItemName.text = recipe.DisplayName;
            _craftItemCategory.text = recipe.Category.ToString();
            _craftItemDescription.text = recipe.Description;

            var inv = App.Instance?.Player?.Inventory;

            foreach (var ingredient in recipe.Ingredients)
            {
                var view = Instantiate(_craftRequiredItemViewPrefab, _craftRequirementItemsContainer);
                view.Bind(ingredient);
                if (inv != null) view.Refresh(inv);
                _requirementViews.Add(view);
            }

            var resultDef = ItemDefinition.Get(recipe.ResultItemId);
            if (resultDef != null)
            {
                foreach (var (name, value) in BuildParameters(resultDef, recipe))
                {
                    var view = Instantiate(_parameterPrefab, _parametersContainer);
                    view.Bind(name, value);
                    _parameterViews.Add(view);
                }
            }

            if (inv != null)
                RefreshCraftButton(inv);
        }

        void RefreshCraftButton(InventoryState inv)
        {
            if (!CraftConstants.TryGet(_selectedRecipeId ?? string.Empty, out var recipe))
            {
                _craftButton.interactable = false;
                return;
            }
            _craftButton.interactable = CraftingSystem.CanCraft(inv, in recipe);
        }

        void SyncCraftCountField()
        {
            _craftCountInputField.SetTextWithoutNotify(_craftCount.ToString());
        }

        IReadOnlyList<CraftRecipe> GetFilteredRecipes()
        {
            var all = CraftConstants.GetByCategory(_selectedCategory);
            if (string.IsNullOrEmpty(_searchText))
                return all;

            var filtered = new List<CraftRecipe>();
            foreach (var r in all)
                if (r.DisplayName.IndexOf(_searchText, StringComparison.OrdinalIgnoreCase) >= 0)
                    filtered.Add(r);
            return filtered;
        }

        static int ComputeMaxCraftCount(InventoryState inv, in CraftRecipe recipe)
        {
            int max = int.MaxValue;
            foreach (var ingredient in recipe.Ingredients)
            {
                int have = CraftingSystem.CountIngredient(inv, ingredient.DefinitionId);
                int possible = ingredient.Count > 0 ? have / ingredient.Count : int.MaxValue;
                if (possible < max) max = possible;
            }
            return max == int.MaxValue ? 0 : max;
        }

        static List<(string name, string value)> BuildParameters(ItemDefinition def, in CraftRecipe recipe)
        {
            var list = new List<(string, string)>();

            if (recipe.ResultCount > 1)
                list.Add(("Produces", recipe.ResultCount.ToString()));

            if (def.ArmorPoints > 0)
                list.Add(("Armor", def.ArmorPoints.ToString("0")));

            if (def.MaxDurability > 0)
                list.Add(("Durability", def.MaxDurability.ToString("0")));

            if (def.Penetration > 0)
                list.Add(("Penetration", def.Penetration.ToString("0")));

            if (def.ArmorDamage > 0)
                list.Add(("Armor Dmg", def.ArmorDamage.ToString("0")));

            if (def.BleedChance > 0)
                list.Add(("Bleed", $"{(def.BleedChance * 100f):0}%"));

            if (def.MaxStackSize > 1)
                list.Add(("Stack", def.MaxStackSize.ToString()));

            return list;
        }
    }
}