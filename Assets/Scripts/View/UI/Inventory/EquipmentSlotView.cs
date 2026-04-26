using ApplicationCore;
using State;
using Systems;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using View.UI.Tooltip;
using View.UI.Tooltip.Builders;

namespace View.UI
{
    public class EquipmentSlotView : SlotViewBase
    {
        [Header("Equipment visuals")]
        [SerializeField] Transform _mainContent;
        [SerializeField] Transform _disabledContent;
        [SerializeField] TMP_Text _itemName;
        [SerializeField] Slider _durabilitySlider;
        [SerializeField] Image _durabilityFill;
        [SerializeField] TMP_Text _durabilityText;

        public void SetDisabled(bool disabled)
        {
            _mainContent.gameObject.SetActive(!disabled);
            _disabledContent.gameObject.SetActive(disabled);
        }

        public void Bind(InventorySlotRef slotRef, ItemState item, bool isLoot)
        {
            SlotRef = slotRef;
            IsLoot = isLoot;
            CurrentItem = item;

            if (_itemName != null)
                _itemName.text = WeaponDisplayName.For(item, App.Instance?.CoreDefinitions);

            UpdateDurability(item);
            ResetHighlight();
        }

        void UpdateDurability(ItemState item)
        {
            if (item?.Definition == null)
            {
                SetDurabilityVisuals(0f);
                if (_durabilityText != null) _durabilityText.text = "";
                return;
            }

            var def = item.Definition;
            float max = item.HasCustomDurability ? item.MaxDurability : def.MaxDurability;
            float cur = item.HasCustomDurability ? item.CurrentDurability : max;

            if (max <= 0f)
            {
                SetDurabilityVisuals(0f);
                if (_durabilityText != null) _durabilityText.text = "";
                return;
            }

            float pct = cur / max;
            SetDurabilityVisuals(pct);

            if (_durabilityText != null)
                _durabilityText.text = $"{cur:0}/{max:0}";
        }

        void SetDurabilityVisuals(float pct)
        {
            if (_durabilitySlider != null)
                _durabilitySlider.value = pct;

            if (_durabilityFill != null)
            {
                if (pct >= 0.7f)
                    _durabilityFill.color = new Color(0.2f, 0.8f, 0.2f, 0.9f);
                else if (pct >= 0.4f)
                    _durabilityFill.color = new Color(0.9f, 0.75f, 0.1f, 0.9f);
                else
                    _durabilityFill.color = new Color(0.9f, 0.2f, 0.15f, 0.9f);
            }
        }

        public override void OnPointerEnter(PointerEventData eventData)
        {
            base.OnPointerEnter(eventData);
            if (CurrentItem == null || TooltipController.Instance == null) return;
            var model = ItemTooltipBuilder.For(CurrentItem, App.Instance?.CoreDefinitions);
            TooltipController.Instance.Show(model, eventData.position);
        }

        public override void OnPointerExit(PointerEventData eventData)
        {
            base.OnPointerExit(eventData);
            TooltipController.Instance?.Hide();
        }
    }
}
