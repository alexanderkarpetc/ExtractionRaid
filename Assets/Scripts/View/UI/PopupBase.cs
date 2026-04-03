using UnityEngine;

namespace View.UI
{
    [RequireComponent(typeof(CanvasGroup))]
    public abstract class PopupBase : MonoBehaviour
    {
        CanvasGroup _canvasGroup;

        public bool IsOpen { get; private set; }

        protected virtual void Awake()
        {
            _canvasGroup = GetComponent<CanvasGroup>();
            SetVisible(false);
        }

        public virtual void Show()
        {
            IsOpen = true;
            SetVisible(true);
        }

        public virtual void Hide()
        {
            IsOpen = false;
            SetVisible(false);
        }

        void SetVisible(bool visible)
        {
            if (_canvasGroup == null)
                _canvasGroup = GetComponent<CanvasGroup>();

            _canvasGroup.alpha = visible ? 1f : 0f;
            _canvasGroup.blocksRaycasts = visible;
            _canvasGroup.interactable = visible;
        }
    }
}
