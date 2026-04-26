using Adapters;
using State;
using Systems;
using UnityEngine.UIElements;
using View.UI.Tooltip;
using View.UI.Tooltip.Builders;

namespace View.UI.WeaponBuilder.Elements
{
    /// <summary>
    /// Read-only backpack thumbnail used inside the Builder window for context.
    /// Renders the inventory item's display name (archetype label for built weapons,
    /// regular DisplayName otherwise) and stack count if &gt; 1.
    ///
    /// Tier 1-2 scope: drag from this element is intentionally NOT wired — modules
    /// don't yet exist as backpack items (Tier 6 work). Hover surfaces full info via
    /// the tooltip system.
    /// </summary>
    public class BackpackItemElement : VisualElement
    {
        readonly Label _label;
        readonly Label _stackCount;

        ItemState               _item;
        ICoreDefinitionRegistry _registry;

        public BackpackItemElement()
        {
            AddToClassList("wb-bp-item");

            _label = new Label();
            _label.AddToClassList("wb-bp-item-name");
            Add(_label);

            _stackCount = new Label();
            _stackCount.AddToClassList("wb-bp-item-count");
            Add(_stackCount);

            RegisterCallback<PointerEnterEvent>(OnPointerEnter);
            RegisterCallback<PointerLeaveEvent>(OnPointerLeave);
        }

        public void Bind(ItemState item, ICoreDefinitionRegistry registry)
        {
            _item     = item;
            _registry = registry;

            if (item == null)
            {
                _label.text = string.Empty;
                _stackCount.text = string.Empty;
                AddToClassList("wb-bp-item-empty");
                RemoveFromClassList("wb-bp-item-weapon");
                return;
            }

            RemoveFromClassList("wb-bp-item-empty");
            EnableInClassList("wb-bp-item-weapon", item.HasWeaponConfiguration);

            _label.text = WeaponDisplayName.For(item, registry);
            _stackCount.text = item.StackCount > 1 ? $"x{item.StackCount}" : string.Empty;
        }

        void OnPointerEnter(PointerEnterEvent evt)
        {
            if (_item == null || TooltipController.Instance == null) return;
            var model = ItemTooltipBuilder.For(_item, _registry);
            TooltipController.Instance.ShowFromPanel(model, evt.position);
        }

        void OnPointerLeave(PointerLeaveEvent _)
        {
            TooltipController.Instance?.Hide();
        }
    }
}
