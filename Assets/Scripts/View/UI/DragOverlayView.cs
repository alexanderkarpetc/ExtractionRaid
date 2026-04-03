using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace View.UI
{
    [RequireComponent(typeof(CanvasGroup))]
    public class DragOverlayView : MonoBehaviour
    {
        [SerializeField] TMP_Text _label;
        [SerializeField] Vector2 _cursorOffset = new Vector2(12f, 12f);

        RectTransform _rect;
        CanvasGroup _canvasGroup;
        Canvas _rootCanvas;

        void Awake()
        {
            _rect = GetComponent<RectTransform>();
            _canvasGroup = GetComponent<CanvasGroup>();
            _rootCanvas = GetComponentInParent<Canvas>()?.rootCanvas;
            HideImmediate();
        }

        public void Show(string text)
        {
            if (_label != null) _label.text = text;
            _canvasGroup.alpha = 1f;
            _canvasGroup.blocksRaycasts = false;
        }

        public void Hide()
        {
            HideImmediate();
        }

        public void FollowCursor()
        {
            if (_rootCanvas == null) return;

            var mousePos = Mouse.current != null
                ? (Vector2)Mouse.current.position.ReadValue()
                : Vector2.zero;

            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _rootCanvas.transform as RectTransform,
                mousePos,
                _rootCanvas.worldCamera,
                out var localPoint);

            _rect.localPosition = localPoint + _cursorOffset;
        }

        void HideImmediate()
        {
            _canvasGroup.alpha = 0f;
            _canvasGroup.blocksRaycasts = false;
        }
    }
}
