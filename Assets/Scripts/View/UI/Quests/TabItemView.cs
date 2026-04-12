using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace View.UI.Quests
{
    public class TabItemView : MonoBehaviour
    {
        [SerializeField] Button _button;
        [SerializeField] GameObject _activeIndicator;
        [SerializeField] Color _activeTextColor = Color.white;
        [SerializeField] Color _inactiveTextColor = Color.gray;
        [SerializeField] TMP_Text _text;

        public Button Button => _button;

        public void SetSelected(bool selected)
        {
            if (_activeIndicator != null)
                _activeIndicator.SetActive(selected);

            if (_text != null)
                _text.color = selected ? _activeTextColor : _inactiveTextColor;
        }
    }
}