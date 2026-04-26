using System;
using State;
using UnityEngine.UIElements;
using View.UI.Tooltip;
using View.UI.Tooltip.Builders;

namespace View.UI.WeaponBuilder.Elements
{
    /// <summary>
    /// Visual representation of one module (Payload or Delivery) in the Builder
    /// palette. Pure data-bound — no game logic. Click selects, hover shows tooltip,
    /// drag is wired separately by <see cref="ModuleDragManipulator"/>.
    ///
    /// Type metadata lives on the element so slot drop targets can validate the
    /// drag without back-references to the registry.
    /// </summary>
    public class ModuleCardElement : VisualElement
    {
        public enum ModuleKind { Payload, Delivery }

        public ModuleKind Kind { get; }
        public string DefinitionId { get; }

        readonly PayloadCoreDefinition  _payload;
        readonly DeliveryCoreDefinition _delivery;

        public event Action<ModuleCardElement> Clicked;

        public ModuleCardElement(PayloadCoreDefinition def)
        {
            Kind = ModuleKind.Payload;
            _payload = def;
            DefinitionId = def?.Id;
            BuildLayout(def?.DisplayName, def?.Id, "Payload");
            RegisterCallback<ClickEvent>(_ => Clicked?.Invoke(this));
            RegisterCallback<PointerEnterEvent>(OnPointerEnter);
            RegisterCallback<PointerLeaveEvent>(OnPointerLeave);
        }

        public ModuleCardElement(DeliveryCoreDefinition def)
        {
            Kind = ModuleKind.Delivery;
            _delivery = def;
            DefinitionId = def?.Id;
            BuildLayout(def?.FormFactor, def?.Id, "Delivery");
            RegisterCallback<ClickEvent>(_ => Clicked?.Invoke(this));
            RegisterCallback<PointerEnterEvent>(OnPointerEnter);
            RegisterCallback<PointerLeaveEvent>(OnPointerLeave);
        }

        public void SetSelected(bool selected)
        {
            if (selected) AddToClassList("wb-card-selected");
            else          RemoveFromClassList("wb-card-selected");
        }

        void BuildLayout(string title, string fallback, string kindLabel)
        {
            AddToClassList("wb-card");

            var titleLabel = new Label(string.IsNullOrEmpty(title) ? fallback : title);
            titleLabel.AddToClassList("wb-card-title");
            Add(titleLabel);

            var kindBadge = new Label(kindLabel);
            kindBadge.AddToClassList("wb-card-kind");
            Add(kindBadge);
        }

        void OnPointerEnter(PointerEnterEvent evt)
        {
            if (TooltipController.Instance == null) return;
            var model = _payload != null
                ? ModuleTooltipBuilder.ForPayload(_payload)
                : ModuleTooltipBuilder.ForDelivery(_delivery);
            TooltipController.Instance.ShowFromPanel(model, evt.position);
        }

        void OnPointerLeave(PointerLeaveEvent _)
        {
            TooltipController.Instance?.Hide();
        }
    }
}
