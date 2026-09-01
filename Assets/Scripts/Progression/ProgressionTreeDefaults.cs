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
            ProgressionCostDefaults.Apply(list);   // ids must exist first — costs are rolled from them
            return list;
        }

        // ── node helpers ──────────────────────────────────────────
        static ProgressionNodeDef Mn(int r, float o, string label, float mag, string unit) =>
            new() { Size = NodeSize.Minor, Ring = r, Offset = o, StatLabel = label, Magnitude = mag, Unit = unit };
        static ProgressionNodeDef Nt(int r, float o, string name, string label, float mag, string unit) =>
            new() { Size = NodeSize.Notable, Ring = r, Offset = o, DisplayName = name, StatLabel = label, Magnitude = mag, Unit = unit };
        static ProgressionNodeDef MnE(int r, float o, ProgressionEffectType effect, string label, float mag, string unit) =>
            new() { Size = NodeSize.Minor, Ring = r, Offset = o, Effect = effect, StatLabel = label, Magnitude = mag, Unit = unit };
        static ProgressionNodeDef NtE(int r, float o, string name, ProgressionEffectType effect, string label, float mag, string unit) =>
            new() { Size = NodeSize.Notable, Ring = r, Offset = o, DisplayName = name, Effect = effect, StatLabel = label, Magnitude = mag, Unit = unit };
        static ProgressionNodeDef NtSp(int r, float o, string name, string desc) =>
            new() { Size = NodeSize.Notable, Ring = r, Offset = o, DisplayName = name, Description = desc };
        static ProgressionNodeDef Ky(int r, float o, string name, string desc) =>
            new() { Size = NodeSize.Keystone, Ring = r, Offset = o, DisplayName = name, Description = desc };

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
                    Mn(1, 0,   "Max HP", 10, ""),
                    Mn(2, 0,   "Max HP", 10, ""),
                    Mn(3, -15, "Max HP", 12, ""),
                    Nt(3, 15,  "Thick Skin", "Damage Taken", -8, "%"),
                    Mn(4, 15,  "Heal Received", 10, "%"),
                    Ky(5, 0,   "Last Stand", "Below 25% HP: +35% damage resistance and steady health regen.")),
                Branch("Plating",
                    Mn(1, 0,   "Armor Points", 15, ""),
                    Mn(2, 0,   "Armor Durability", 20, "%"),
                    Mn(3, -15, "Ricochet Chance", 10, "%"),
                    Nt(3, 15,  "Hardened Kit", "Armor Durability Loss", -40, "%"),
                    Nt(4, 15,  "Deadweight", "Armor Speed Penalty", -50, "%"),
                    Mn(5, 0,   "Damage Taken", -6, "%")),
                Branch("Spoils",
                    Mn(1, 0,   "Bot Mod Drops", 25, "%"),
                    Mn(2, 0,   "Ammo/Med Box Drops", 1, ""),
                    Mn(3, -15, "Backpack Slots", 2, ""),
                    Nt(3, 15,  "Trophy Taker", "Guaranteed Kill Drop", 1, ""),
                    Mn(4, 15,  "Bot Mod Drops", 25, "%"),
                    Ky(5, 0,   "War Spoils", "Killing a Boss or PMC drops a bonus Rare-or-better item.")),
                Branch("Presence",
                    Mn(1, 0,   "Max Stamina", 10, "%"),
                    Mn(2, 0,   "Heavy Stamina Drain", -15, "%"),
                    Mn(3, -15, "Melee Damage", 8, "%"),
                    Nt(3, 15,  "Bloodied but Unbowed", "Damage Below 50% HP", 12, "%"),
                    Mn(4, -15, "Max HP", 10, ""),
                    NtSp(5, 0, "Juggernaut", "While standing still, +18% damage resistance.")),
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
                    Mn(1, 0,   "Vision Radius", 15, "%"),
                    Mn(2, 0,   "Vision Angle", 12, "°"),
                    Mn(3, -15, "360° Awareness", 20, "%"),
                    Nt(3, 15,  "Eagle Eye", "Scope Reveal", 45, "%"),
                    Mn(4, 15,  "Spotting Duration", 25, "%"),
                    Ky(5, 0,   "Cartographer", "Rare containers and Boss positions are pinged on your map when the raid begins.")),
                Branch("Sound",
                    Mn(1, 0,   "Hearing Range", 20, "%"),
                    Mn(2, 0,   "Footstep Noise", -20, "%"),
                    Mn(3, -15, "Gunfire Noise Range", -5, "%"),
                    Nt(3, 15,  "Blindspot", "Enemy Track Loss", 10, "%"),
                    Mn(4, -15, "Hearing Range", 15, "%"),
                    Nt(5, 0,   "Muffled", "Sprint Noise", -40, "%")),
                Branch("Fortune",
                    Mn(1, 0,   "Rare+ Roll Chance", 15, "%"),
                    Mn(2, 0,   "Loot Pickup Speed", 50, "%"),
                    Mn(3, -15, "Credits Found", 15, "%"),
                    Nt(3, 15,  "Cat Burglar", "Module Cache Drops", 1, ""),
                    Mn(4, 15,  "Rare+ Roll Chance", 10, "%"),
                    Ky(5, 0,   "Golden Eye", "The best item in each container is highlighted and upgraded one rarity tier.")),
                Branch("Fleet",
                    Mn(1, 0,   "Max Stamina", 10, "%"),
                    Mn(2, 0,   "Move Speed", 10, "%"),
                    Mn(3, -15, "Max HP", 8, ""),
                    Nt(3, 15,  "Silent Runner", "Sprint Drain", -25, "%"),
                    Mn(4, 15,  "Dodge Cooldown", -20, "%"),
                    Ky(5, 0,   "Silent Assassin", "Hits on unaware enemies deal +40% damage.")),
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
                    MnE(1, 0,   ProgressionEffectType.WeaponDamage, "Weapon Damage", 6, "%"),
                    MnE(2, 0,   ProgressionEffectType.Penetration, "Penetration", 8, "%"),
                    MnE(3, -15, ProgressionEffectType.ArmorDamage, "Armor Damage", 15, "%"),
                    NtE(3, 15,  "Executioner", ProgressionEffectType.HeadshotDamage, "Headshot Damage", 45, "%"),
                    MnE(4, 15,  ProgressionEffectType.WeaponDamage, "Weapon Damage", 6, "%"),
                    Ky(5, 0,   "Apex Predator", "+30% damage to Bosses and PMCs, and they drop an extra Rare+ item.")),
                Branch("Handling",
                    MnE(1, 0,   ProgressionEffectType.Recoil, "Recoil", -15, "%"),
                    MnE(2, 0,   ProgressionEffectType.RecoilRecovery, "Recoil Recovery", 25, "%"),
                    MnE(3, -15, ProgressionEffectType.ReloadTime, "Reload Time", -15, "%"),
                    NtE(3, 15,  "Steady Hands", ProgressionEffectType.AimSway, "Aim Sway", -30, "%"),
                    MnE(4, -15, ProgressionEffectType.EquipTime, "Equip Time", -20, "%"),
                    NtE(5, 0,   "Cold Barrel", ProgressionEffectType.HeatBuildup, "Heat Buildup", -35, "%")),
                Branch("Bloodlust",
                    MnE(1, 0,   ProgressionEffectType.MaxHp, "Max HP", 8, ""),
                    MnE(2, 0,   ProgressionEffectType.HealPerKill, "Heal per Kill", 5, " HP"),
                    MnE(3, -15, ProgressionEffectType.BleedApplied, "Bleed Applied", 25, "%"),
                    NtE(3, 15,  "Adrenaline", ProgressionEffectType.StaminaPerKill, "Stamina per Kill", 20, "%"),
                    MnE(4, 15,  ProgressionEffectType.MaxHp, "Max HP", 10, ""),
                    Ky(5, 0,   "Berserk", "Below 50% HP: +20% fire rate and hits heal you for a fraction of the damage dealt.")),
                Branch("The Hunt",
                    MnE(1, 0,   ProgressionEffectType.BossSpawnChance, "Boss Spawn Chance", 20, "%"),
                    MnE(2, 0,   ProgressionEffectType.BossKillDrops, "Boss Kill Drops", 1, ""),
                    MnE(3, -15, ProgressionEffectType.CreditsFromLoot, "Credits from Loot", 10, "%"),
                    NtSp(3, 15, "Tracker", "Boss and elite positions are revealed after your first kill of the raid."),
                    MnE(4, 15,  ProgressionEffectType.BossSpawnChance, "Boss Spawn Chance", 15, "%"),
                    Ky(5, 0,   "Big Game", "+35% chance a Boss or a rare high-value spawn appears on the map this raid.")),
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
                    Mn(1, 0,   "Container Drops", 1, ""),
                    Mn(2, 0,   "Container Drops", 1, ""),
                    Mn(3, -15, "Container Slots", 2, ""),
                    Nt(3, 15,  "Deep Pockets", "Backpack Slots", 4, ""),
                    Mn(4, 15,  "Weapon Slots", 1, ""),
                    Nt(5, 0,   "Pack Rat", "Container Drops", 2, "")),
                Branch("Fortune",
                    Mn(1, 0,   "Rare+ Roll Chance", 15, "%"),
                    Mn(2, 0,   "Credits Found", 20, "%"),
                    Mn(3, -15, "Loot Box Drops", 1, ""),
                    Nt(3, 15,  "Lucky Find", "Rare+ Roll Chance", 30, "%"),
                    Mn(4, 15,  "Credits Found", 15, "%"),
                    Ky(5, 0,   "Golden Touch", "The best drop in each container gains +1 rarity tier — but enemies hear you 30% farther.")),
                Branch("Endurance",
                    Mn(1, 0,   "Max Stamina", 12, "%"),
                    Mn(2, 0,   "Move Speed", 8, "%"),
                    Mn(3, -15, "Max HP", 10, ""),
                    Nt(3, 15,  "Pack Mule", "Carry Speed Penalty", -100, "%"),
                    Mn(4, -15, "Max HP", 10, ""),
                    Nt(5, 0,   "Second Wind", "Stamina Regen", 30, "%")),
                Branch("Getaway",
                    Mn(1, 0,   "Heal Speed", 20, "%"),
                    Mn(2, 0,   "Med Use Delay", -1, "s"),
                    Mn(3, -15, "Bleed Taken", -40, "%"),
                    NtSp(3, 15, "Field Dressing", "Bandages instantly stop all bleeding."),
                    Mn(4, 15,  "Heal Speed", 15, "%"),
                    Ky(5, 0,   "Secure Pocket", "Keep one random slot of loot even if you die in the raid.")),
            },
        };
    }
}
