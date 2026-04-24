using System;
using Constants;
using State;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace View.UI.Craft
{
    public class CraftItemView : MonoBehaviour
    {
        [SerializeField] private Button _button;
        [SerializeField] private TMP_Text _itemName;
        [SerializeField] private TMP_Text _itemCurrentCount;
        [SerializeField] private Image _borderImage;
        [SerializeField] private Color _selectedColorForBorder;
        [SerializeField] private Color _regularColorForBorder;

        private CraftRecipe _recipe;

        public CraftRecipe Recipe => _recipe;
        public event Action<CraftItemView> Clicked;

        void Awake()
        {
            _button.onClick.AddListener(() => Clicked?.Invoke(this));
        }

        public void Bind(in CraftRecipe recipe)
        {
            _recipe = recipe;
            _itemName.text = recipe.DisplayName;
        }

        public void Refresh(InventoryState inventory)
        {
            int owned = CountOwned(inventory, _recipe.ResultItemId);
            _itemCurrentCount.text = owned > 0 ? owned.ToString() : string.Empty;
        }

        public void SetSelected(bool selected)
        {
            _borderImage.color = selected ? _selectedColorForBorder : _regularColorForBorder;
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
    }
}