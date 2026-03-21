using System.Collections.Generic;
using Constants;
using State;
using Systems;
using UnityEngine;

namespace View
{
    public class CraftingUI : MonoBehaviour
    {
        const float ScreenMargin = 20f;
        const float PanelGap = 10f;

        static readonly CraftCategory[] Categories =
        {
            CraftCategory.Meds,
            CraftCategory.Weapons,
            CraftCategory.Ammo,
            CraftCategory.WeaponMods,
        };

        static readonly string[] CategoryLabels = { "MEDS", "WEAPONS", "AMMO", "MODS" };

        bool _isOpen;

        CraftCategory _selectedCategory = CraftCategory.Meds;
        string _selectedRecipeId;

        Texture2D _panelBg;
        Texture2D _darkBg;
        Texture2D _slotBg;
        Texture2D _slotSelected;
        Texture2D _categoryBg;
        Texture2D _categoryActive;
        Texture2D _craftBtnBg;
        Texture2D _craftBtnDisabled;
        Texture2D _ingredientOk;
        Texture2D _ingredientMissing;
        Texture2D _promptBg;

        GUIStyle _headerStyle;
        GUIStyle _labelStyle;
        GUIStyle _recipeStyle;
        GUIStyle _recipeSelectedStyle;
        GUIStyle _categoryStyle;
        GUIStyle _categoryActiveStyle;
        GUIStyle _descStyle;
        GUIStyle _craftBtnStyle;
        GUIStyle _ingredientStyle;
        GUIStyle _ingredientCountStyle;
        GUIStyle _ingredientCountMissingStyle;
        GUIStyle _promptStyle;
        GUIStyle _ownedStyle;

        Vector2 _recipeScrollPos;

        void Awake()
        {
            _panelBg = MakeTex(new Color(0.12f, 0.12f, 0.14f, 0.95f));
            _darkBg = MakeTex(new Color(0.08f, 0.08f, 0.10f, 0.95f));
            _slotBg = MakeTex(new Color(0.18f, 0.18f, 0.20f, 0.9f));
            _slotSelected = MakeTex(new Color(0.3f, 0.5f, 0.25f, 0.9f));
            _categoryBg = MakeTex(new Color(0.2f, 0.2f, 0.22f, 0.9f));
            _categoryActive = MakeTex(new Color(0.35f, 0.55f, 0.3f, 0.9f));
            _craftBtnBg = MakeTex(new Color(0.25f, 0.6f, 0.25f, 0.9f));
            _craftBtnDisabled = MakeTex(new Color(0.3f, 0.3f, 0.3f, 0.7f));
            _ingredientOk = MakeTex(new Color(0.15f, 0.22f, 0.15f, 0.8f));
            _ingredientMissing = MakeTex(new Color(0.25f, 0.12f, 0.12f, 0.8f));
            _promptBg = MakeTex(new Color(0.1f, 0.1f, 0.1f, 0.8f));
        }

        void Update()
        {
            var session = App.App.Instance?.RaidSession;
            var player = session?.RaidState?.PlayerEntity;
            if (player == null) return;

            bool shouldBeOpen = player.CraftTargetId != EId.None;

            if (shouldBeOpen && !_isOpen)
            {
                _isOpen = true;
                _selectedRecipeId = null;
                SelectFirstRecipe();
            }
            else if (!shouldBeOpen && _isOpen)
            {
                _isOpen = false;
            }
        }

