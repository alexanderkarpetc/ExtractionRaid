using ApplicationCore;
using State;
using Systems;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace View.UI
{
    public class InventorySlotView : SlotViewBase
    {
        [Header("Item visuals")]
        [SerializeField] TMP_Text _nameLabel;
        [SerializeField] TMP_Text _resourceText;

        [Header("Durability (optional)")]
        [SerializeField] GameObject _durabilityRoot;
        [SerializeField] Slider _durabilitySlider;
        [SerializeField] Image _durabilityFill;

        [Header("Hotbar badge (optional)")]
        [SerializeField] GameObject _hotbarBadgeRoot;
        [SerializeField] TMP_Text _hotbarKeyText;

        public void Bind(InventorySlotRef slotRef, ItemState item, bool isLoot, int quickSlotKey)
        {
            SlotRef = slotRef;
            IsLoot = isLoot;
            CurrentItem = item;

            if (_nameLabel != null)
                _nameLabel.text = WeaponDisplayName.For(item, App.Instance?.CoreDefinitions);

            UpdateResourceText(item);
            UpdateDurability(item);
            UpdateHotbarBadge(quickSlotKey);
            ResetHighlight();
        }

        void UpdateResourceText(ItemState item)
        {
            if (_resourceText == null) return;

            if (item == null)
            {
                _resourceText.text = "";
                return;
            }

            var def = item.Definition;
            bool hasArmor = def != null && def.ArmorPoints > 0f;

            if (hasArmor)
            {
                float max = item.HasCustomDurability ? item.MaxDurability : def.MaxDurability;
                float cur = item.HasCustomDurability ? item.CurrentDurability : max;
                _resourceText.text = max > 0f ? $"{cur:0}/{max:0}" : "";
            }
            else if (item.StackCount > 1)
            {
                _resourceText.text = $"x{item.StackCount}";
            }
            else
            {
                _resourceText.text = "";
            }
        }

        void UpdateDurability(ItemState item)
        {
            bool show = item?.Definition != null && item.Definition.ArmorPoints > 0f;

            if (_durabilityRoot != null)
                _durabilityRoot.SetActive(show);

            if (!show) return;

            var def = item.Definition;
            float max = item.HasCustomDurability ? item.MaxDurability : def.MaxDurability;
            float cur = item.HasCustomDurability ? item.CurrentDurability : max;
            float pct = max > 0f ? cur / max : 0f;

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

        void UpdateHotbarBadge(int quickSlotKey)
        {
            bool show = quickSlotKey >= 0;
            if (_hotbarBadgeRoot != null)
                _hotbarBadgeRoot.SetActive(show);
            if (show && _hotbarKeyText != null)
                _hotbarKeyText.text = quickSlotKey.ToString();
        }
    }
}
