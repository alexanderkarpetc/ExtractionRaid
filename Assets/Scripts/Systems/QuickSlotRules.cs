using System.Collections.Generic;

namespace Systems
{
    /// <summary>
    /// Single source of truth for which items can be bound to a quick slot.
    /// Consumed by both <c>LootPopupView</c> (right-click "Bind to N" menu and
    /// hover+number bind) and <c>HotbarOverlay</c> (click-empty-slot picker).
    /// Activation logic lives in <see cref="QuickSlotSystem"/>.
    /// </summary>
    public static class QuickSlotRules
    {
        static readonly HashSet<string> Assignable = new()
        {
            "Medkit",
            "Bandage",
            "Grenade",
        };

        public static bool IsAssignable(string definitionId) =>
            !string.IsNullOrEmpty(definitionId) && Assignable.Contains(definitionId);
    }
}
