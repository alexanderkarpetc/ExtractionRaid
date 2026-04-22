using System.Collections.Generic;

namespace View.UI.CraftingMockup
{
    /// <summary>
    /// Static mock data for the UI Toolkit crafting window. No logic wiring —
    /// mirrors the HTML mockup 1:1 for visual parity testing.
    /// </summary>
    public static class CraftingMockupData
    {
        public enum Rarity { Common, Uncommon, Rare, Epic }

        public readonly struct Tab
        {
            public readonly string Key;
            public readonly string Label;
            public Tab(string key, string label) { Key = key; Label = label; }
        }

        public readonly struct TypeFilter
        {
            public readonly string Key;
            public readonly string Label;
            public TypeFilter(string key, string label) { Key = key; Label = label; }
        }

        public readonly struct Requirement
        {
            public readonly string Name;
            public readonly string Icon;
            public readonly int Have;
            public readonly int Need;
            public Requirement(string name, string icon, int have, int need)
            { Name = name; Icon = icon; Have = have; Need = need; }
        }

        public readonly struct StatRow
        {
            public readonly string Label;
            public readonly string Display;
            public StatRow(string label, string display) { Label = label; Display = display; }
        }

        public sealed class Item
        {
            public string Id;
            public string Category;
            public string Type;
            public string Icon;
            public int Count;
            public string Title;
            public string Subtitle;
            public Rarity Rarity;
            public string Description;
            public List<StatRow> Stats;
            public List<Requirement> Requirements;
            public string Workbench;
            public string CraftTime;
        }

        public static readonly Tab[] Tabs =
        {
            new Tab("meds", "Meds"),
            new Tab("weapons", "Weapons"),
            new Tab("ammo", "Ammo"),
            new Tab("mods", "Mods"),
            new Tab("tools", "Tools"),
            new Tab("utils", "Utils"),
        };

        public static TypeFilter[] GetTypeFilters(string tabKey)
        {
            switch (tabKey)
            {
                case "meds": return new[] {
                    new TypeFilter("all", "All"),
                    new TypeFilter("medical", "Med"),
                    new TypeFilter("material", "Mat"),
                };
                case "weapons": return new[] {
                    new TypeFilter("all", "All"),
                    new TypeFilter("weapon", "Gun"),
                    new TypeFilter("throwable", "Throw"),
                };
                case "ammo": return new[] { new TypeFilter("all", "All"), new TypeFilter("ammo", "Ammo") };
                case "mods": return new[] { new TypeFilter("all", "All"), new TypeFilter("mod", "Mod") };
                case "tools": return new[] { new TypeFilter("all", "All"), new TypeFilter("tool", "Tool") };
                case "utils": return new[] { new TypeFilter("all", "All"), new TypeFilter("utility", "Util") };
                default: return new[] { new TypeFilter("all", "All") };
            }
        }

