using TMPro;
using UnityEngine;
using UnityEngine.UI;
using View.UI.Common;

namespace View.UI.Craft
{
    public class CraftPopupView : MonoBehaviour
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
    }
}