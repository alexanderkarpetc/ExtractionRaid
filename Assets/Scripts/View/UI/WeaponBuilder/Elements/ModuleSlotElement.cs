using System;
using State;
using UnityEngine.UIElements;
using View.UI.Tooltip;
using View.UI.Tooltip.Builders;

namespace View.UI.WeaponBuilder.Elements
{
    /// <summary>
    /// One Build slot — Payload OR Delivery. Renders empty placeholder when nothing
    /// is selected and a filled summary card with a clear button when populated.
    /// Drag-and-drop drop target detection is geometry-based (handled by the window's
    /// drag manipulator); this element just exposes <see cref="Kind"/> and a clear
    /// callback for the host.
    /// </summary>
    public class ModuleSlotElement : VisualElement
    {
        public ModuleCardElement.ModuleKind Kind { get; }

        readonly Label _empty;
        readonly VisualElement _filled;
        readonly Label _filledTitle;
        readonly Label _filledKind;
        readonly Button _clearBtn;

        PayloadCoreDefinition  _payload;
        DeliveryCoreDefinition _delivery;

        public event Action<ModuleSlotElement> Cleared;

        public ModuleSlotElement(ModuleCardElement.ModuleKind kind)
        {
            Kind = kind;
            AddToClassList("wb-slot");

            _empty = new Label(kind == ModuleCardElement.ModuleKind.Payload
                ? "⊕  Drop a Payload"
                : "⊕  Drop a Delivery");
            _empty.AddToClassList("wb-slot-empty");
            Add(_empty);

            _filled = new VisualElement();
            _filled.AddToClassList("wb-slot-filled");
            _filled.style.display = DisplayStyle.None;

            _filledTitle = new Label();
            _filledTitle.AddToClassList("wb-slot-filled-title");

            _filledKind = new Label();
            _filledKind.AddToClassList("wb-slot-filled-kind");

            _clearBtn = new Button(() => Cleared?.Invoke(this)) { text = "×" };
            _clearBtn.AddToClassList("wb-slot-clear");
            _clearBtn.tooltip = "Clear slot";

            _filled.Add(_filledTitle);
            _filled.Add(_filledKind);
            _filled.Add(_clearBtn);
            Add(_filled);

            RegisterCallback<PointerEnterEvent>(OnPointerEnter);
            RegisterCallback<PointerLeaveEvent>(OnPointerLeave);
        }

        public void Clear()
        {
            _payload  = null;
            _delivery = null;
            _empty.style.display  = DisplayStyle.Flex;
            _filled.style.display = DisplayStyle.None;
            RemoveFromClassList("wb-slot-filled-state");
        }

        public void Fill(PayloadCoreDefinition def)
        {
            _payload  = def;
            _delivery = null;
            _filledTitle.text = !string.IsNullOrEmpty(def?.DisplayName) ? def.DisplayName : def?.Id;
            _filledKind.text  = "Payload";
            _empty.style.display  = DisplayStyle.None;
            _filled.style.display = DisplayStyle.Flex;
            AddToClassList("wb-slot-filled-state");
        }

        public void Fill(DeliveryCoreDefinition def)
        {
            _payload  = null;
            _delivery = def;
            _filledTitle.text = !string.IsNullOrEmpty(def?.FormFactor) ? def.FormFactor : def?.Id;
            _filledKind.text  = "Delivery";
            _empty.style.display  = DisplayStyle.None;
            _filled.style.display = DisplayStyle.Flex;
            AddToClassList("wb-slot-filled-state");
        }

        public void SetDragOver(bool valid, bool hovering)
        {
            RemoveFromClassList("wb-slot-drag-valid");
            RemoveFromClassList("wb-slot-drag-invalid");
            if (!hovering) return;
            AddToClassList(valid ? "wb-slot-drag-valid" : "wb-slot-drag-invalid");
        }

        void OnPointerEnter(PointerEnterEvent evt)
        {
            if (TooltipController.Instance == null) return;
            if (_payload == null && _delivery == null) return;
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
