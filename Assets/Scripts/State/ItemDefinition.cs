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
                ["Pistol"] = new()
                {
                    Id = "Pistol",
                    DisplayName = "Pistol",
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
                ["Advanced_Medkit"] = new()
                {
                    Id = "Advanced_Medkit",
                    DisplayName = "Advanced Medkit",
                    AllowedSlots = ItemSlotType.Backpack,
                    MaxStackSize = 1,
                },

                // --- Crafting Materials (Common) ---
                ["Adhesive"] = new()
                {
                    Id = "Adhesive",
                    DisplayName = "Adhesive",
                    AllowedSlots = ItemSlotType.Backpack,
                    MaxStackSize = 20,
                },
                ["Metal_Parts"] = new()
                {
                    Id = "Metal_Parts",
                    DisplayName = "Metal Parts",
                    AllowedSlots = ItemSlotType.Backpack,
                    MaxStackSize = 30,
                },
                ["Mechanical_Parts"] = new()
                {
                    Id = "Mechanical_Parts",
                    DisplayName = "Mechanical Parts",
                    AllowedSlots = ItemSlotType.Backpack,
                    MaxStackSize = 20,
                },
                ["Electronics"] = new()
                {
                    Id = "Electronics",
                    DisplayName = "Electronics",
                    AllowedSlots = ItemSlotType.Backpack,
                    MaxStackSize = 15,
                },
                ["Chemicals"] = new()
                {
                    Id = "Chemicals",
                    DisplayName = "Chemicals",
                    AllowedSlots = ItemSlotType.Backpack,
                    MaxStackSize = 20,
                },
                ["Cloth"] = new()
                {
                    Id = "Cloth",
                    DisplayName = "Cloth",
                    AllowedSlots = ItemSlotType.Backpack,
                    MaxStackSize = 30,
                },
                ["Gunpowder"] = new()
                {
                    Id = "Gunpowder",
                    DisplayName = "Gunpowder",
                    AllowedSlots = ItemSlotType.Backpack,
                    MaxStackSize = 30,
                },
                ["Plastic"] = new()
                {
                    Id = "Plastic",
                    DisplayName = "Plastic",
                    AllowedSlots = ItemSlotType.Backpack,
                    MaxStackSize = 20,
                },
                ["Glass"] = new()
                {
                    Id = "Glass",
                    DisplayName = "Glass",
                    AllowedSlots = ItemSlotType.Backpack,
                    MaxStackSize = 15,
                },
                ["Rubber"] = new()
                {
                    Id = "Rubber",
                    DisplayName = "Rubber",
                    AllowedSlots = ItemSlotType.Backpack,
                    MaxStackSize = 15,
                },
                ["Springs"] = new()
                {
                    Id = "Springs",
                    DisplayName = "Springs",
                    AllowedSlots = ItemSlotType.Backpack,
                    MaxStackSize = 15,
                },

                // --- Crafting Materials (Rare) ---
                ["Military_Components"] = new()
                {
                    Id = "Military_Components",
                    DisplayName = "Military Components",
                    AllowedSlots = ItemSlotType.Backpack,
                    MaxStackSize = 5,
                },
                ["Energy_Core"] = new()
                {
                    Id = "Energy_Core",
                    DisplayName = "Energy Core",
                    AllowedSlots = ItemSlotType.Backpack,
                    MaxStackSize = 3,
                },

                // --- AP Ammo ---
                ["Ammo_Pistol_AP"] = new()
                {
                    Id = "Ammo_Pistol_AP",
                    DisplayName = "Pistol AP Ammo",
                    AllowedSlots = ItemSlotType.Backpack,
                    MaxStackSize = 36,
                    AmmoType = "Ammo_Pistol_AP",
                },
                ["Ammo_Rifle_AP"] = new()
                {
                    Id = "Ammo_Rifle_AP",
                    DisplayName = "Rifle AP Ammo",
                    AllowedSlots = ItemSlotType.Backpack,
                    MaxStackSize = 60,
                    AmmoType = "Ammo_Rifle_AP",
                },

                // --- Weapon Mods ---
                ["Basic_Scope"] = new()
                {
                    Id = "Basic_Scope",
                    DisplayName = "Basic Scope",
                    AllowedSlots = ItemSlotType.Backpack,
                },
                ["Advanced_Scope"] = new()
                {
                    Id = "Advanced_Scope",
                    DisplayName = "Advanced Scope",
                    AllowedSlots = ItemSlotType.Backpack,
                },
                ["Long_Barrel"] = new()
                {
                    Id = "Long_Barrel",
                    DisplayName = "Long Barrel",
                    AllowedSlots = ItemSlotType.Backpack,
                },
                ["Short_Barrel"] = new()
                {
                    Id = "Short_Barrel",
                    DisplayName = "Short Barrel",
                    AllowedSlots = ItemSlotType.Backpack,
                },
                ["Suppressor"] = new()
                {
                    Id = "Suppressor",
                    DisplayName = "Suppressor",
                    AllowedSlots = ItemSlotType.Backpack,
                },
                ["Compensator"] = new()
                {
                    Id = "Compensator",
                    DisplayName = "Compensator",
                    AllowedSlots = ItemSlotType.Backpack,
                },
                ["Extended_Mag"] = new()
                {
                    Id = "Extended_Mag",
                    DisplayName = "Extended Mag",
                    AllowedSlots = ItemSlotType.Backpack,
                },
                ["Fast_Reload_Mag"] = new()
                {
                    Id = "Fast_Reload_Mag",
                    DisplayName = "Fast Reload Mag",
                    AllowedSlots = ItemSlotType.Backpack,
                },
                ["Recoil_Grip"] = new()
                {
                    Id = "Recoil_Grip",
                    DisplayName = "Recoil Grip",
                    AllowedSlots = ItemSlotType.Backpack,
                },
                ["Stabilized_Stock"] = new()
                {
                    Id = "Stabilized_Stock",
                    DisplayName = "Stabilized Stock",
                    AllowedSlots = ItemSlotType.Backpack,
                },
                ["AP_Barrel"] = new()
                {
                    Id = "AP_Barrel",
                    DisplayName = "Armor-Piercing Barrel",
                    AllowedSlots = ItemSlotType.Backpack,
                },
                ["Overclock_Receiver"] = new()
                {
                    Id = "Overclock_Receiver",
                    DisplayName = "Overclock Receiver",
                    AllowedSlots = ItemSlotType.Backpack,
                },
            };
        }
    }
}
