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

        static readonly LootDrop[] AmmoDrops =
        {
            new("Ammo_Rifle", 10, 40),
            new("Ammo_Shotgun", 4, 14),
            new("Ammo_Pistol", 8, 24),
        };

        static readonly LootDrop[] WeaponsDrops =
        {
            new("Rifle", 1, 1),
            new("Shotgun", 1, 1),
        };

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
            new("Ammo_Shotgun", 4, 10),
            new("Ammo_Pistol", 8, 18),
            new("Rifle", 1, 1),
            new("Shotgun", 1, 1),
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
