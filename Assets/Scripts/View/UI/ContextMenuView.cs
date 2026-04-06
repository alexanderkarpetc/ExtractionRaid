using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace View.UI
{
    [RequireComponent(typeof(CanvasGroup))]
    public class ContextMenuView : MonoBehaviour
    {
        [SerializeField] Button _contextButton;
        [SerializeField] RectTransform _container;

        [Header("Row Colors")]
        [SerializeField] Color _normalLabelColor = new Color(1f, 1f, 1f, 0.75f);
        [SerializeField] Color _normalHintColor = new Color(1f, 1f, 1f, 0.28f);
        [SerializeField] Color _hoverBackgroundColor = new Color(0.878f, 0.314f, 0.251f, 0.10f);
        [SerializeField] Color _hoverLabelColor = new Color(0.878f, 0.314f, 0.251f, 1f);
        [SerializeField] Color _hoverHintColor = new Color(0.878f, 0.314f, 0.251f, 0.7f);

        CanvasGroup _canvasGroup;
        Canvas _rootCanvas;

        readonly struct Entry
        {
            public readonly Button Button;
            public readonly TMP_Text Label;
            public readonly TMP_Text Hint;
            public Entry(Button b, TMP_Text l, TMP_Text h) { Button = b; Label = l; Hint = h; }
        }

        Entry[] _entries;
        int _activeCount;
        Action<int> _callback;

        void Awake()
        {
            _canvasGroup = GetComponent<CanvasGroup>();
            _rootCanvas = GetComponentInParent<Canvas>()?.rootCanvas;
            _contextButton.gameObject.SetActive(false);
            HideImmediate();
        }

        /// <param name="position">Screen-space position where the menu should appear.</param>
        /// <param name="options">Button labels to show.</param>
        /// <param name="hints">Keyboard shortcut labels shown beside each option.</param>
        /// <param name="onSelect">Callback with the chosen option index.</param>
        public void Show(Vector2 position, string[] options, string[] hints, Action<int> onSelect)
        {
            _callback = onSelect;
            EnsureEntries(options.Length);

            for (int i = 0; i < options.Length; i++)
            {
                var e = _entries[i];
                e.Label.text = options[i];
                e.Label.color = _normalLabelColor;
                if (e.Hint != null)
                {
                    e.Hint.text = i < hints.Length ? hints[i] : string.Empty;
                    e.Hint.color = _normalHintColor;
                }
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
                var go = Instantiate(_contextButton.gameObject, _container);
                go.SetActive(false);
                var btn = go.GetComponent<Button>();
                var labels = go.GetComponentsInChildren<TMP_Text>();
                var lbl = labels.Length > 0 ? labels[0] : null;
                var hint = labels.Length > 1 ? labels[1] : null;
                SetupButtonColors(btn, lbl, hint);
                _entries[i] = new Entry(btn, lbl, hint);
            }
        }

        void SetupButtonColors(Button btn, TMP_Text lbl, TMP_Text hint)
        {
            var colors = btn.colors;
            colors.highlightedColor = _hoverBackgroundColor;
            colors.pressedColor = new Color(_hoverBackgroundColor.r, _hoverBackgroundColor.g, _hoverBackgroundColor.b,
                Mathf.Clamp01(_hoverBackgroundColor.a * 2.5f));
            colors.selectedColor = _hoverBackgroundColor;
            colors.colorMultiplier = 1f;
            btn.colors = colors;

            var trigger = btn.GetComponent<EventTrigger>() ?? btn.gameObject.AddComponent<EventTrigger>();
            trigger.triggers.Clear();

            var onEnter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
            onEnter.callback.AddListener(_ =>
            {
                if (lbl != null) lbl.color = _hoverLabelColor;
                if (hint != null) hint.color = _hoverHintColor;
            });
            trigger.triggers.Add(onEnter);

            var onExit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
            onExit.callback.AddListener(_ =>
            {
                if (lbl != null) lbl.color = _normalLabelColor;
                if (hint != null) hint.color = _normalHintColor;
            });
            trigger.triggers.Add(onExit);
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
