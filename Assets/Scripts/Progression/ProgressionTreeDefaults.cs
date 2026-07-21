using System.Collections.Generic;
using UnityEngine;

namespace Progression
{
    /// <summary>
    /// Built-in default content for the progression tree, ported from the design concept
    /// (Assets/Concepts/progression_tree_concept.html). Used to seed the editable asset
    /// ("Seed Default Tree") and as a runtime fallback when no asset exists.
    ///
    /// Node ids are assigned here as "<discipline>.<branchIndex>.<nodeIndex>" so they stay
    /// stable for save data and for <see cref="ProgressionSystem.ApplyAllocatedEffects"/>.
    /// </summary>
    public static class ProgressionTreeDefaults
    {
        static readonly Color Warden     = new(1f,    0.42f,  0.365f);
        static readonly Color Phantom    = new(0.529f, 0.902f, 1f);
        static readonly Color Predator   = new(1f,    0.714f, 0.282f);
        static readonly Color Prospector = new(0.753f, 0.549f, 1f);

        public static ProgressionTreeConfig BuildRuntime()
        {
            var cfg = ScriptableObject.CreateInstance<ProgressionTreeConfig>();
            cfg.name = "ProgressionTree (defaults)";
            cfg.Disciplines = BuildDisciplines();
            return cfg;
        }

        public static List<ProgressionDisciplineDef> BuildDisciplines()
        {
            var list = new List<ProgressionDisciplineDef>
            {
                Warden_(), Phantom_(), Predator_(), Prospector_(),
            };
            AssignIds(list);
            return list;
        }

        // ── node helpers ──────────────────────────────────────────
        static ProgressionNodeDef Mn(int r, float o, string label, float mag, string unit, string hook) =>
            new() { Size = NodeSize.Minor, Ring = r, Offset = o, StatLabel = label, Magnitude = mag, Unit = unit, DevHook = hook };
        static ProgressionNodeDef Nt(int r, float o, string name, string label, float mag, string unit, string hook) =>
            new() { Size = NodeSize.Notable, Ring = r, Offset = o, DisplayName = name, StatLabel = label, Magnitude = mag, Unit = unit, DevHook = hook };
        static ProgressionNodeDef NtSp(int r, float o, string name, string desc, string hook) =>
            new() { Size = NodeSize.Notable, Ring = r, Offset = o, DisplayName = name, Description = desc, DevHook = hook };
        static ProgressionNodeDef Ky(int r, float o, string name, string desc, string hook) =>
            new() { Size = NodeSize.Keystone, Ring = r, Offset = o, DisplayName = name, Description = desc, DevHook = hook };

        static ProgressionBranchDef Branch(string name, params ProgressionNodeDef[] nodes) =>
            new() { Name = name, Nodes = new List<ProgressionNodeDef>(nodes) };

        static void AssignIds(List<ProgressionDisciplineDef> disciplines)
        {
            foreach (var d in disciplines)
                for (int bi = 0; bi < d.Branches.Count; bi++)
                {
                    var b = d.Branches[bi];
                    for (int ni = 0; ni < b.Nodes.Count; ni++)
                        b.Nodes[ni].Id = $"{d.Id}.{bi}.{ni}";
                }
        }

