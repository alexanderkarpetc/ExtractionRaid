using State;

namespace Systems
{
    public static class EquipmentSystem
    {
        /// <summary>
        /// Write current ArmorMap durability back to inventory ItemState.
        /// Call BEFORE SyncArmorFromInventory to preserve combat damage.
        /// </summary>
        public static void WriteBackDurability(RaidState state, EId entityId, InventoryState inventory)
        {
            if (!state.ArmorMap.TryGetValue(entityId, out var slots)) return;

            if (slots.Helmet != null && inventory.HelmetSlot != null)
            {
                inventory.HelmetSlot.CurrentDurability = slots.Helmet.CurrentDurability;
                inventory.HelmetSlot.MaxDurability = slots.Helmet.MaxDurability;
            }

            if (slots.BodyArmor != null && inventory.BodyArmorSlot != null)
            {
                inventory.BodyArmorSlot.CurrentDurability = slots.BodyArmor.CurrentDurability;
                inventory.BodyArmorSlot.MaxDurability = slots.BodyArmor.MaxDurability;
            }
        }

        public static void SyncArmorFromInventory(RaidState state, EId entityId, InventoryState inventory)
        {
            var helmet = CreateArmorFromItem(inventory.HelmetSlot);
            var bodyArmor = CreateArmorFromItem(inventory.BodyArmorSlot);

            if (helmet == null && bodyArmor == null)
            {
                state.ArmorMap.Remove(entityId);
                return;
            }

            state.ArmorMap[entityId] = new ArmorSlotState
            {
                Helmet = helmet,
                BodyArmor = bodyArmor,
            };
        }

        static ArmorState CreateArmorFromItem(ItemState item)
        {
            if (item == null) return null;

            var def = item.Definition;
            if (def == null || def.ArmorPoints <= 0f) return null;

            if (item.HasCustomDurability)
            {
                return new ArmorState
                {
                    ArmorPoints = def.ArmorPoints,
                    CurrentDurability = item.CurrentDurability,
                    MaxDurability = item.MaxDurability,
                };
            }

            return ArmorState.Create(def.ArmorPoints, def.MaxDurability);
        }
    }
}
