using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace View.UI.Inventory
{
    public class InventoryNavigationButtonView : MonoBehaviour
    {
        [SerializeField] Button _button;
        [SerializeField] TMP_Text _buttonText;
        [SerializeField] Image _backgroundImage;
        [SerializeField] Image _borderImage;
        [SerializeField] Color _activeBackgroundColor;
        [SerializeField] Color _inactiveBackgroundColor;
        [SerializeField] Color _activeBorderColor;
        [SerializeField] Color _inactiveBorderColor;

        Action _onClick;
        bool _wired;

        public void Bind(int displayNumber, bool isActive, Action onClick)
        {
            _onClick = onClick;
            SetNumber(displayNumber);
            SetActiveState(isActive);

            if (!_wired && _button != null)
            {
                _button.onClick.AddListener(OnClickedInternal);
                _wired = true;
            }
        }

        public void SetNumber(int displayNumber)
        {
            if (_buttonText != null)
                _buttonText.text = displayNumber.ToString();
        }

        public void SetActiveState(bool isActive)
        {
            if (_backgroundImage != null)
                _backgroundImage.color = isActive ? _activeBackgroundColor : _inactiveBackgroundColor;
            if (_borderImage != null)
                _borderImage.color = isActive ? _activeBorderColor : _inactiveBorderColor;
        }

        void OnClickedInternal() => _onClick?.Invoke();
    }
}