        // ── WARDEN — take the hit, take their stuff ───────────────
        static ProgressionDisciplineDef Warden_() => new()
        {
            Id = "warden", DisplayName = "Warden", Color = Warden, AngleDeg = 135f,
            Tagline = "Take the hit, take their stuff",
            Branches = new List<ProgressionBranchDef>
            {
                Branch("Flesh",
                    Mn(1, 0,   "Max HP", 10, "", "BotConstants.PlayerMaxHp"),
                    Mn(2, 0,   "Max HP", 10, "", "BotConstants.PlayerMaxHp"),
                    Mn(3, -15, "Max HP", 12, "", "BotConstants.PlayerMaxHp"),
                    Nt(3, 15,  "Thick Skin", "Damage Taken", -8, "%", "ArmorConstants.DamageReductionK"),
                    Mn(4, 15,  "Heal Received", 10, "%", "MedConstants.HealPerSecond"),
                    Ky(5, 0,   "Last Stand", "Below 25% HP: +35% damage resistance and steady health regen.", "HealthState.CurrentHp guard")),
                Branch("Plating",
                    Mn(1, 0,   "Armor Points", 15, "", "ItemDefinition.ArmorPoints"),
                    Mn(2, 0,   "Armor Durability", 20, "%", "ArmorState.MaxDurability"),
                    Mn(3, -15, "Ricochet Chance", 10, "%", "ArmorConstants.RicochetChance"),
                    Nt(3, 15,  "Hardened Kit", "Armor Durability Loss", -40, "%", "ArmorConstants.ArmorDamageCap"),
                    Nt(4, 15,  "Deadweight", "Armor Speed Penalty", -50, "%", "ArmorConstants.WeightSpeedFactor"),
                    Mn(5, 0,   "Damage Taken", -6, "%", "ArmorConstants.DamageReductionK")),
                Branch("Spoils",
                    Mn(1, 0,   "Bot Mod Drops", 25, "%", "LootSystem.BotModDropChance"),
                    Mn(2, 0,   "Ammo/Med Box Drops", 1, "", "ContainerTypeConfig.MaxDrops"),
                    Mn(3, -15, "Backpack Slots", 2, "", "InventoryState.BackpackSize"),
                    Nt(3, 15,  "Trophy Taker", "Guaranteed Kill Drop", 1, "", "LootSystem.CreateLootable"),
                    Mn(4, 15,  "Bot Mod Drops", 25, "%", "LootSystem.BotModDropChance"),
                    Ky(5, 0,   "War Spoils", "Killing a Boss or PMC drops a bonus Rare-or-better item.", "LootSystem + RarityTier")),
                Branch("Presence",
                    Mn(1, 0,   "Max Stamina", 10, "%", "StaminaConstants.MaxStamina"),
                    Mn(2, 0,   "Heavy Stamina Drain", -15, "%", "StaminaConstants.SprintDrainRate"),
                    Mn(3, -15, "Melee Damage", 8, "%", "WeaponStats.Damage"),
                    Nt(3, 15,  "Bloodied but Unbowed", "Damage Below 50% HP", 12, "%", "ShootingConfig.DamageMultiplier"),
                    Mn(4, -15, "Max HP", 10, "", "BotConstants.PlayerMaxHp"),
                    NtSp(5, 0, "Juggernaut", "While standing still, +18% damage resistance.", "ArmorConstants.DamageReductionK")),
            },
        };

        // ── PHANTOM — see everything, leave no trace ──────────────
        static ProgressionDisciplineDef Phantom_() => new()
        {
            Id = "phantom", DisplayName = "Phantom", Color = Phantom, AngleDeg = 225f,
            Tagline = "See everything, leave no trace",
            Branches = new List<ProgressionBranchDef>
            {
                Branch("Sight",
                    Mn(1, 0,   "Vision Radius", 15, "%", "FOVConfig.FarRadius"),
                    Mn(2, 0,   "Vision Angle", 12, "°", "FOVConfig.Angle"),
                    Mn(3, -15, "360° Awareness", 20, "%", "FOVConfig.NearRadius"),
                    Nt(3, 15,  "Eagle Eye", "Scope Reveal", 45, "%", "PlayerEntityState.ScopeRadius"),
                    Mn(4, 15,  "Spotting Duration", 25, "%", "PlayerFOVSystem"),
                    Ky(5, 0,   "Cartographer", "Rare containers and Boss positions are pinged on your map when the raid begins.", "BotSpawnSystem + LootContainerSpawnPoint")),
                Branch("Sound",
                    Mn(1, 0,   "Hearing Range", 20, "%", "FOVConfig hearing radius"),
                    Mn(2, 0,   "Footstep Noise", -20, "%", "MovementSystem noise"),
                    Mn(3, -15, "Gunfire Noise Range", -5, "%", "shooting noise radius"),
                    Nt(3, 15,  "Blindspot", "Enemy Track Loss", 10, "%", "BotTypeConfig.ReactionTime"),
                    Mn(4, -15, "Hearing Range", 15, "%", "FOVConfig hearing radius"),
                    Nt(5, 0,   "Muffled", "Sprint Noise", -40, "%", "MovementSystem")),
                Branch("Fortune",
                    Mn(1, 0,   "Rare+ Roll Chance", 15, "%", "RarityTier / LootConstants.ValueWeight"),
                    Mn(2, 0,   "Loot Pickup Speed", 50, "%", "interaction time"),
                    Mn(3, -15, "Credits Found", 15, "%", "PlayerProfileState.Credits"),
                    Nt(3, 15,  "Cat Burglar", "Module Cache Drops", 1, "", "ContainerConstants.ModuleCache"),
                    Mn(4, 15,  "Rare+ Roll Chance", 10, "%", "RarityTier"),
                    Ky(5, 0,   "Golden Eye", "The best item in each container is highlighted and upgraded one rarity tier.", "RarityTier")),
                Branch("Fleet",
                    Mn(1, 0,   "Max Stamina", 10, "%", "StaminaConstants.MaxStamina"),
                    Mn(2, 0,   "Move Speed", 10, "%", "MovementConfig.MoveSpeedMultiplier"),
                    Mn(3, -15, "Max HP", 8, "", "BotConstants.PlayerMaxHp"),
                    Nt(3, 15,  "Silent Runner", "Sprint Drain", -25, "%", "StaminaConstants.SprintDrainRate"),
                    Mn(4, 15,  "Dodge Cooldown", -20, "%", "DodgeConstants.Cooldown"),
                    Ky(5, 0,   "Silent Assassin", "Hits on unaware enemies deal +40% damage.", "DamageSystem")),
            },
        };

