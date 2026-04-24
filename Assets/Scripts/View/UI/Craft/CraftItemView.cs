using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace View.UI.Craft
{
    public class CraftItemView : MonoBehaviour
    {
        [SerializeField] private TMP_Text _itemName;
        [SerializeField] private TMP_Text _itemCurrentCount;
        [SerializeField] private Image _borderImage;
        [SerializeField] private Color _selectedColorForBorder;
        [SerializeField] private Color _regularColorForBorder;
    }
}