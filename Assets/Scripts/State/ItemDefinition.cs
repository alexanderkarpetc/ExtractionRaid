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

        // Intrinsic worth of one unit — the SEED the balance table starts from. When you
        // "Sync from ItemDefinition", ItemBalanceAsset copies this into Price and derives an
        // initial DropWeight from it. At runtime the balance asset (Resources/Configs/
        // ItemBalance) is authoritative for both price and drop chance; this stays the
        // fallback when an item isn't in the table yet. Baseline here; per-item values are
        // assigned in ApplyLootValues.
        public int Value = 10;

        // Consumable resource pool (e.g. medkit healing charge). 0 = not a resource
        // item. When > 0 the item is a single, non-stackable unit whose resource
        // drains on use and is shown as "current/max" rather than a stack count.
        public int MaxResource;

        // Flat HP restored when this consumable finishes applying (e.g. bandage).
        // 0 = no direct healing. Unlike MaxResource (a drainable pool), this is a
        // fixed amount granted once per item consumed.
        public float HealAmount;

        // Armor stats (helmet/vest items)
        public float ArmorPoints;
        public float MaxDurability;
        public string ArmorPrefabId; // visual mesh in Resources/Prefabs/Armor/{ArmorPrefabId}

        // Combat stats (ammo items) — modifiers added to weapon base at fire time.
        // DamageModifier can be negative (AP trade-off: better pen, less flesh damage).
        public float Penetration;
        public float DamageModifier;
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
            var reg = new Dictionary<string, ItemDefinition>
            {
                // Generic weapon entry for Builder-created weapons. All guns spawn as
                // this generic shell — identity (payload + delivery + rarity) lives in
                // the item's WeaponConfiguration. PrefabId is left empty here;
                // WeaponSyncSystem derives it from the Delivery FormFactor at assembly
                // time. Display name is replaced by the archetype label (e.g. "Ballistic
                // Pistol"). The old self-configuring "Rifle"/"Pistol" ids were retired —
                // use a WeaponPresetDefinition (Dev Cheats → Give Weapon Preset) instead.
                ["Weapon"] = new()
                {
                    Id = "Weapon",
                    DisplayName = "Weapon",
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
                    // One medkit per slot. Its 200 HP is a drainable resource pool,
                    // not a stack of 200 items. See MedkitSystem / ItemState.Resource.
                    MaxStackSize = 1,
                    MaxResource = (int)Constants.MedConstants.TotalHealAmount,
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
                    // Baseline 5% — every shot has a chance to bleed. HP variants escalate further.
                    BleedChance = 0.05f,
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
                    // pen/armor modifiers are zero in Tier 2 (AP/HP variants → Tier 4).
                    // Baseline 5% bleed shared across all ammo for consistent feedback.
                    Penetration = 0f,
                    ArmorDamage = 0f,
                    BleedChance = 0.05f,
                },
                // Ammo audit (2026-07-27): only calibers a payload core declares live here.
                // Pistol / AP / HP definitions were deleted — AmmoSystem chambers by exact id,
                // so an id no payload reads is unloadable loot. Re-add them together with the
                // payload (or the ammo-selection feature) that can actually fire them;
                // AmmoAvailabilityTests fails the moment an orphan caliber reappears.
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
                    MaxStackSize = 3,
                    // Stops/reduces bleeding AND restores a small chunk of HP on use.
                    HealAmount = 25f,
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

                // --- Crafting Materials (Common — structural / generic) ---
                // Stack sizes mirror the existing common-tier scale (15-30 per stack).
                // Adhesive / Plastic / Rubber / Springs are intentionally skipped — already
                // defined above.
                ["Pipes"] = new()
                {
                    Id = "Pipes",
                    DisplayName = "Pipes",
                    Category = ItemCategory.Material,
                    AllowedSlots = ItemSlotType.Backpack,
                    MaxStackSize = 20,
                },
                ["Insulated_Wiring"] = new()
                {
                    Id = "Insulated_Wiring",
                    DisplayName = "Insulated Wiring",
                    Category = ItemCategory.Material,
                    AllowedSlots = ItemSlotType.Backpack,
                    MaxStackSize = 20,
                },
                ["Duct_Tape"] = new()
                {
                    Id = "Duct_Tape",
                    DisplayName = "Duct Tape",
                    Category = ItemCategory.Material,
                    AllowedSlots = ItemSlotType.Backpack,
                    MaxStackSize = 20,
                },
                ["Aluminum"] = new()
                {
                    Id = "Aluminum",
                    DisplayName = "Aluminum",
                    Category = ItemCategory.Material,
                    AllowedSlots = ItemSlotType.Backpack,
                    MaxStackSize = 30,
                },
                ["Structural_Foam"] = new()
                {
                    Id = "Structural_Foam",
                    DisplayName = "Structural Foam",
                    Category = ItemCategory.Material,
                    AllowedSlots = ItemSlotType.Backpack,
                    MaxStackSize = 20,
                },
                ["Synthetic_Fiber"] = new()
                {
                    Id = "Synthetic_Fiber",
                    DisplayName = "Synthetic Fiber",
                    Category = ItemCategory.Material,
                    AllowedSlots = ItemSlotType.Backpack,
                    MaxStackSize = 20,
                },

                // --- Crafting Materials (Uncommon — mechanical / electrical modules) ---
                // Stack 8-15. Recipe inputs for mid-tier crafts and weapon mods.
                ["Hydraulic_Seals"] = new()
                {
                    Id = "Hydraulic_Seals",
                    DisplayName = "Hydraulic Seals",
                    Category = ItemCategory.Material,
                    AllowedSlots = ItemSlotType.Backpack,
                    MaxStackSize = 15,
                },
                ["Gear_Cluster"] = new()
                {
                    Id = "Gear_Cluster",
                    DisplayName = "Gear Cluster",
                    Category = ItemCategory.Material,
                    AllowedSlots = ItemSlotType.Backpack,
                    MaxStackSize = 15,
                },
                ["Rotary_Motor"] = new()
                {
                    Id = "Rotary_Motor",
                    DisplayName = "Rotary Motor",
                    Category = ItemCategory.Material,
                    AllowedSlots = ItemSlotType.Backpack,
                    MaxStackSize = 10,
                },
                ["Pneumatic_Valve"] = new()
                {
                    Id = "Pneumatic_Valve",
                    DisplayName = "Pneumatic Valve",
                    Category = ItemCategory.Material,
                    AllowedSlots = ItemSlotType.Backpack,
                    MaxStackSize = 10,
                },
                ["Cooling_Fan"] = new()
                {
                    Id = "Cooling_Fan",
                    DisplayName = "Cooling Fan",
                    Category = ItemCategory.Material,
                    AllowedSlots = ItemSlotType.Backpack,
                    MaxStackSize = 10,
                },
                ["Sensor_Module"] = new()
                {
                    Id = "Sensor_Module",
                    DisplayName = "Sensor Module",
                    Category = ItemCategory.Material,
                    AllowedSlots = ItemSlotType.Backpack,
                    MaxStackSize = 10,
                },
                ["Motion_Sensor"] = new()
                {
                    Id = "Motion_Sensor",
                    DisplayName = "Motion Sensor",
                    Category = ItemCategory.Material,
                    AllowedSlots = ItemSlotType.Backpack,
                    MaxStackSize = 8,
                },
                ["Camera_Optics"] = new()
                {
                    Id = "Camera_Optics",
                    DisplayName = "Camera Optics",
                    Category = ItemCategory.Material,
                    AllowedSlots = ItemSlotType.Backpack,
                    MaxStackSize = 8,
                },
                ["Flux_Coil"] = new()
                {
                    Id = "Flux_Coil",
                    DisplayName = "Flux Coil",
                    Category = ItemCategory.Material,
                    AllowedSlots = ItemSlotType.Backpack,
                    MaxStackSize = 10,
                },
                ["Pulse_Converter"] = new()
                {
                    Id = "Pulse_Converter",
                    DisplayName = "Pulse Converter",
                    Category = ItemCategory.Material,
                    AllowedSlots = ItemSlotType.Backpack,
                    MaxStackSize = 8,
                },
                ["Ion_Battery"] = new()
                {
                    Id = "Ion_Battery",
                    DisplayName = "Ion Battery",
                    Category = ItemCategory.Material,
                    AllowedSlots = ItemSlotType.Backpack,
                    MaxStackSize = 10,
                },
                ["Conductive_Gel"] = new()
                {
                    Id = "Conductive_Gel",
                    DisplayName = "Conductive Gel",
                    Category = ItemCategory.Material,
                    AllowedSlots = ItemSlotType.Backpack,
                    MaxStackSize = 15,
                },
                ["Energy_Relay"] = new()
                {
                    Id = "Energy_Relay",
                    DisplayName = "Energy Relay",
                    Category = ItemCategory.Material,
                    AllowedSlots = ItemSlotType.Backpack,
                    MaxStackSize = 10,
                },
                ["Chemical_Catalyst"] = new()
                {
                    Id = "Chemical_Catalyst",
                    DisplayName = "Chemical Catalyst",
                    Category = ItemCategory.Material,
                    AllowedSlots = ItemSlotType.Backpack,
                    MaxStackSize = 15,
                },
                ["Bio_Compound"] = new()
                {
                    Id = "Bio_Compound",
                    DisplayName = "Bio Compound",
                    Category = ItemCategory.Material,
                    AllowedSlots = ItemSlotType.Backpack,
                    MaxStackSize = 15,
                },
                ["Filtration_Membrane"] = new()
                {
                    Id = "Filtration_Membrane",
                    DisplayName = "Filtration Membrane",
                    Category = ItemCategory.Material,
                    AllowedSlots = ItemSlotType.Backpack,
                    MaxStackSize = 15,
                },
                ["Sterile_Wrap"] = new()
                {
                    Id = "Sterile_Wrap",
                    DisplayName = "Sterile Wrap",
                    Category = ItemCategory.Material,
                    AllowedSlots = ItemSlotType.Backpack,
                    MaxStackSize = 15,
                },

                // --- Crafting Materials (Rare — exotic / advanced) ---
                // Stack 3-10. Top-tier inputs; rolling these from loot should be a moment.
                ["Gyro_Stabilizer"] = new()
                {
                    Id = "Gyro_Stabilizer",
                    DisplayName = "Gyro Stabilizer",
                    Category = ItemCategory.Material,
                    AllowedSlots = ItemSlotType.Backpack,
                    MaxStackSize = 8,
                },
                ["Smart_Targeting_Unit"] = new()
                {
                    Id = "Smart_Targeting_Unit",
                    DisplayName = "Smart Targeting Unit",
                    Category = ItemCategory.Material,
                    AllowedSlots = ItemSlotType.Backpack,
                    MaxStackSize = 3,
                },
                ["Pulse_Emitter"] = new()
                {
                    Id = "Pulse_Emitter",
                    DisplayName = "Pulse Emitter",
                    Category = ItemCategory.Material,
                    AllowedSlots = ItemSlotType.Backpack,
                    MaxStackSize = 3,
                },
                ["Adaptive_Circuit"] = new()
                {
                    Id = "Adaptive_Circuit",
                    DisplayName = "Adaptive Circuit",
                    Category = ItemCategory.Material,
                    AllowedSlots = ItemSlotType.Backpack,
                    MaxStackSize = 5,
                },
                ["Nano_Filament"] = new()
                {
                    Id = "Nano_Filament",
                    DisplayName = "Nano Filament",
                    Category = ItemCategory.Material,
                    AllowedSlots = ItemSlotType.Backpack,
                    MaxStackSize = 5,
                },
                ["Magnetic_Alloy"] = new()
                {
                    Id = "Magnetic_Alloy",
                    DisplayName = "Magnetic Alloy",
                    Category = ItemCategory.Material,
                    AllowedSlots = ItemSlotType.Backpack,
                    MaxStackSize = 5,
                },
                ["Resonance_Plate"] = new()
                {
                    Id = "Resonance_Plate",
                    DisplayName = "Resonance Plate",
                    Category = ItemCategory.Material,
                    AllowedSlots = ItemSlotType.Backpack,
                    MaxStackSize = 5,
                },
                ["Synthetic_Quartz"] = new()
                {
                    Id = "Synthetic_Quartz",
                    DisplayName = "Synthetic Quartz",
                    Category = ItemCategory.Material,
                    AllowedSlots = ItemSlotType.Backpack,
                    MaxStackSize = 5,
                },
                ["Crystal_Matrix"] = new()
                {
                    Id = "Crystal_Matrix",
                    DisplayName = "Crystal Matrix",
                    Category = ItemCategory.Material,
                    AllowedSlots = ItemSlotType.Backpack,
                    MaxStackSize = 5,
                },
                ["Phase_Shard"] = new()
                {
                    Id = "Phase_Shard",
                    DisplayName = "Phase Shard",
                    Category = ItemCategory.Material,
                    AllowedSlots = ItemSlotType.Backpack,
                    MaxStackSize = 3,
                },
                ["Phase_Battery"] = new()
                {
                    Id = "Phase_Battery",
                    DisplayName = "Phase Battery",
                    Category = ItemCategory.Material,
                    AllowedSlots = ItemSlotType.Backpack,
                    MaxStackSize = 3,
                },
                ["Quantum_Relay"] = new()
                {
                    Id = "Quantum_Relay",
                    DisplayName = "Quantum Relay",
                    Category = ItemCategory.Material,
                    AllowedSlots = ItemSlotType.Backpack,
                    MaxStackSize = 3,
                },
                ["Plasma_Residue"] = new()
                {
                    Id = "Plasma_Residue",
                    DisplayName = "Plasma Residue",
                    Category = ItemCategory.Material,
                    AllowedSlots = ItemSlotType.Backpack,
                    MaxStackSize = 10,
                },
                ["Irradiated_Dust"] = new()
                {
                    Id = "Irradiated_Dust",
                    DisplayName = "Irradiated Dust",
                    Category = ItemCategory.Material,
                    AllowedSlots = ItemSlotType.Backpack,
                    MaxStackSize = 10,
                },
                ["Neural_Gel"] = new()
                {
                    Id = "Neural_Gel",
                    DisplayName = "Neural Gel",
                    Category = ItemCategory.Material,
                    AllowedSlots = ItemSlotType.Backpack,
                    MaxStackSize = 5,
                },
                ["Bio_Foam"] = new()
                {
                    Id = "Bio_Foam",
                    DisplayName = "Bio Foam",
                    Category = ItemCategory.Material,
                    AllowedSlots = ItemSlotType.Backpack,
                    MaxStackSize = 10,
                },

                // Specialty materials — intel/sample case classed as Material like the
                // rest of the additions, low stack so they read as high-value crafting inputs.
                ["Military_Intel"] = new()
                {
                    Id = "Military_Intel",
                    DisplayName = "Military Intel",
                    Category = ItemCategory.Material,
                    AllowedSlots = ItemSlotType.Backpack,
                    MaxStackSize = 3,
                },
                ["Bio_Sample_Case"] = new()
                {
                    Id = "Bio_Sample_Case",
                    DisplayName = "Bio Sample Case",
                    Category = ItemCategory.Material,
                    AllowedSlots = ItemSlotType.Backpack,
                    MaxStackSize = 3,
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

                // --- Attachments (loot-gated) ---
                // Ids MUST match the AttachmentDefinition SOs in
                // Resources/WeaponBuilder/Attachments so a backpack item resolves to its
                // attachment 1:1. Stackable so a haul of duplicates collapses into one slot.
                // The editor consumes one on install and returns one on remove.
                ["PowerComp"] = new()
                {
                    Id = "PowerComp",
                    DisplayName = "Power Compensator",
                    Category = ItemCategory.WeaponMod,
                    AllowedSlots = ItemSlotType.Backpack,
                    MaxStackSize = 20,
                },
                ["MuzzleBrake"] = new()
                {
                    Id = "MuzzleBrake",
                    DisplayName = "Muzzle Brake",
                    Category = ItemCategory.WeaponMod,
                    AllowedSlots = ItemSlotType.Backpack,
                    MaxStackSize = 20,
                },
                ["VerticalGrip"] = new()
                {
                    Id = "VerticalGrip",
                    DisplayName = "Vertical Grip",
                    Category = ItemCategory.WeaponMod,
                    AllowedSlots = ItemSlotType.Backpack,
                    MaxStackSize = 20,
                },
                ["AngledGrip"] = new()
                {
                    Id = "AngledGrip",
                    DisplayName = "Angled Grip",
                    Category = ItemCategory.WeaponMod,
                    AllowedSlots = ItemSlotType.Backpack,
                    MaxStackSize = 20,
                },
                ["HeavyStock"] = new()
                {
                    Id = "HeavyStock",
                    DisplayName = "Heavy Stock",
                    Category = ItemCategory.WeaponMod,
                    AllowedSlots = ItemSlotType.Backpack,
                    MaxStackSize = 20,
                },
                ["SkeletonStock"] = new()
                {
                    Id = "SkeletonStock",
                    DisplayName = "Skeleton Stock",
                    Category = ItemCategory.WeaponMod,
                    AllowedSlots = ItemSlotType.Backpack,
                    MaxStackSize = 20,
                },
                ["RedDot"] = new()
                {
                    Id = "RedDot",
                    DisplayName = "Red Dot Sight",
                    Category = ItemCategory.WeaponMod,
                    AllowedSlots = ItemSlotType.Backpack,
                    MaxStackSize = 20,
                },
                ["SniperScope"] = new()
                {
                    Id = "SniperScope",
                    DisplayName = "Sniper Scope",
                    Category = ItemCategory.WeaponMod,
                    AllowedSlots = ItemSlotType.Backpack,
                    MaxStackSize = 20,
                },
                ["ExtendedMag"] = new()
                {
                    Id = "ExtendedMag",
                    DisplayName = "Extended Magazine",
                    Category = ItemCategory.WeaponMod,
                    AllowedSlots = ItemSlotType.Backpack,
                    MaxStackSize = 20,
                },
                ["QuickMag"] = new()
                {
                    Id = "QuickMag",
                    DisplayName = "Quick Magazine",
                    Category = ItemCategory.WeaponMod,
                    AllowedSlots = ItemSlotType.Backpack,
                    MaxStackSize = 20,
                },

                // Unique (archetype-restricted) attachments — P3.
                ["LaserFocusing"] = new()
                {
                    Id = "LaserFocusing",
                    DisplayName = "Laser Focusing Optic",
                    Category = ItemCategory.WeaponMod,
                    AllowedSlots = ItemSlotType.Backpack,
                    MaxStackSize = 20,
                },
                ["ScatterChoke"] = new()
                {
                    Id = "ScatterChoke",
                    DisplayName = "Scatter Choke",
                    Category = ItemCategory.WeaponMod,
                    AllowedSlots = ItemSlotType.Backpack,
                    MaxStackSize = 20,
                },
                ["AutoHeatSink"] = new()
                {
                    Id = "AutoHeatSink",
                    DisplayName = "Auto Heat-Sink",
                    Category = ItemCategory.WeaponMod,
                    AllowedSlots = ItemSlotType.Backpack,
                    MaxStackSize = 20,
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

            ApplyLootValues(reg);
            return reg;
        }

        // Central "values file": intrinsic worth per item. Anything not listed keeps the
        // baseline Value (10). Higher value → rarer in value-weighted loot picks. Grouped
        // by the same tiers the crafting-material sections use. Ids here mirror the
        // registry keys above — keep them in sync when renaming an item.
        static void ApplyLootValues(Dictionary<string, ItemDefinition> reg)
        {
            void Set(string id, int value)
            {
                if (reg.TryGetValue(id, out var d)) d.Value = value;
            }

            // Ammo — energy cells cost more per round than rifle brass, but kept modest so a
            // full stack doesn't out-value a body armor.
            Set("Ammo_Rifle", 8);   Set("Ammo_EnergyCell", 16);

            // Meds / throwables / armor.
            Set("Bandage", 8); Set("Medkit", 45); Set("Advanced_Medkit", 110);
            Set("Grenade", 35);
            Set("Helmet_Basic", 60); Set("Armor_Basic", 85);

            // Weapon-builder modules — valuable weapon parts.
            Set("BallisticRound", 55); Set("LaserCharge", 70);
            Set("SingleAction", 45);   Set("Auto", 60); Set("Scatter", 55);

            // Weapon attachment mods. Tiered like Tarkov relative to this economy: cheap
            // handling mods (grips/mags/brake ≈ a core), mid stocks/red-dot, premium
            // optics/suppressors/uniques (rival a good core, below armor). Mirrors the
            // ItemBalance asset so a re-sync keeps prices sane instead of reverting to 10.
            Set("RedDot", 90);          Set("SniperScope", 260);
            Set("PowerComp", 130);      Set("MuzzleBrake", 70);
            Set("VerticalGrip", 55);    Set("AngledGrip", 55);
            Set("HeavyStock", 65);      Set("SkeletonStock", 80);
            Set("ExtendedMag", 60);     Set("QuickMag", 55);
            Set("LaserFocusing", 200);  Set("ScatterChoke", 170); Set("AutoHeatSink", 180);
            // Legacy-named attachment ids (no-op if not registered).
            Set("Basic_Scope", 90);     Set("Advanced_Scope", 240);
            Set("Suppressor", 160);     Set("Compensator", 70);
            Set("Long_Barrel", 110);    Set("Short_Barrel", 70); Set("AP_Barrel", 140);
            Set("Extended_Mag", 60);    Set("Fast_Reload_Mag", 55);
            Set("Recoil_Grip", 55);     Set("Stabilized_Stock", 75);
            Set("Overclock_Receiver", 160);

            // Uncommon crafting materials.
            foreach (var id in UncommonMaterials) Set(id, 40);
            // Rare crafting materials.
            foreach (var id in RareMaterials) Set(id, 120);
            // Prize finds — the "graphics card / bitcoin" jackpot loot: far pricier and far
            // scarcer than the rest of the rare band (drop weights set low in ItemBalance).
            // Worth diverting a raid to grab.
            Set("Smart_Targeting_Unit", 550); Set("Phase_Battery", 500);
            Set("Neural_Gel", 700); Set("Crystal_Matrix", 800); Set("Quantum_Relay", 1200);
            // Specialty / intel.
            Set("Military_Intel", 220); Set("Bio_Sample_Case", 190);
            Set("Worn_Warehouse_Key", 220);
        }

        static readonly string[] UncommonMaterials =
        {
            "Hydraulic_Seals", "Gear_Cluster", "Rotary_Motor", "Pneumatic_Valve", "Cooling_Fan",
            "Sensor_Module", "Motion_Sensor", "Camera_Optics", "Flux_Coil", "Pulse_Converter",
            "Ion_Battery", "Conductive_Gel", "Energy_Relay", "Chemical_Catalyst", "Bio_Compound",
            "Filtration_Membrane", "Sterile_Wrap", "Military_Components", "Energy_Core",
        };

        static readonly string[] RareMaterials =
        {
            "Gyro_Stabilizer", "Smart_Targeting_Unit", "Pulse_Emitter", "Adaptive_Circuit",
            "Nano_Filament", "Magnetic_Alloy", "Resonance_Plate", "Synthetic_Quartz",
            "Crystal_Matrix", "Phase_Shard", "Phase_Battery", "Quantum_Relay", "Plasma_Residue",
            "Irradiated_Dust", "Neural_Gel", "Bio_Foam",
        };
    }
}
