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
        [SerializeField] Button _button;

        [Header("Row Colors")]
        [SerializeField] Color _normalLabelColor = new Color(1f, 1f, 1f, 0.75f);
        [SerializeField] Color _hoverBackgroundColor = new Color(0.878f, 0.314f, 0.251f, 0.10f);
        [SerializeField] Color _hoverLabelColor = new Color(0.878f, 0.314f, 0.251f, 1f);

        CanvasGroup _canvasGroup;
        Canvas _rootCanvas;
        TMP_Text _label;
        Action<int> _callback;

        void Awake()
        {
            _canvasGroup = GetComponent<CanvasGroup>();
            _rootCanvas = GetComponentInParent<Canvas>()?.rootCanvas;
            _label = _button.GetComponentInChildren<TMP_Text>();
            SetupButtonColors();
            HideImmediate();
        }

        /// <param name="position">Screen-space position where the menu should appear.</param>
        /// <param name="options">Button labels to show.</param>
        /// <param name="onSelect">Callback with the chosen option index.</param>
        public void Show(Vector2 position, string[] options, Action<int> onSelect)
        {
            _callback = onSelect;

            _label.text = options[0];
            _label.color = _normalLabelColor;
            _button.gameObject.SetActive(true);
            _button.onClick.RemoveAllListeners();
            _button.onClick.AddListener(() => OnOptionClicked(0));

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

        void SetupButtonColors()
        {
            var colors = _button.colors;
            colors.highlightedColor = _hoverBackgroundColor;
            colors.pressedColor = new Color(_hoverBackgroundColor.r, _hoverBackgroundColor.g, _hoverBackgroundColor.b,
                Mathf.Clamp01(_hoverBackgroundColor.a * 2.5f));
            colors.selectedColor = _hoverBackgroundColor;
            colors.colorMultiplier = 1f;
            _button.colors = colors;

            var trigger = _button.GetComponent<EventTrigger>() ?? _button.gameObject.AddComponent<EventTrigger>();
            trigger.triggers.Clear();

            var onEnter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
            onEnter.callback.AddListener(_ => _label.color = _hoverLabelColor);
            trigger.triggers.Add(onEnter);

            var onExit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
            onExit.callback.AddListener(_ => _label.color = _normalLabelColor);
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
