using State;
using UnityEngine;

namespace Constants
{
    /// <summary>
    /// Broad, misuse-proof loot categories a bot can roll items from. A safe subset of
    /// <see cref="ItemCategory"/> — excludes Weapon / Quest / None so a loot config can
    /// never drop a generic weapon shell or a quest item by accident.
    /// </summary>
    public enum LootCategory
    {
        Materials,
        Meds,
        Ammo,
        Mods,
        Throwables,
    }

    public static class LootConstants
    {
        /// <summary>
        /// Pick weight for a value-weighted loot roll: pricier items are rarer. Linear
        /// inverse of intrinsic <see cref="ItemDefinition.Value"/> — a 120-value item is
        /// ~12× less likely than a 10-value one.
        /// </summary>
        public static float ValueWeight(int value) => 1f / Mathf.Max(1, value);

        public static ItemCategory ToItemCategory(LootCategory c) => c switch
        {
            LootCategory.Materials  => ItemCategory.Material,
            LootCategory.Meds       => ItemCategory.Meds,
            LootCategory.Ammo       => ItemCategory.Ammo,
            LootCategory.Mods       => ItemCategory.WeaponMod,
            LootCategory.Throwables => ItemCategory.Throwable,
            _                       => ItemCategory.None,
        };
    }
}
