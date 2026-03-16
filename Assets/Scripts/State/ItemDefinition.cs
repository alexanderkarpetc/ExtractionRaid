using System;
using System.Collections.Generic;

namespace State
{
    [Flags]
    public enum ItemSlotType
    {
        None = 0,
        Weapon = 1 << 0,
        Helmet = 1 << 1,
        BodyArmor = 1 << 2,
        Backpack = 1 << 3,
    }

    public class ItemDefinition
    {
        public string Id;
        public string DisplayName;
        public ItemSlotType AllowedSlots;
        public int MaxStackSize = 1;
        public string AmmoType;

        public bool IsStackable => MaxStackSize > 1;

        static Dictionary<string, ItemDefinition> _registry;

        public static IReadOnlyDictionary<string, ItemDefinition> Registry
        {
            get
            {
                _registry ??= BuildRegistry();
                return _registry;
            }
        }

        public static ItemDefinition Get(string id)
        {
            return Registry.TryGetValue(id, out var def) ? def : null;
        }

        static Dictionary<string, ItemDefinition> BuildRegistry()
        {
            return new Dictionary<string, ItemDefinition>
            {
                ["Rifle"] = new()
                {
                    Id = "Rifle",
                    DisplayName = "Rifle",
                    AllowedSlots = ItemSlotType.Weapon | ItemSlotType.Backpack,
                },
                ["Shotgun"] = new()
                {
                    Id = "Shotgun",
                    DisplayName = "Shotgun",
                    AllowedSlots = ItemSlotType.Weapon | ItemSlotType.Backpack,
                },
                ["Helmet_Basic"] = new()
                {
                    Id = "Helmet_Basic",
                    DisplayName = "Basic Helmet",
                    AllowedSlots = ItemSlotType.Helmet | ItemSlotType.Backpack,
                },
                ["Armor_Basic"] = new()
                {
                    Id = "Armor_Basic",
                    DisplayName = "Basic Armor",
                    AllowedSlots = ItemSlotType.BodyArmor | ItemSlotType.Backpack,
                },
                ["Medkit"] = new()
                {
                    Id = "Medkit",
                    DisplayName = "Medkit",
                    AllowedSlots = ItemSlotType.Backpack,
                    MaxStackSize = 200,
                },
                ["Ammo_Rifle"] = new()
                {
                    Id = "Ammo_Rifle",
                    DisplayName = "Rifle Ammo",
                    AllowedSlots = ItemSlotType.Backpack,
                    MaxStackSize = 60,
                    AmmoType = "Ammo_Rifle",
                },
                ["Ammo_Shotgun"] = new()
                {
                    Id = "Ammo_Shotgun",
                    DisplayName = "Shotgun Ammo",
                    AllowedSlots = ItemSlotType.Backpack,
                    MaxStackSize = 20,
                    AmmoType = "Ammo_Shotgun",
                },
                ["Ammo_Pistol"] = new()
                {
                    Id = "Ammo_Pistol",
                    DisplayName = "Pistol Ammo",
                    AllowedSlots = ItemSlotType.Backpack,
                    MaxStackSize = 36,
                    AmmoType = "Ammo_Pistol",
                },
                ["Grenade"] = new()
                {
                    Id = "Grenade",
                    DisplayName = "Grenade",
                    AllowedSlots = ItemSlotType.Backpack,
                    MaxStackSize = 1,
                },
                ["Bandage"] = new()
                {
                    Id = "Bandage",
                    DisplayName = "Bandage",
                    AllowedSlots = ItemSlotType.Backpack,
                    MaxStackSize = 1,
                },
            };
        }
    }
}
