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

    public enum ItemCategory
    {
        None,
        Weapon,
        Armor,
        Meds,
        Throwable,
        Ammo,
        Material,
        WeaponMod,
        Quest,
    }

    public class ItemDefinition
    {
        public string Id;
        public string DisplayName;
        public ItemCategory Category;
        public ItemSlotType AllowedSlots;
        public int MaxStackSize = 1;
        public string AmmoType;

        // Armor stats (helmet/vest items)
        public float ArmorPoints;
        public float MaxDurability;
        public string ArmorPrefabId; // visual mesh in Resources/Prefabs/Armor/{ArmorPrefabId}

        // Combat stats (ammo items)
        public float Penetration;
        public float ArmorDamage;
        public float BleedChance;

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
#pragma warning disable CS0618 // Rifle/Pistol legacy entries still set WeaponPrefabId until Tier 4 bot migration
            return new Dictionary<string, ItemDefinition>
            {
                // Generic weapon entry for Builder-created weapons. PrefabId is left
                // empty here; WeaponSyncSystem derives it from the Delivery FormFactor
                // at assembly time. Display name shown in inventory UI is expected to be
                // replaced by the archetype label (e.g. "Ballistic Pistol").
                ["Weapon"] = new()
                {
                    Id = "Weapon",
                    DisplayName = "Weapon",
                    Category = ItemCategory.Weapon,
                    AllowedSlots = ItemSlotType.Weapon | ItemSlotType.Backpack,
                },
                ["Rifle"] = new()
                {
                    Id = "Rifle",
                    DisplayName = "Rifle",
                    Category = ItemCategory.Weapon,
                    AllowedSlots = ItemSlotType.Weapon | ItemSlotType.Backpack,
                },
                ["Pistol"] = new()
                {
                    Id = "Pistol",
                    DisplayName = "Pistol",
                    Category = ItemCategory.Weapon,
                    AllowedSlots = ItemSlotType.Weapon | ItemSlotType.Backpack,
                },
                ["Helmet_Basic"] = new()
                {
                    Id = "Helmet_Basic",
                    DisplayName = "Basic Helmet",
                    Category = ItemCategory.Armor,
                    AllowedSlots = ItemSlotType.Helmet | ItemSlotType.Backpack,
                    ArmorPoints = 30f,
                    MaxDurability = 100f,
                    ArmorPrefabId = "Helmet_Basic",
                },
                ["Armor_Basic"] = new()
                {
                    Id = "Armor_Basic",
                    DisplayName = "Basic Armor",
                    Category = ItemCategory.Armor,
                    AllowedSlots = ItemSlotType.BodyArmor | ItemSlotType.Backpack,
                    ArmorPoints = 40f,
                    MaxDurability = 120f,
                    ArmorPrefabId = "Armor_Basic",
                },
                ["Medkit"] = new()
                {
                    Id = "Medkit",
                    DisplayName = "Medkit",
                    Category = ItemCategory.Meds,
                    AllowedSlots = ItemSlotType.Backpack,
                    MaxStackSize = 200,
                },
                ["Ammo_Rifle"] = new()
                {
                    Id = "Ammo_Rifle",
                    DisplayName = "Rifle Ammo",
                    Category = ItemCategory.Ammo,
                    AllowedSlots = ItemSlotType.Backpack,
                    MaxStackSize = 60,
                    AmmoType = "Ammo_Rifle",
                    Penetration = 10f,
                    ArmorDamage = 5f,
                },
                ["Ammo_EnergyCell"] = new()
                {
                    Id = "Ammo_EnergyCell",
                    DisplayName = "Energy Cell",
                    Category = ItemCategory.Ammo,
                    AllowedSlots = ItemSlotType.Backpack,
                    MaxStackSize = 30,
                    AmmoType = "Ammo_EnergyCell",
                    // Laser payload does its damage through weapon base stats; ammo-level
                    // pen/armor/bleed modifiers are zero in Tier 2 (AP/HP variants → Tier 4).
                    Penetration = 0f,
                    ArmorDamage = 0f,
                    BleedChance = 0f,
                },
                ["Ammo_Pistol"] = new()
                {
                    Id = "Ammo_Pistol",
                    DisplayName = "Pistol Ammo",
                    Category = ItemCategory.Ammo,
                    AllowedSlots = ItemSlotType.Backpack,
                    MaxStackSize = 36,
                    AmmoType = "Ammo_Pistol",
                    Penetration = 12f,
                    ArmorDamage = 6f,
                },
                ["Grenade"] = new()
                {
                    Id = "Grenade",
                    DisplayName = "Grenade",
                    Category = ItemCategory.Throwable,
                    AllowedSlots = ItemSlotType.Backpack,
                    MaxStackSize = 1,
                },
                ["Bandage"] = new()
                {
                    Id = "Bandage",
                    DisplayName = "Bandage",
                    Category = ItemCategory.Meds,
                    AllowedSlots = ItemSlotType.Backpack,
                    MaxStackSize = 1,
                },
                ["Advanced_Medkit"] = new()
                {
                    Id = "Advanced_Medkit",
                    DisplayName = "Advanced Medkit",
                    Category = ItemCategory.Meds,
                    AllowedSlots = ItemSlotType.Backpack,
                    MaxStackSize = 1,
                },

                // --- Crafting Materials (Common) ---
                ["Adhesive"] = new()
                {
                    Id = "Adhesive",
                    DisplayName = "Adhesive",
                    Category = ItemCategory.Material,
                    AllowedSlots = ItemSlotType.Backpack,
                    MaxStackSize = 20,
                },
                ["Metal_Parts"] = new()
                {
                    Id = "Metal_Parts",
                    DisplayName = "Metal Parts",
                    Category = ItemCategory.Material,
                    AllowedSlots = ItemSlotType.Backpack,
                    MaxStackSize = 30,
                },
                ["Mechanical_Parts"] = new()
                {
                    Id = "Mechanical_Parts",
                    DisplayName = "Mechanical Parts",
                    Category = ItemCategory.Material,
                    AllowedSlots = ItemSlotType.Backpack,
                    MaxStackSize = 20,
                },
                ["Electronics"] = new()
                {
                    Id = "Electronics",
                    DisplayName = "Electronics",
                    Category = ItemCategory.Material,
                    AllowedSlots = ItemSlotType.Backpack,
                    MaxStackSize = 15,
                },
                ["Chemicals"] = new()
                {
                    Id = "Chemicals",
                    DisplayName = "Chemicals",
                    Category = ItemCategory.Material,
                    AllowedSlots = ItemSlotType.Backpack,
                    MaxStackSize = 20,
                },
                ["Cloth"] = new()
                {
                    Id = "Cloth",
                    DisplayName = "Cloth",
                    Category = ItemCategory.Material,
                    AllowedSlots = ItemSlotType.Backpack,
                    MaxStackSize = 30,
                },
                ["Gunpowder"] = new()
                {
                    Id = "Gunpowder",
                    DisplayName = "Gunpowder",
                    Category = ItemCategory.Material,
                    AllowedSlots = ItemSlotType.Backpack,
                    MaxStackSize = 30,
                },
                ["Plastic"] = new()
                {
                    Id = "Plastic",
                    DisplayName = "Plastic",
                    Category = ItemCategory.Material,
                    AllowedSlots = ItemSlotType.Backpack,
                    MaxStackSize = 20,
                },
                ["Glass"] = new()
                {
                    Id = "Glass",
                    DisplayName = "Glass",
                    Category = ItemCategory.Material,
                    AllowedSlots = ItemSlotType.Backpack,
                    MaxStackSize = 15,
                },
                ["Rubber"] = new()
                {
                    Id = "Rubber",
                    DisplayName = "Rubber",
                    Category = ItemCategory.Material,
                    AllowedSlots = ItemSlotType.Backpack,
                    MaxStackSize = 15,
                },
                ["Springs"] = new()
                {
                    Id = "Springs",
                    DisplayName = "Springs",
                    Category = ItemCategory.Material,
                    AllowedSlots = ItemSlotType.Backpack,
                    MaxStackSize = 15,
                },

                // --- Crafting Materials (Rare) ---
                ["Military_Components"] = new()
                {
                    Id = "Military_Components",
                    DisplayName = "Military Components",
                    Category = ItemCategory.Material,
                    AllowedSlots = ItemSlotType.Backpack,
                    MaxStackSize = 5,
                },
                ["Energy_Core"] = new()
                {
                    Id = "Energy_Core",
                    DisplayName = "Energy Core",
                    Category = ItemCategory.Material,
                    AllowedSlots = ItemSlotType.Backpack,
                    MaxStackSize = 3,
                },

                // --- AP Ammo ---
                ["Ammo_Pistol_AP"] = new()
                {
                    Id = "Ammo_Pistol_AP",
                    DisplayName = "Pistol AP Ammo",
                    Category = ItemCategory.Ammo,
                    AllowedSlots = ItemSlotType.Backpack,
                    MaxStackSize = 36,
                    AmmoType = "Ammo_Pistol_AP",
                    Penetration = 30f,
                    ArmorDamage = 7f,
                },
                ["Ammo_Rifle_AP"] = new()
                {
                    Id = "Ammo_Rifle_AP",
                    DisplayName = "Rifle AP Ammo",
                    Category = ItemCategory.Ammo,
                    AllowedSlots = ItemSlotType.Backpack,
                    MaxStackSize = 60,
                    AmmoType = "Ammo_Rifle_AP",
                    Penetration = 35f,
                    ArmorDamage = 8f,
                },

                // --- HP Ammo (Hollow Point — high bleed, no pen) ---
                ["Ammo_Rifle_HP"] = new()
                {
                    Id = "Ammo_Rifle_HP",
                    DisplayName = "Rifle HP Ammo",
                    Category = ItemCategory.Ammo,
                    AllowedSlots = ItemSlotType.Backpack,
                    MaxStackSize = 60,
                    AmmoType = "Ammo_Rifle_HP",
                    Penetration = 0f,
                    ArmorDamage = 0f,
                    BleedChance = 0.30f,
                },
                ["Ammo_Pistol_HP"] = new()
                {
                    Id = "Ammo_Pistol_HP",
                    DisplayName = "Pistol HP Ammo",
                    Category = ItemCategory.Ammo,
                    AllowedSlots = ItemSlotType.Backpack,
                    MaxStackSize = 36,
                    AmmoType = "Ammo_Pistol_HP",
                    Penetration = 0f,
                    ArmorDamage = 0f,
                    BleedChance = 0.25f,
                },

                // --- Weapon Mods ---
                ["Basic_Scope"] = new()
                {
                    Id = "Basic_Scope",
                    DisplayName = "Basic Scope",
                    Category = ItemCategory.WeaponMod,
                    AllowedSlots = ItemSlotType.Backpack,
                },
                ["Advanced_Scope"] = new()
                {
                    Id = "Advanced_Scope",
                    DisplayName = "Advanced Scope",
                    Category = ItemCategory.WeaponMod,
                    AllowedSlots = ItemSlotType.Backpack,
                },
                ["Long_Barrel"] = new()
                {
                    Id = "Long_Barrel",
                    DisplayName = "Long Barrel",
                    Category = ItemCategory.WeaponMod,
                    AllowedSlots = ItemSlotType.Backpack,
                },
                ["Short_Barrel"] = new()
                {
                    Id = "Short_Barrel",
                    DisplayName = "Short Barrel",
                    Category = ItemCategory.WeaponMod,
                    AllowedSlots = ItemSlotType.Backpack,
                },
                ["Suppressor"] = new()
                {
                    Id = "Suppressor",
                    DisplayName = "Suppressor",
                    Category = ItemCategory.WeaponMod,
                    AllowedSlots = ItemSlotType.Backpack,
                },
                ["Compensator"] = new()
                {
                    Id = "Compensator",
                    DisplayName = "Compensator",
                    Category = ItemCategory.WeaponMod,
                    AllowedSlots = ItemSlotType.Backpack,
                },
                ["Extended_Mag"] = new()
                {
                    Id = "Extended_Mag",
                    DisplayName = "Extended Mag",
                    Category = ItemCategory.WeaponMod,
                    AllowedSlots = ItemSlotType.Backpack,
                },
                ["Fast_Reload_Mag"] = new()
                {
                    Id = "Fast_Reload_Mag",
                    DisplayName = "Fast Reload Mag",
                    Category = ItemCategory.WeaponMod,
                    AllowedSlots = ItemSlotType.Backpack,
                },
                ["Recoil_Grip"] = new()
                {
                    Id = "Recoil_Grip",
                    DisplayName = "Recoil Grip",
                    Category = ItemCategory.WeaponMod,
                    AllowedSlots = ItemSlotType.Backpack,
                },
                ["Stabilized_Stock"] = new()
                {
                    Id = "Stabilized_Stock",
                    DisplayName = "Stabilized Stock",
                    Category = ItemCategory.WeaponMod,
                    AllowedSlots = ItemSlotType.Backpack,
                },
                ["AP_Barrel"] = new()
                {
                    Id = "AP_Barrel",
                    DisplayName = "Armor-Piercing Barrel",
                    Category = ItemCategory.WeaponMod,
                    AllowedSlots = ItemSlotType.Backpack,
                },
                ["Overclock_Receiver"] = new()
                {
                    Id = "Overclock_Receiver",
                    DisplayName = "Overclock Receiver",
                    Category = ItemCategory.WeaponMod,
                    AllowedSlots = ItemSlotType.Backpack,
                },

                // --- Quest Items ---
                ["Worn_Warehouse_Key"] = new()
                {
                    Id = "Worn_Warehouse_Key",
                    DisplayName = "Worn Warehouse Key",
                    Category = ItemCategory.Quest,
                    AllowedSlots = ItemSlotType.Backpack,
                    MaxStackSize = 1,
                },

                // --- Weapon Builder modules (Tier 6 G1) ---
                // Module Ids match PayloadCoreDefinition.Id / DeliveryCoreDefinition.Id у
                // CoreDefinitionDatabase. Non-stackable (MaxStackSize=1) for forward-compat
                // з Tier 4 rarity (різні tier = різні items). Auto-gen path → Tier 4 коли
                // rarity змусить SO refactor anyway. See docs/ai/weapon-builder/plan/roadmap.md
                // → Tier 6.

                // Payload modules
                ["BallisticRound"] = new()
                {
                    Id = "BallisticRound",
                    DisplayName = "Ballistic Round",
                    Category = ItemCategory.WeaponMod,
                    AllowedSlots = ItemSlotType.Backpack,
                    MaxStackSize = 1,
                },
                ["LaserCharge"] = new()
                {
                    Id = "LaserCharge",
                    DisplayName = "Laser Charge",
                    Category = ItemCategory.WeaponMod,
                    AllowedSlots = ItemSlotType.Backpack,
                    MaxStackSize = 1,
                },

                // Delivery modules
                ["SingleAction"] = new()
                {
                    Id = "SingleAction",
                    DisplayName = "Single-Action Delivery",
                    Category = ItemCategory.WeaponMod,
                    AllowedSlots = ItemSlotType.Backpack,
                    MaxStackSize = 1,
                },
                ["Auto"] = new()
                {
                    Id = "Auto",
                    DisplayName = "Auto Delivery",
                    Category = ItemCategory.WeaponMod,
                    AllowedSlots = ItemSlotType.Backpack,
                    MaxStackSize = 1,
                },
                ["Scatter"] = new()
                {
                    Id = "Scatter",
                    DisplayName = "Scatter Delivery",
                    Category = ItemCategory.WeaponMod,
                    AllowedSlots = ItemSlotType.Backpack,
                    MaxStackSize = 1,
                },
            };
#pragma warning restore CS0618
        }
    }
}