        void OnGUI()
        {
            var session = App.App.Instance?.RaidSession;
            if (session == null) return;
            var state = session.RaidState;
            if (state?.PlayerEntity == null) return;

            if (!_isOpen)
            {
                DrawCraftPrompt(state, state.PlayerEntity);
                return;
            }

            EnsureStyles();

            float totalW = Screen.width - ScreenMargin * 2f;
            float totalH = Screen.height - ScreenMargin * 2f;
            float leftW = totalW * 0.25f;
            float centerW = totalW * 0.35f;
            float rightW = totalW - leftW - centerW - PanelGap * 2f;

            float x = ScreenMargin;
            float y = ScreenMargin;

            var leftRect = new Rect(x, y, leftW, totalH);
            var centerRect = new Rect(x + leftW + PanelGap, y, centerW, totalH);
            var rightRect = new Rect(x + leftW + centerW + PanelGap * 2f, y, rightW, totalH);

            GUI.DrawTexture(leftRect, _panelBg);
            GUI.DrawTexture(centerRect, _panelBg);
            GUI.DrawTexture(rightRect, _panelBg);

            DrawLeftPanel(leftRect, state);
            DrawCenterPanel(centerRect, state, session);
            DrawRightPanel(rightRect, state);
        }

        // ── Left Panel: Categories + Recipe List ─────────────────

        void DrawLeftPanel(Rect panel, RaidState state)
        {
            float pad = panel.width * 0.04f;
            float cx = panel.x + pad;
            float cy = panel.y + pad;
            float availW = panel.width - pad * 2f;

            float headerH = 40f;
            GUI.Label(new Rect(cx, cy, availW, headerH), "CRAFTING", _headerStyle);
            cy += headerH + 10f;

            float catBtnH = 36f;
            float catGap = 6f;
            float catW = (availW - catGap * (Categories.Length - 1)) / Categories.Length;

            for (int i = 0; i < Categories.Length; i++)
            {
                var btnRect = new Rect(cx + i * (catW + catGap), cy, catW, catBtnH);
                var style = Categories[i] == _selectedCategory ? _categoryActiveStyle : _categoryStyle;
                if (GUI.Button(btnRect, CategoryLabels[i], style))
                {
                    _selectedCategory = Categories[i];
                    _selectedRecipeId = null;
                    SelectFirstRecipe();
                }
            }
            cy += catBtnH + 12f;

            float listH = panel.y + panel.height - cy - pad;
            var recipes = CraftConstants.GetByCategory(_selectedCategory);

            float rowH = 48f;
            float contentH = recipes.Count * (rowH + 5f);
            var contentRect = new Rect(0f, 0f, availW - 16f, Mathf.Max(contentH, listH));

            _recipeScrollPos = GUI.BeginScrollView(
                new Rect(cx, cy, availW, listH), _recipeScrollPos, contentRect);

            float ry = 0f;
            for (int i = 0; i < recipes.Count; i++)
            {
                var recipe = recipes[i];
                bool selected = recipe.RecipeId == _selectedRecipeId;
                var rowRect = new Rect(0f, ry, contentRect.width, rowH);

                var style = selected ? _recipeSelectedStyle : _recipeStyle;
                int owned = CountOwned(state.Inventory, recipe.ResultItemId);
                string label = owned > 0
                    ? $"  {recipe.DisplayName}    Owned: {owned}"
                    : $"  {recipe.DisplayName}";

                if (GUI.Button(rowRect, label, style))
                    _selectedRecipeId = recipe.RecipeId;

                ry += rowH + 5f;
            }

            GUI.EndScrollView();
        }

        // ── Center Panel: Selected Recipe Details ────────────────

        void DrawCenterPanel(Rect panel, RaidState state, Session.RaidSession session)
        {
            float pad = panel.width * 0.06f;
            float cx = panel.x + pad;
            float cy = panel.y + pad;
            float availW = panel.width - pad * 2f;

            if (!CraftConstants.TryGet(_selectedRecipeId ?? "", out var recipe))
            {
                GUI.Label(new Rect(cx, cy, availW, 40f), "Select a recipe", _labelStyle);
                return;
            }

            float headerH = 44f;
            GUI.Label(new Rect(cx, cy, availW, headerH), recipe.DisplayName.ToUpper(), _headerStyle);
            cy += headerH + 10f;

            float descH = 70f;
            GUI.Label(new Rect(cx, cy, availW, descH), recipe.Description, _descStyle);
            cy += descH + 16f;

            var resultDef = ItemDefinition.Get(recipe.ResultItemId);
            if (resultDef != null)
            {
                string resultText = recipe.ResultCount > 1
                    ? $"Produces: {resultDef.DisplayName} x{recipe.ResultCount}"
                    : $"Produces: {resultDef.DisplayName}";
                GUI.Label(new Rect(cx, cy, availW, 32f), resultText, _labelStyle);
                cy += 40f;
            }

            cy = panel.y + panel.height - pad - 60f;

            bool canCraft = CraftingSystem.CanCraft(state.Inventory, in recipe);
            var btnRect = new Rect(cx, cy, availW, 54f);

            var prevBg = GUI.backgroundColor;
            _craftBtnStyle.normal.background = canCraft ? _craftBtnBg : _craftBtnDisabled;

            GUI.enabled = canCraft;
            if (GUI.Button(btnRect, "CRAFT", _craftBtnStyle))
            {
                session.RequestCraft(recipe.RecipeId);
            }
            GUI.enabled = true;
            GUI.backgroundColor = prevBg;
        }

