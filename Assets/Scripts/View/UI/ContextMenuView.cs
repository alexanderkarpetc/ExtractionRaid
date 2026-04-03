using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace View.UI
{
    [RequireComponent(typeof(CanvasGroup))]
    public class ContextMenuView : MonoBehaviour
    {
        [SerializeField] Button _buttonTemplate;
        [SerializeField] RectTransform _container;

        CanvasGroup _canvasGroup;
        Canvas _rootCanvas;

        readonly struct Entry
        {
            public readonly Button Button;
            public readonly TMP_Text Label;
            public Entry(Button b, TMP_Text l) { Button = b; Label = l; }
        }

        Entry[] _entries;
        int _activeCount;
        Action<int> _callback;

        void Awake()
        {
            _canvasGroup = GetComponent<CanvasGroup>();
            _rootCanvas = GetComponentInParent<Canvas>()?.rootCanvas;
            if (_buttonTemplate != null)
                _buttonTemplate.gameObject.SetActive(false);
            HideImmediate();
        }

        /// <param name="position">Screen-space position where the menu should appear.</param>
        /// <param name="options">Button labels to show.</param>
        /// <param name="onSelect">Callback with the chosen option index.</param>
        public void Show(Vector2 position, string[] options, Action<int> onSelect)
        {
            _callback = onSelect;
            EnsureEntries(options.Length);

            for (int i = 0; i < options.Length; i++)
            {
                var e = _entries[i];
                e.Label.text = options[i];
                e.Button.gameObject.SetActive(true);
                int idx = i;
                e.Button.onClick.RemoveAllListeners();
                e.Button.onClick.AddListener(() => OnOptionClicked(idx));
            }
            for (int i = options.Length; i < _activeCount; i++)
                _entries[i].Button.gameObject.SetActive(false);
            _activeCount = options.Length;

            PositionAt(position);
            _canvasGroup.alpha = 1f;
            _canvasGroup.blocksRaycasts = true;
            _canvasGroup.interactable = true;
        }

        public void Hide()
        {
            HideImmediate();
            _callback = null;
        }

        public bool IsVisible => _canvasGroup != null && _canvasGroup.alpha > 0f;

        void OnOptionClicked(int index)
        {
            var cb = _callback;
            Hide();
            cb?.Invoke(index);
        }

        void PositionAt(Vector2 screenPos)
        {
            if (_rootCanvas == null) return;

            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _rootCanvas.transform as RectTransform,
                screenPos,
                _rootCanvas.worldCamera,
                out var localPoint);

            ((RectTransform)transform).localPosition = localPoint;
        }

        void EnsureEntries(int count)
        {
            if (_entries != null && _entries.Length >= count) return;

            int oldLen = _entries?.Length ?? 0;
            var prev = _entries;
            _entries = new Entry[count];
            if (prev != null)
                Array.Copy(prev, _entries, oldLen);

            for (int i = oldLen; i < count; i++)
            {
                var go = Instantiate(_buttonTemplate.gameObject, _container);
                go.SetActive(false);
                var btn = go.GetComponent<Button>();
                var lbl = go.GetComponentInChildren<TMP_Text>();
                _entries[i] = new Entry(btn, lbl);
            }
        }

        void HideImmediate()
        {
            if (_canvasGroup == null) return;
            _canvasGroup.alpha = 0f;
            _canvasGroup.blocksRaycasts = false;
            _canvasGroup.interactable = false;
        }
    }
}
