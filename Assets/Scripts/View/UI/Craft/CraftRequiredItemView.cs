using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace View.UI.Craft
{
    public class CraftRequiredItemView : MonoBehaviour
    {
        [SerializeField] private TMP_Text _itemName;
        [SerializeField] private TMP_Text _itemCount; // format "current / required"
        [SerializeField] private Image _borderImage;
        [SerializeField] private Color _enoughColor; // apply to border image and item count
        [SerializeField] private Color _notEnoughColor; // apply to border image and item count
        
    }
}