        // ── Right Panel: Required Materials ──────────────────────

        void DrawRightPanel(Rect panel, RaidState state)
        {
            float pad = panel.width * 0.06f;
            float cx = panel.x + pad;
            float cy = panel.y + pad;
            float availW = panel.width - pad * 2f;

            float headerH = 40f;
            GUI.Label(new Rect(cx, cy, availW * 0.5f, headerH), "REQUIRES", _headerStyle);
            GUI.Label(new Rect(cx + availW * 0.55f, cy, availW * 0.45f, headerH), "YOU HAVE", _headerStyle);
            cy += headerH + 12f;

            if (!CraftConstants.TryGet(_selectedRecipeId ?? "", out var recipe))
                return;

            float rowH = 60f;
            float rowGap = 8f;

            for (int i = 0; i < recipe.Ingredients.Length; i++)
            {
                var ingredient = recipe.Ingredients[i];
                int have = CraftingSystem.CountIngredient(state.Inventory, ingredient.DefinitionId);
                bool met = have >= ingredient.Count;

                var rowRect = new Rect(cx, cy, availW, rowH);
                GUI.DrawTexture(rowRect, met ? _ingredientOk : _ingredientMissing);

                var def = ItemDefinition.Get(ingredient.DefinitionId);
                string itemName = def != null ? def.DisplayName : ingredient.DefinitionId;

                var nameRect = new Rect(cx + 10f, cy + 4f, availW * 0.55f - 10f, rowH - 8f);
                GUI.Label(nameRect, itemName, _ingredientStyle);

                var countStyle = met ? _ingredientCountStyle : _ingredientCountMissingStyle;
                string countText = $"{have} / {ingredient.Count}";
                var countRect = new Rect(cx + availW * 0.55f, cy + 4f, availW * 0.4f, rowH - 8f);
                GUI.Label(countRect, countText, countStyle);

                cy += rowH + rowGap;
            }
        }

        // ── Craft Prompt (when not open) ─────────────────────────

        void DrawCraftPrompt(RaidState state, PlayerEntityState player)
        {
            if (player.LootTargetId != EId.None) return;
            if (player.CraftTargetId != EId.None) return;

            var nearest = LootSystem.FindNearestInteractable(state, player.Position, player.FacingDirection);
            if (nearest.Type != InteractableType.Workbench) return;

            EnsureStyles();

            float w = 200f;
            float h = 32f;
            float x = (Screen.width - w) * 0.5f;
            float y = Screen.height * 0.65f;

            var rect = new Rect(x, y, w, h);
            GUI.DrawTexture(rect, _promptBg);

            if (_promptStyle == null)
            {
                _promptStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 16,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter,
                };
                _promptStyle.normal.textColor = new Color(1f, 0.9f, 0.6f);
            }

            GUI.Label(rect, "Press F to craft", _promptStyle);
        }

        // ── Helpers ──────────────────────────────────────────────

        void SelectFirstRecipe()
        {
            var recipes = CraftConstants.GetByCategory(_selectedCategory);
            _selectedRecipeId = recipes.Count > 0 ? recipes[0].RecipeId : null;
        }

