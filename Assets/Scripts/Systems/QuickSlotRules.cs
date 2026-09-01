using System.Collections.Generic;

namespace Systems
{
    /// <summary>
    /// Single source of truth for which items can be bound to a quick slot.
    /// Consumed by <c>InventoryWindow</c> (right-click "Bind to N" menu +
    /// hover+digit bind) and <c>HotbarOverlay</c> (click-empty-slot picker).
    /// Activation logic lives in <see cref="QuickSlotSystem"/>.
    /// </summary>
    public static class QuickSlotRules
    {
        static readonly HashSet<string> Assignable = new()
        {
            "Medkit",
            "Advanced_Medkit",
            "Bandage",
            "Grenade",
        };

        public static bool IsAssignable(string definitionId) =>
            !string.IsNullOrEmpty(definitionId) && Assignable.Contains(definitionId);

        public static bool IsMedkit(string definitionId) =>
            definitionId == "Medkit" || definitionId == "Advanced_Medkit";

        public static bool IsBandage(string definitionId) => definitionId == "Bandage";
    }
}
