using UnityEngine;

namespace View.UI
{
    public class PopupManager : MonoBehaviour
    {
        PopupBase _current;

        public PopupBase Current => _current;
        public bool IsAnyOpen => _current != null && _current.IsOpen;

        public void Open(PopupBase popup)
        {
            if (_current != null && _current != popup)
                _current.Hide();

            _current = popup;
            _current.Show();
        }

        public void Close()
        {
            if (_current == null) return;
            _current.Hide();
            _current = null;
        }

        public bool IsOpen(PopupBase popup) => _current == popup && popup != null && popup.IsOpen;
    }
}
