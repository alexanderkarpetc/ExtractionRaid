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
    /// <summary>
    /// ⚠️ LEGACY — dead code, kept only because deleting it is a prefab job.
    ///
    /// The uGUI inventory/loot slot from before the UI Toolkit rewrite. Nothing instantiates or
    /// opens it any more: <see cref="InventoryWindow"/> + <c>InventorySlotElement</c> replaced the
    /// whole path, and no script references this type. It survives because it sits on the
    /// <c>LootPopup</c> / <c>SlotItemView</c> / <c>HotBarItemVIew</c> prefabs, which are children of
    /// <c>Resources/Prefabs/UI/UI.prefab</c> — and that prefab is instantiated in every scene, so
    /// deleting the files would leave missing-script and broken-reference warnings project-wide.
    ///
    /// Do not extend it, and do not copy from it: its tooltip call passes no shop context and no
    /// canModify, so prices and the modify hint silently differ from the live path.
    /// Removal = strip the legacy subtree from UI.prefab in the editor first, then delete the
    /// prefabs, then these two scripts.
    /// </summary>
    public class InventorySlotView : SlotViewBase
    {
        [Header("Item visuals")]
        [SerializeField] TMP_Text _nameLabel;
        [SerializeField] TMP_Text _resourceText;
        
        [SerializeField] GameObject _questMarker;

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
            UpdateQuestMarker(item);
            ResetHighlight();
        }

        void UpdateQuestMarker(ItemState item)
        {
            if (_questMarker == null) return;
            _questMarker.SetActive(item?.Definition?.Category == ItemCategory.Quest);
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
            else if (item.IsResourceItem)
            {
                _resourceText.text = $"{item.CurrentResource}/{item.MaxResource}";
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

        public override void OnPointerEnter(PointerEventData eventData)
        {
            base.OnPointerEnter(eventData);
            if (CurrentItem == null || TooltipController.Instance == null) return;
            var model = ItemTooltipBuilder.For(CurrentItem, App.Instance?.CoreDefinitions,
                App.Instance?.QuestDatabase);
            TooltipController.Instance.Show(model, eventData.position);
        }

        public override void OnPointerExit(PointerEventData eventData)
        {
            base.OnPointerExit(eventData);
            TooltipController.Instance?.Hide();
        }
    }
}