        // ── PREDATOR — hunt the biggest game ──────────────────────
        static ProgressionDisciplineDef Predator_() => new()
        {
            Id = "predator", DisplayName = "Predator", Color = Predator, AngleDeg = 315f,
            Tagline = "Hunt the biggest game",
            Branches = new List<ProgressionBranchDef>
            {
                Branch("Lethality",
                    Mn(1, 0,   "Weapon Damage", 6, "%", "ShootingConfig.DamageMultiplier"),
                    Mn(2, 0,   "Penetration", 8, "%", "WeaponStats.BasePenetration"),
                    Mn(3, -15, "Armor Damage", 15, "%", "WeaponStats.BaseArmorDamage"),
                    Nt(3, 15,  "Executioner", "Headshot Damage", 45, "%", "WeaponStats.HeadshotDamageMultiplier"),
                    Mn(4, 15,  "Weapon Damage", 6, "%", "ShootingConfig.DamageMultiplier"),
                    Ky(5, 0,   "Apex Predator", "+30% damage to Bosses and PMCs, and they drop an extra Rare+ item.", "DamageSystem + LootSystem")),
                Branch("Handling",
                    Mn(1, 0,   "Recoil", -15, "%", "ShootingConfig.RecoilMultiplier"),
                    Mn(2, 0,   "Recoil Recovery", 25, "%", "AimConfig.RecoilRecoveryMultiplier"),
                    Mn(3, -15, "Reload Time", -15, "%", "WeaponStats.ReloadTime"),
                    Nt(3, 15,  "Steady Hands", "Aim Sway", -30, "%", "AimConfig.AimFollowMultiplier"),
                    Mn(4, -15, "Equip Time", -20, "%", "WeaponStats.EquipTime"),
                    Nt(5, 0,   "Cold Barrel", "Heat Buildup", -35, "%", "BarrelHeatConfig")),
                Branch("Bloodlust",
                    Mn(1, 0,   "Max HP", 8, "", "BotConstants.PlayerMaxHp"),
                    Mn(2, 0,   "Heal per Kill", 5, " HP", "DamageSystem on-kill"),
                    Mn(3, -15, "Bleed Applied", 25, "%", "WeaponStats.BaseBleedChance"),
                    Nt(3, 15,  "Adrenaline", "Stamina per Kill", 20, "%", "StaminaSystem"),
                    Mn(4, 15,  "Max HP", 10, "", "BotConstants.PlayerMaxHp"),
                    Ky(5, 0,   "Berserk", "Below 50% HP: +20% fire rate and hits heal you for a fraction of the damage dealt.", "ShootingConfig + DamageSystem")),
                Branch("The Hunt",
                    Mn(1, 0,   "Boss Spawn Chance", 20, "%", "BotSpawnPoint.spawnChance"),
                    Mn(2, 0,   "Boss Kill Drops", 1, "", "LootSystem"),
                    Mn(3, -15, "Credits from Loot", 10, "%", "PlayerProfileState.Credits"),
                    NtSp(3, 15, "Tracker", "Boss and elite positions are revealed after your first kill of the raid.", "BotSpawnSystem"),
                    Mn(4, 15,  "Boss Spawn Chance", 15, "%", "BotSpawnPoint.spawnChance"),
                    Ky(5, 0,   "Big Game", "+35% chance a Boss or a rare high-value spawn appears on the map this raid.", "BotSpawnPoint.spawnChance")),
            },
        };

