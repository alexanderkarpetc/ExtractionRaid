using State;
using UnityEngine.UIElements;
using View.UI.Tooltip;
using View.UI.Tooltip.Builders;

namespace View.UI.WeaponBuilder.Elements
{
    /// <summary>
    /// Visual representation of one module (Payload or Delivery) in the Builder
    /// palette. Pure data-bound — no game logic.
    ///
    /// Pointer / click handling is owned by <c>WeaponBuilderWindow</c> so it can
    /// coordinate the drag state machine + click-suppression after a drag. This
    /// element only exposes <see cref="Kind"/>, <see cref="DefinitionId"/> and
    /// <see cref="GetDisplayName"/>, plus tooltip wiring on hover.
    /// </summary>
    public class ModuleCardElement : VisualElement
    {
        public enum ModuleKind { Payload, Delivery }

        public ModuleKind Kind { get; }
        public string DefinitionId { get; }

        readonly PayloadCoreDefinition  _payload;
        readonly DeliveryCoreDefinition _delivery;

        public ModuleCardElement(PayloadCoreDefinition def)
        {
            Kind = ModuleKind.Payload;
            _payload = def;
            DefinitionId = def?.Id;
            BuildLayout(def?.DisplayName, def?.Id, "Payload");
            RegisterCallback<PointerEnterEvent>(OnPointerEnter);
            RegisterCallback<PointerLeaveEvent>(OnPointerLeave);
        }

        public ModuleCardElement(DeliveryCoreDefinition def)
        {
            Kind = ModuleKind.Delivery;
            _delivery = def;
            DefinitionId = def?.Id;
            BuildLayout(def?.FormFactor, def?.Id, "Delivery");
            RegisterCallback<PointerEnterEvent>(OnPointerEnter);
            RegisterCallback<PointerLeaveEvent>(OnPointerLeave);
        }

        public void SetSelected(bool selected)
        {
            if (selected) AddToClassList("wb-card-selected");
            else          RemoveFromClassList("wb-card-selected");
        }

        /// <summary>
        /// Toggles the dimmed "unavailable" look — used коли player не has the
        /// corresponding module item у backpack (Tier 6 G4). Click/drag still work
        /// for selection consistency; CanBuild gating prevents actual Build.
        /// </summary>
        public void SetAvailable(bool available)
        {
            if (available) RemoveFromClassList("wb-card-unavailable");
            else           AddToClassList("wb-card-unavailable");
        }

        /// <summary>Display name shown on the card (and reused by drag ghosts).</summary>
        public string GetDisplayName()
        {
            if (_payload != null)
                return !string.IsNullOrEmpty(_payload.DisplayName) ? _payload.DisplayName : _payload.Id;
            return !string.IsNullOrEmpty(_delivery?.FormFactor) ? _delivery.FormFactor : _delivery?.Id;
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
