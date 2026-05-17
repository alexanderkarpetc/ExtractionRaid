using UnityEngine;

namespace Dev
{
    public class DevCheatsCheatsSection : ScriptableObject
    {
        public bool GodMode;
        public bool InfiniteAmmo;

        // Migration toggle: when true, Tab opens the new UI Toolkit InventoryWindow
        // instead of the legacy uGUI LootPopupView. Both stay в проекті until the
        // UTK side is validated end-to-end. See docs/ai/ui-styling.md and the
        // inventory-migration plan.
        public bool UseUiToolkitInventory;
    }
}