        // ── PROSPECTOR — grab it all, live to spend it ────────────
        static ProgressionDisciplineDef Prospector_() => new()
        {
            Id = "prospector", DisplayName = "Prospector", Color = Prospector, AngleDeg = 45f,
            Tagline = "Grab it all, live to spend it",
            Branches = new List<ProgressionBranchDef>
            {
                Branch("Haul",
                    Mn(1, 0,   "Container Drops", 1, "", "ContainerTypeConfig.MaxDrops"),
                    Mn(2, 0,   "Container Drops", 1, "", "ContainerTypeConfig.MaxDrops"),
                    Mn(3, -15, "Container Slots", 2, "", "ContainerTypeConfig.SlotCount"),
                    Nt(3, 15,  "Deep Pockets", "Backpack Slots", 4, "", "InventoryState.BackpackSize"),
                    Mn(4, 15,  "Weapon Slots", 1, "", "InventoryState.WeaponSlotCount"),
                    Nt(5, 0,   "Pack Rat", "Container Drops", 2, "", "ContainerTypeConfig.MaxDrops")),
                Branch("Fortune",
                    Mn(1, 0,   "Rare+ Roll Chance", 15, "%", "RarityTier / LootConstants.ValueWeight"),
                    Mn(2, 0,   "Credits Found", 20, "%", "PlayerProfileState.Credits"),
                    Mn(3, -15, "Loot Box Drops", 1, "", "ContainerConstants.RandomLootBox"),
                    Nt(3, 15,  "Lucky Find", "Rare+ Roll Chance", 30, "%", "RarityTier"),
                    Mn(4, 15,  "Credits Found", 15, "%", "PlayerProfileState.Credits"),
                    Ky(5, 0,   "Golden Touch", "The best drop in each container gains +1 rarity tier — but enemies hear you 30% farther.", "RarityTier + noise")),
                Branch("Endurance",
                    Mn(1, 0,   "Max Stamina", 12, "%", "StaminaConstants.MaxStamina"),
                    Mn(2, 0,   "Move Speed", 8, "%", "MovementConfig.MoveSpeedMultiplier"),
                    Mn(3, -15, "Max HP", 10, "", "BotConstants.PlayerMaxHp"),
                    Nt(3, 15,  "Pack Mule", "Carry Speed Penalty", -100, "%", "ArmorConstants.WeightSpeedFactor"),
                    Mn(4, -15, "Max HP", 10, "", "BotConstants.PlayerMaxHp"),
                    Nt(5, 0,   "Second Wind", "Stamina Regen", 30, "%", "StaminaConstants.RegenRate")),
                Branch("Getaway",
                    Mn(1, 0,   "Heal Speed", 20, "%", "MedConstants.HealPerSecond"),
                    Mn(2, 0,   "Med Use Delay", -1, "s", "MedConstants.UseDelay"),
                    Mn(3, -15, "Bleed Taken", -40, "%", "StatusEffectConstants"),
                    NtSp(3, 15, "Field Dressing", "Bandages instantly stop all bleeding.", "StatusEffectConstants"),
                    Mn(4, 15,  "Heal Speed", 15, "%", "MedConstants.HealPerSecond"),
                    Ky(5, 0,   "Secure Pocket", "Keep one random slot of loot even if you die in the raid.", "InventoryState")),
            },
        };
    }
}