        public static readonly List<Item> Items = new List<Item>
        {
            new Item {
                Id = "field-medkit", Category = "meds", Type = "medical", Icon = "+", Count = 201,
                Title = "Field Medkit", Subtitle = "Portable medkit", Rarity = Rarity.Uncommon,
                Description = "Portable medkit for field use. Restores moderate health over time and stops light bleeding. Compact enough to carry in a tactical pouch without sacrificing too much inventory space.",
                Stats = new List<StatRow> {
                    new StatRow("Healing", "72"),
                    new StatRow("Bleed Control", "64"),
                    new StatRow("Use Time", "3.5 s"),
                    new StatRow("Weight", "0.4 kg"),
                },
                Requirements = new List<Requirement> {
                    new Requirement("Bandage", "B", 2, 2),
                    new Requirement("Antiseptic", "A", 1, 1),
                    new Requirement("Rag", "R", 5, 3),
                },
                Workbench = "Level 1", CraftTime = "00:18",
            },
            new Item {
                Id = "advanced-medkit", Category = "meds", Type = "medical", Icon = "++", Count = 0,
                Title = "Advanced Medkit", Subtitle = "Emergency trauma kit", Rarity = Rarity.Rare,
                Description = "A larger trauma-oriented kit used for critical stabilization. Slower to craft and heavier to carry, but significantly more effective in drawn-out encounters.",
                Stats = new List<StatRow> {
                    new StatRow("Healing", "92"),
                    new StatRow("Bleed Control", "88"),
                    new StatRow("Use Time", "5.8 s"),
                    new StatRow("Weight", "0.9 kg"),
                },
                Requirements = new List<Requirement> {
                    new Requirement("Field Medkit", "+", 2, 2),
                    new Requirement("Antiseptic", "A", 1, 2),
                    new Requirement("Blood Bag", "BB", 0, 1),
                    new Requirement("Injector", "Inj", 1, 1),
                },
                Workbench = "Level 2", CraftTime = "00:52",
            },
            new Item {
                Id = "bandage", Category = "meds", Type = "medical", Icon = "B", Count = 31,
                Title = "Bandage", Subtitle = "Basic consumable", Rarity = Rarity.Common,
                Description = "Simple wound dressing used to stop light bleeding. Cheap, common, and useful as a core ingredient in many medical recipes.",
                Stats = new List<StatRow> {
                    new StatRow("Healing", "18"),
                    new StatRow("Bleed Control", "34"),
                    new StatRow("Use Time", "1.4 s"),
                },
                Requirements = new List<Requirement> { new Requirement("Rag", "R", 5, 2) },
                Workbench = "Level 1", CraftTime = "00:08",
            },
            new Item {
                Id = "antiseptic", Category = "meds", Type = "medical", Icon = "A", Count = 143,
                Title = "Antiseptic", Subtitle = "Disinfectant bottle", Rarity = Rarity.Common,
                Description = "A disinfectant solution required for clean treatment procedures and advanced medical crafting.",
                Stats = new List<StatRow> { new StatRow("Purity", "60"), new StatRow("Volume", "250 ml") },
                Requirements = new List<Requirement> {
                    new Requirement("Chemicals", "Ch", 5, 1),
                    new Requirement("Bottle", "Bt", 3, 1),
                },
                Workbench = "Level 1", CraftTime = "00:12",
            },
            new Item {
                Id = "rag", Category = "meds", Type = "material", Icon = "R", Count = 312,
                Title = "Rag", Subtitle = "Fabric scrap", Rarity = Rarity.Common,
                Description = "Fabric scrap used in field repairs, cleaning, and emergency medical crafting.",
                Stats = new List<StatRow> { new StatRow("Durability", "16"), new StatRow("Softness", "24") },
                Requirements = new List<Requirement> { new Requirement("Cloth Scrap", "Cl", 8, 2) },
                Workbench = "Level 1", CraftTime = "00:06",
            },
            new Item {
                Id = "painkillers", Category = "meds", Type = "medical", Icon = "P", Count = 76,
                Title = "Painkillers", Subtitle = "Tablet pack", Rarity = Rarity.Common,
                Description = "Suppresses pain effects for a short time and improves combat survivability in emergencies.",
                Stats = new List<StatRow> { new StatRow("Pain Relief", "58"), new StatRow("Duration", "35 s") },
                Requirements = new List<Requirement> {
                    new Requirement("Chemicals", "Ch", 5, 2),
                    new Requirement("Pack", "Pk", 2, 1),
                },
                Workbench = "Level 1", CraftTime = "00:16",
            },
            new Item {
                Id = "adrenaline", Category = "meds", Type = "medical", Icon = "Ad", Count = 24,
                Title = "Adrenaline", Subtitle = "Combat stim", Rarity = Rarity.Rare,
                Description = "Short burst stimulant that boosts movement and responsiveness, but may have post-use penalties.",
                Stats = new List<StatRow> {
                    new StatRow("Stamina Boost", "80"),
                    new StatRow("Speed Boost", "+12%"),
                    new StatRow("Duration", "18 s"),
                },
                Requirements = new List<Requirement> {
                    new Requirement("Injector", "Inj", 1, 1),
                    new Requirement("Chemicals", "Ch", 5, 3),
                    new Requirement("Stabilizer", "St", 0, 1),
                },
                Workbench = "Level 2", CraftTime = "00:44",
            },

            new Item {
                Id = "rusty-pistol", Category = "weapons", Type = "weapon", Icon = "RP", Count = 0,
                Title = "Rusty Pistol", Subtitle = "Handgun", Rarity = Rarity.Common,
                Description = "An old sidearm pieced together from mismatched parts. Unreliable but serviceable in the early game.",
                Stats = new List<StatRow> {
                    new StatRow("Damage", "19"),
                    new StatRow("Fire Rate", "310 RPM"),
                    new StatRow("Accuracy", "42"),
                    new StatRow("Recoil Control", "34"),
                    new StatRow("Durability", "27 / 100"),
                },
                Requirements = new List<Requirement> {
                    new Requirement("Metal Parts", "MP", 12, 6),
                    new Requirement("Barrel", "Bar", 0, 1),
                    new Requirement("Spring", "Sp", 6, 2),
                    new Requirement("Grip", "Gr", 3, 1),
                },
                Workbench = "Level 1", CraftTime = "01:20",
            },
            new Item {
                Id = "smg-45", Category = "weapons", Type = "weapon", Icon = "SMG", Count = 0,
                Title = "SMG-45", Subtitle = "Submachine Gun", Rarity = Rarity.Uncommon,
                Description = "A compact submachine gun designed for close-quarters combat. High rate of fire with low vertical recoil makes it effective in corridors, interiors, and urban ambushes. Built around a simple blowback system, it is easy to maintain and forgiving for players who prefer aggressive flanks.",
                Stats = new List<StatRow> {
                    new StatRow("Damage", "24"),
                    new StatRow("Fire Rate", "950 RPM"),
                    new StatRow("Accuracy", "48"),
                    new StatRow("Recoil Control", "62"),
                    new StatRow("Ergonomics", "68"),
                    new StatRow("Magazine Size", "30"),
                    new StatRow("Reload Time", "2.1 s"),
                    new StatRow("Effective Range", "60 m"),
                    new StatRow("Muzzle Velocity", "400 m/s"),
                    new StatRow("Weight", "2.45 kg"),
                    new StatRow("Durability", "100 / 100"),
                },
                Requirements = new List<Requirement> {
                    new Requirement("Metal Parts", "MP", 12, 12),
                    new Requirement("Polymer", "Pl", 8, 8),
                    new Requirement("Screws", "Sc", 15, 15),
                    new Requirement("Spring", "Sp", 6, 6),
                    new Requirement("Duct Tape", "DT", 4, 4),
                    new Requirement("Gun Powder", "GP", 10, 10),
                    new Requirement("Tool Kit", "TK", 1, 1),
                },
                Workbench = "Level 2", CraftTime = "02:30",
            },
            new Item {
                Id = "shotgun-m870", Category = "weapons", Type = "weapon", Icon = "SG", Count = 0,
                Title = "Shotgun M870", Subtitle = "Pump-action shotgun", Rarity = Rarity.Rare,
                Description = "Reliable close-range weapon with devastating stopping power, balanced by long reloads and heavy spread.",
                Stats = new List<StatRow> {
                    new StatRow("Damage", "86"),
                    new StatRow("Fire Rate", "80 RPM"),
                    new StatRow("Accuracy", "30"),
                    new StatRow("Recoil Control", "26"),
                    new StatRow("Durability", "88 / 100"),
                },
                Requirements = new List<Requirement> {
                    new Requirement("Metal Parts", "MP", 12, 10),
                    new Requirement("Wood Parts", "WP", 5, 4),
                    new Requirement("Barrel", "Bar", 0, 1),
                    new Requirement("Spring", "Sp", 6, 3),
                },
                Workbench = "Level 2", CraftTime = "03:10",
            },
            new Item {
                Id = "hunting-rifle", Category = "weapons", Type = "weapon", Icon = "HR", Count = 0,
                Title = "Hunting Rifle", Subtitle = "Bolt-action rifle", Rarity = Rarity.Uncommon,
                Description = "Simple long-range rifle with strong per-shot impact and moderate crafting requirements.",
                Stats = new List<StatRow> {
                    new StatRow("Damage", "78"),
                    new StatRow("Fire Rate", "42 RPM"),
                    new StatRow("Accuracy", "84"),
                    new StatRow("Recoil Control", "44"),
                    new StatRow("Durability", "80 / 100"),
                },
                Requirements = new List<Requirement> {
                    new Requirement("Metal Parts", "MP", 12, 9),
                    new Requirement("Wood Parts", "WP", 5, 5),
                    new Requirement("Scope Mount", "Sm", 0, 1),
                },
                Workbench = "Level 2", CraftTime = "02:45",
            },
            new Item {
                Id = "molotov", Category = "weapons", Type = "throwable", Icon = "Mol", Count = 87,
                Title = "Molotov", Subtitle = "Incendiary throwable", Rarity = Rarity.Common,
                Description = "Improvised fire bomb for area denial and panic pressure.",
                Stats = new List<StatRow> {
                    new StatRow("Burn Radius", "2.8 m"),
                    new StatRow("Duration", "7 s"),
                    new StatRow("Throw Range", "18 m"),
                },
                Requirements = new List<Requirement> {
                    new Requirement("Bottle", "Bt", 3, 1),
                    new Requirement("Fuel", "Fu", 2, 1),
                    new Requirement("Rag", "R", 5, 1),
                },
                Workbench = "Level 1", CraftTime = "00:14",
            },
            new Item {
                Id = "stun-grenade", Category = "weapons", Type = "throwable", Icon = "Stn", Count = 45,
                Title = "Stun Grenade", Subtitle = "Tactical throwable", Rarity = Rarity.Uncommon,
                Description = "Disorients enemies in a short radius and opens aggressive entry windows.",
                Stats = new List<StatRow> {
                    new StatRow("Stun Radius", "4.1 m"),
                    new StatRow("Fuse", "1.5 s"),
                },
                Requirements = new List<Requirement> {
                    new Requirement("Shell", "Sh", 2, 1),
                    new Requirement("Chemicals", "Ch", 5, 2),
                    new Requirement("Fuse", "Fs", 8, 1),
                },
                Workbench = "Level 2", CraftTime = "00:22",
            },

            new Item {
                Id = "flashlight", Category = "tools", Type = "tool", Icon = "Fl", Count = 28,
                Title = "Flashlight", Subtitle = "Basic utility tool", Rarity = Rarity.Common,
                Description = "Compact handheld flashlight. Cheap to make and useful in dark interiors.",
                Stats = new List<StatRow> {
                    new StatRow("Brightness", "320 lm"),
                    new StatRow("Battery Life", "2 h"),
                },
                Requirements = new List<Requirement> {
                    new Requirement("Housing", "Hs", 8, 1),
                    new Requirement("Bulb", "Bl", 2, 1),
                    new Requirement("Battery", "Bat", 4, 2),
                },
                Workbench = "Level 1", CraftTime = "00:25",
            },

            new Item {
                Id = "ammo-9mm", Category = "ammo", Type = "ammo", Icon = "9mm", Count = 180,
                Title = "9mm Ammo", Subtitle = "Standard pistol ammo", Rarity = Rarity.Common,
                Description = "Basic handgun ammunition crafted in small batches.",
                Stats = new List<StatRow> {
                    new StatRow("Penetration", "34"),
                    new StatRow("Damage", "22"),
                },
                Requirements = new List<Requirement> {
                    new Requirement("Brass", "Br", 12, 4),
                    new Requirement("Gun Powder", "GP", 10, 2),
                    new Requirement("Primer", "Pr", 8, 4),
                },
                Workbench = "Level 1", CraftTime = "00:18",
            },

            new Item {
                Id = "rifle-scope", Category = "mods", Type = "mod", Icon = "Sc", Count = 3,
                Title = "Rifle Scope", Subtitle = "Optic attachment", Rarity = Rarity.Rare,
                Description = "Precision optic for long-range engagements. Requires cleaner assembly and better workbench tools.",
                Stats = new List<StatRow> {
                    new StatRow("Zoom", "4x"),
                    new StatRow("Clarity", "62"),
                    new StatRow("Weight", "0.5 kg"),
                },
                Requirements = new List<Requirement> {
                    new Requirement("Lens", "Ln", 1, 2),
                    new Requirement("Metal Parts", "MP", 12, 4),
                    new Requirement("Mount", "Mt", 0, 1),
                },
                Workbench = "Level 2", CraftTime = "01:40",
            },
        };

        public static bool CanCraft(Item item)
        {
            foreach (var r in item.Requirements)
                if (r.Have < r.Need) return false;
            return true;
        }
    }
}
