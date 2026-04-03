using System;
using State;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace View.UI
{
    public abstract class SlotViewBase : MonoBehaviour,
        IBeginDragHandler, IDragHandler, IEndDragHandler,
        IDropHandler, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
    {
        [Header("Slot base")]
        [SerializeField] protected Image _background;

        [Header("Colors")]
        [SerializeField] Color _normalColor = new Color(0.2f, 0.2f, 0.2f, 0.9f);
        [SerializeField] Color _highlightColor = new Color(0.4f, 0.6f, 0.3f, 0.9f);

        public InventorySlotRef SlotRef { get; protected set; }
        public bool IsLoot { get; protected set; }
        public ItemState CurrentItem { get; protected set; }
        public bool IsHovered { get; private set; }

        public event Action<SlotViewBase> DragStarted;
        public event Action<SlotViewBase> DroppedOnSlot;
        public event Action<SlotViewBase, PointerEventData> RightClicked;

        public void SetHighlight(bool on)
        {
            if (_background != null)
                _background.color = on ? _highlightColor : _normalColor;
        }

        protected void ResetHighlight()
        {
            if (_background != null)
                _background.color = _normalColor;
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left) return;
            if (CurrentItem == null) return;
            DragStarted?.Invoke(this);
        }

        public void OnDrag(PointerEventData eventData) { }

        public void OnEndDrag(PointerEventData eventData) { }

        public void OnDrop(PointerEventData eventData)
        {
            DroppedOnSlot?.Invoke(this);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Right && CurrentItem != null)
                RightClicked?.Invoke(this, eventData);
        }

        public void OnPointerEnter(PointerEventData eventData) => IsHovered = true;
        public void OnPointerExit(PointerEventData eventData) => IsHovered = false;
    }
}
