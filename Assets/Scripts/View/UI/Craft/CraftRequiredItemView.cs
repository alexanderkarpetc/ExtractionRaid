using Constants;
using State;
using Systems;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace View.UI.Craft
{
    public class CraftRequiredItemView : MonoBehaviour
    {
        [SerializeField] private TMP_Text _itemName;
        [SerializeField] private TMP_Text _itemCount;
        [SerializeField] private Image _borderImage;
        [SerializeField] private Color _enoughColor;
        [SerializeField] private Color _notEnoughColor;

        private CraftIngredient _ingredient;

        public void Bind(in CraftIngredient ingredient)
        {
            _ingredient = ingredient;
            if (_itemName != null)
            {
                var def = State.ItemDefinition.Get(ingredient.DefinitionId);
                _itemName.text = def != null ? def.DisplayName : ingredient.DefinitionId;
            }
        }

        public void Refresh(InventoryState inventory)
        {
            int have = CraftingSystem.CountIngredient(inventory, _ingredient.DefinitionId);
            bool enough = have >= _ingredient.Count;

            if (_itemCount != null)
            {
                _itemCount.text = $"{have} / {_ingredient.Count}";
                _itemCount.color = enough ? _enoughColor : _notEnoughColor;
            }

            if (_borderImage != null)
                _borderImage.color = enough ? _enoughColor : _notEnoughColor;
        }
    }
}