        static int CountOwned(InventoryState inv, string definitionId)
        {
            int count = 0;
            for (int i = 0; i < InventoryState.BackpackSize; i++)
            {
                var item = inv.Backpack[i];
                if (item != null && item.DefinitionId == definitionId)
                    count += item.StackCount;
            }
            for (int i = 0; i < InventoryState.WeaponSlotCount; i++)
            {
                if (inv.WeaponSlots[i] != null && inv.WeaponSlots[i].DefinitionId == definitionId)
                    count++;
            }
            return count;
        }

        void EnsureStyles()
        {
            if (_headerStyle != null) return;

            _headerStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 28,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
            };
            _headerStyle.normal.textColor = Color.white;

            _labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 20,
                alignment = TextAnchor.MiddleLeft,
            };
            _labelStyle.normal.textColor = new Color(0.85f, 0.85f, 0.85f);

            _descStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 18,
                wordWrap = true,
                alignment = TextAnchor.UpperLeft,
            };
            _descStyle.normal.textColor = new Color(0.7f, 0.7f, 0.7f);

            _recipeStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 20,
                alignment = TextAnchor.MiddleLeft,
                fontStyle = FontStyle.Normal,
            };
            _recipeStyle.normal.background = _slotBg;
            _recipeStyle.normal.textColor = new Color(0.9f, 0.9f, 0.9f);
            _recipeStyle.hover.background = _slotSelected;
            _recipeStyle.hover.textColor = Color.white;
            _recipeStyle.active.background = _slotSelected;

            _recipeSelectedStyle = new GUIStyle(_recipeStyle);
            _recipeSelectedStyle.normal.background = _slotSelected;
            _recipeSelectedStyle.normal.textColor = Color.white;

            _categoryStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 16,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
            };
            _categoryStyle.normal.background = _categoryBg;
            _categoryStyle.normal.textColor = new Color(0.8f, 0.8f, 0.8f);
            _categoryStyle.hover.background = _categoryActive;

            _categoryActiveStyle = new GUIStyle(_categoryStyle);
            _categoryActiveStyle.normal.background = _categoryActive;
            _categoryActiveStyle.normal.textColor = Color.white;

            _craftBtnStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 28,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
            };
            _craftBtnStyle.normal.background = _craftBtnBg;
            _craftBtnStyle.normal.textColor = Color.white;
            _craftBtnStyle.hover.background = _craftBtnBg;

            _ingredientStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 22,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
            };
            _ingredientStyle.normal.textColor = Color.white;

            _ingredientCountStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 24,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleRight,
            };
            _ingredientCountStyle.normal.textColor = new Color(0.3f, 0.9f, 0.3f);

            _ingredientCountMissingStyle = new GUIStyle(_ingredientCountStyle);
            _ingredientCountMissingStyle.normal.textColor = new Color(0.9f, 0.3f, 0.3f);

            _ownedStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 16,
                alignment = TextAnchor.MiddleRight,
            };
            _ownedStyle.normal.textColor = new Color(0.6f, 0.6f, 0.6f);
        }

        static Texture2D MakeTex(Color color)
        {
            var tex = new Texture2D(1, 1);
            tex.SetPixel(0, 0, color);
            tex.Apply();
            return tex;
        }

        void OnDestroy()
        {
            if (_panelBg != null) Destroy(_panelBg);
            if (_darkBg != null) Destroy(_darkBg);
            if (_slotBg != null) Destroy(_slotBg);
            if (_slotSelected != null) Destroy(_slotSelected);
            if (_categoryBg != null) Destroy(_categoryBg);
            if (_categoryActive != null) Destroy(_categoryActive);
            if (_craftBtnBg != null) Destroy(_craftBtnBg);
            if (_craftBtnDisabled != null) Destroy(_craftBtnDisabled);
            if (_ingredientOk != null) Destroy(_ingredientOk);
            if (_ingredientMissing != null) Destroy(_ingredientMissing);
            if (_promptBg != null) Destroy(_promptBg);
        }
    }
}
