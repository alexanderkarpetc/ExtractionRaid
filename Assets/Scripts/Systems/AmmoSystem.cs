using State;

namespace Systems
{
    public static class AmmoSystem
    {
        /// <summary>
        /// Counts total reserve ammo of the given type across all backpack slots.
        /// </summary>
        public static int CountReserve(InventoryState inventory, string ammoType)
        {
            if (string.IsNullOrEmpty(ammoType)) return 0;
            int total = 0;
            for (int i = 0; i < InventoryState.BackpackSize; i++)
            {
                var item = inventory.Backpack[i];
                if (item != null && item.DefinitionId == ammoType)
                    total += item.StackCount;
            }
            return total;
        }

        /// <summary>
        /// Consumes up to `amount` rounds of the given ammo type from backpack.
        /// Returns actual amount consumed.
        /// </summary>
        public static int ConsumeAmmo(InventoryState inventory, string ammoType, int amount)
        {
            if (string.IsNullOrEmpty(ammoType) || amount <= 0) return 0;

            int remaining = amount;
            for (int i = 0; i < InventoryState.BackpackSize && remaining > 0; i++)
            {
                var item = inventory.Backpack[i];
                if (item == null || item.DefinitionId != ammoType) continue;

                int take = remaining < item.StackCount ? remaining : item.StackCount;
                item.StackCount -= take;
                remaining -= take;

                if (item.StackCount <= 0)
                    inventory.Backpack[i] = null;
            }
            return amount - remaining;
        }

        /// <summary>
        /// Called when reload timer finishes. Pulls ammo from inventory to fill magazine.
        /// </summary>
        public static void CompleteReload(WeaponEntityState weapon, InventoryState inventory)
        {
            if (string.IsNullOrEmpty(weapon.AmmoType)) return;

            int needed = weapon.MagazineSize - weapon.AmmoInMagazine;
            if (needed <= 0) return;

            int consumed = ConsumeAmmo(inventory, weapon.AmmoType, needed);
            weapon.AmmoInMagazine += consumed;
        }

        /// <summary>
        /// Returns true if the weapon can start reloading (has room in magazine AND has reserve).
        /// </summary>
        public static bool CanReload(WeaponEntityState weapon, InventoryState inventory)
        {
            if (string.IsNullOrEmpty(weapon.AmmoType)) return false;
            if (weapon.AmmoInMagazine >= weapon.MagazineSize) return false;
            return CountReserve(inventory, weapon.AmmoType) > 0;
        }
    }
}
