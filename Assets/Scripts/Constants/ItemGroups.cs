namespace Constants
{
    public enum ItemGroup
    {
        Meds,
        Ammo,
        Weapons,
        Gear,
        Throwables,
        Mixed,
    }

    /// <summary>
    /// Loose-loot presets: the "what kind of thing lies here" shorthand a
    /// <c>LooseLootSpawnPoint</c> picks from a dropdown. Each group is just a weighted set of
    /// <see cref="LootCategory"/> buckets — the actual item and its stack size come from
    /// <see cref="ItemBalanceAsset"/>, same as containers.
    /// </summary>
    public static class ItemGroups
    {
        static readonly LootPoolEntry[] MedsPool =
        {
            LootPoolEntry.FromCategory(LootCategory.Meds),
        };

        static readonly LootPoolEntry[] AmmoPool =
        {
            LootPoolEntry.FromCategory(LootCategory.Ammo),
        };

        // Cluster A (2026-05-01): "Rifle" / "Pistol" legacy items retired — a "weapons cache"
        // spawn point produces buildable Builder cores instead.
        static readonly LootPoolEntry[] WeaponsPool =
        {
            LootPoolEntry.FromCategory(LootCategory.WeaponCores),
        };

        static readonly LootPoolEntry[] GearPool =
        {
            LootPoolEntry.FromCategory(LootCategory.Gear),
        };

        static readonly LootPoolEntry[] ThrowablesPool =
        {
            LootPoolEntry.FromCategory(LootCategory.Throwables),
        };

        static readonly LootPoolEntry[] MixedPool =
        {
            LootPoolEntry.FromCategory(LootCategory.Meds,        2f),
            LootPoolEntry.FromCategory(LootCategory.Ammo,        3f),
            LootPoolEntry.FromCategory(LootCategory.Materials,   2f),
            LootPoolEntry.FromCategory(LootCategory.Throwables,  1f),
            LootPoolEntry.FromCategory(LootCategory.WeaponCores, 1f),
            LootPoolEntry.FromCategory(LootCategory.Attachments, 1f),
            LootPoolEntry.FromCategory(LootCategory.Gear,        0.5f),
        };

        public static LootPoolEntry[] GetPool(ItemGroup group)
        {
            return group switch
            {
                ItemGroup.Meds => MedsPool,
                ItemGroup.Ammo => AmmoPool,
                ItemGroup.Weapons => WeaponsPool,
                ItemGroup.Gear => GearPool,
                ItemGroup.Throwables => ThrowablesPool,
                ItemGroup.Mixed => MixedPool,
                _ => MixedPool,
            };
        }
    }
}
