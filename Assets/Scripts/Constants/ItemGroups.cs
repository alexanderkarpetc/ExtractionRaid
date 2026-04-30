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

    public static class ItemGroups
    {
        static readonly LootDrop[] MedsDrops =
        {
            new("Medkit", 1, 1),
            new("Bandage", 1, 1),
        };

        // Cluster A (2026-05-01): Ammo_Pistol family retired from drop tables —
        // no current payload uses it. May come back if Tier 3 splits Ballistic
        // into per-caliber payloads.
        static readonly LootDrop[] AmmoDrops =
        {
            new("Ammo_Rifle", 10, 40),
        };

        // Cluster A (2026-05-01): "Rifle" / "Pistol" legacy items retired —
        // ItemGroup.Weapons now drops Builder modules so a "weapons cache"
        // LooseLootSpawnPoint produces buildable parts.
        static readonly LootDrop[] WeaponsDrops = ContainerConstants.WeaponModuleDrops;

        static readonly LootDrop[] GearDrops =
        {
            new("Helmet_Basic", 1, 1),
            new("Armor_Basic", 1, 1),
        };

        static readonly LootDrop[] ThrowablesDrops =
        {
            new("Grenade", 1, 1),
        };

        static readonly LootDrop[] MixedDrops =
        {
            new("Medkit", 1, 1),
            new("Bandage", 1, 1),
            new("Grenade", 1, 1),
            new("Ammo_Rifle", 10, 30),
            // Modules — 1× each (5 entries) — Tier 6 G2 module loot economy.
            new("BallisticRound", 1, 1),
            new("LaserCharge",    1, 1),
            new("SingleAction",   1, 1),
            new("Auto",           1, 1),
            new("Scatter",        1, 1),
            new("Helmet_Basic", 1, 1),
            new("Armor_Basic", 1, 1),
        };

        public static LootDrop[] GetDrops(ItemGroup group)
        {
            return group switch
            {
                ItemGroup.Meds => MedsDrops,
                ItemGroup.Ammo => AmmoDrops,
                ItemGroup.Weapons => WeaponsDrops,
                ItemGroup.Gear => GearDrops,
                ItemGroup.Throwables => ThrowablesDrops,
                ItemGroup.Mixed => MixedDrops,
                _ => MixedDrops,
            };
        }
    }
}
