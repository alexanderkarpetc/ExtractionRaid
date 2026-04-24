using TMPro;
using UnityEngine;

namespace View.UI.Craft
{
    public class ParameterItemView : MonoBehaviour
    {
        [SerializeField] private TMP_Text _parameterName;
        [SerializeField] private TMP_Text _parameterValue;

        public void Bind(string paramName, string paramValue)
        {
            if (_parameterName != null) _parameterName.text = paramName;
            if (_parameterValue != null) _parameterValue.text = paramValue;
        }
    }
}