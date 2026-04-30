using System.Collections.Generic;

namespace Constants
{
    public enum CraftCategory : byte
    {
        Meds,
        Weapons,
        Ammo,
        WeaponMods,
    }

    public readonly struct CraftIngredient
    {
        public readonly string DefinitionId;
        public readonly int Count;

        public CraftIngredient(string definitionId, int count)
        {
            DefinitionId = definitionId;
            Count = count;
        }
    }

    public readonly struct CraftRecipe
    {
        public readonly string RecipeId;
        public readonly string DisplayName;
        public readonly string Description;
        public readonly CraftCategory Category;
        public readonly string ResultItemId;
        public readonly int ResultCount;
        public readonly CraftIngredient[] Ingredients;

        public CraftRecipe(string recipeId, string displayName, string description,
            CraftCategory category, string resultItemId, int resultCount,
            CraftIngredient[] ingredients)
        {
            RecipeId = recipeId;
            DisplayName = displayName;
            Description = description;
            Category = category;
            ResultItemId = resultItemId;
            ResultCount = resultCount;
            Ingredients = ingredients;
        }
    }

    public static class CraftConstants
    {
        // ── Meds ─────────────────────────────────────────────────

        public static readonly CraftRecipe Bandage = new(
            "Bandage", "Bandage", "Basic wound dressing to stop bleeding.",
            CraftCategory.Meds, "Bandage", 1,
            new[]
            {
                new CraftIngredient("Cloth", 2),
                new CraftIngredient("Adhesive", 1),
            });

        public static readonly CraftRecipe FieldMedkit = new(
            "FieldMedkit", "Field Medkit", "Standard field medical kit for treating injuries.",
            CraftCategory.Meds, "Medkit", 1,
            new[]
            {
                new CraftIngredient("Cloth", 3),
                new CraftIngredient("Chemicals", 2),
                new CraftIngredient("Adhesive", 2),
                new CraftIngredient("Plastic", 1),
            });

        public static readonly CraftRecipe AdvancedMedkit = new(
            "AdvancedMedkit", "Advanced Medkit", "High-grade medical kit with electronic diagnostics.",
            CraftCategory.Meds, "Advanced_Medkit", 1,
            new[]
            {
                new CraftIngredient("Cloth", 4),
                new CraftIngredient("Chemicals", 4),
                new CraftIngredient("Adhesive", 3),
                new CraftIngredient("Electronics", 1),
            });

        // ── Weapon Builder Modules (Cluster A 2026-05-01) ────────
        // Replaces ImprovisedRifle recipe — player crafts modules at workbench
        // and assembles weapons via Weapon Builder. Each recipe outputs 1×
        // module (non-stackable). Costs roughly half a full-weapon recipe
        // since each weapon = 1×payload + 1×delivery.

        public static readonly CraftRecipe BallisticRoundModule = new(
            "BallisticRoundModule", "Ballistic Round (Payload)",
            "Standard kinetic round payload — outputs metal-jacketed projectiles.",
            CraftCategory.Weapons, "BallisticRound", 1,
            new[]
            {
                new CraftIngredient("Metal_Parts", 4),
                new CraftIngredient("Gunpowder", 2),
            });

        public static readonly CraftRecipe LaserChargeModule = new(
            "LaserChargeModule", "Laser Charge (Payload)",
            "Energy charge payload — fires after a brief charge-up. Pricey.",
            CraftCategory.Weapons, "LaserCharge", 1,
            new[]
            {
                new CraftIngredient("Electronics", 4),
                new CraftIngredient("Glass", 1),
                new CraftIngredient("Metal_Parts", 2),
            });

        public static readonly CraftRecipe SingleActionModule = new(
            "SingleActionModule", "Single-Action (Delivery)",
            "Compact pistol-form mechanism — single-shot trigger.",
            CraftCategory.Weapons, "SingleAction", 1,
            new[]
            {
                new CraftIngredient("Metal_Parts", 3),
                new CraftIngredient("Mechanical_Parts", 2),
            });

        public static readonly CraftRecipe AutoModule = new(
            "AutoModule", "Auto (Delivery)",
            "Rifle-form mechanism — sustained automatic fire.",
            CraftCategory.Weapons, "Auto", 1,
            new[]
            {
                new CraftIngredient("Metal_Parts", 4),
                new CraftIngredient("Mechanical_Parts", 3),
                new CraftIngredient("Springs", 1),
            });

        public static readonly CraftRecipe ScatterModule = new(
            "ScatterModule", "Scatter (Delivery)",
            "Shotgun-form mechanism — wide cone of pellets per shot.",
            CraftCategory.Weapons, "Scatter", 1,
            new[]
            {
                new CraftIngredient("Metal_Parts", 5),
                new CraftIngredient("Mechanical_Parts", 2),
            });

        // ── Ammo ─────────────────────────────────────────────────
        // Cluster A (2026-05-01): Ammo_Pistol / Ammo_Pistol_AP recipes retired —
        // no current payload uses Pistol-caliber ammo (BallisticPayload reads Ammo_Rifle).
        // May return if Tier 3 splits Ballistic into per-caliber payloads.

        public static readonly CraftRecipe RifleAmmo = new(
            "RifleAmmo", "Rifle Ammo", "Standard rifle cartridges.",
            CraftCategory.Ammo, "Ammo_Rifle", 5,
            new[]
            {
                new CraftIngredient("Gunpowder", 2),
                new CraftIngredient("Metal_Parts", 2),
            });

        public static readonly CraftRecipe RifleAPAmmo = new(
            "RifleAPAmmo", "Rifle AP Ammo", "Armor-piercing rifle cartridges with hardened core.",
            CraftCategory.Ammo, "Ammo_Rifle_AP", 5,
            new[]
            {
                new CraftIngredient("Gunpowder", 2),
                new CraftIngredient("Metal_Parts", 2),
                new CraftIngredient("Military_Components", 1),
            });

        // ── Weapon Mods: Scopes ──────────────────────────────────

        public static readonly CraftRecipe BasicScope = new(
            "BasicScope", "Basic Scope", "Simple optical scope for improved accuracy.",
            CraftCategory.WeaponMods, "Basic_Scope", 1,
            new[]
            {
                new CraftIngredient("Glass", 2),
                new CraftIngredient("Metal_Parts", 1),
            });

        public static readonly CraftRecipe AdvancedScope = new(
            "AdvancedScope", "Advanced Scope", "Electronic scope with enhanced optics.",
            CraftCategory.WeaponMods, "Advanced_Scope", 1,
            new[]
            {
                new CraftIngredient("Glass", 2),
                new CraftIngredient("Electronics", 2),
                new CraftIngredient("Metal_Parts", 1),
            });

        // ── Weapon Mods: Barrels ─────────────────────────────────

        public static readonly CraftRecipe LongBarrel = new(
            "LongBarrel", "Long Barrel", "Extended barrel for improved accuracy at range.",
            CraftCategory.WeaponMods, "Long_Barrel", 1,
            new[]
            {
                new CraftIngredient("Metal_Parts", 2),
                new CraftIngredient("Mechanical_Parts", 1),
            });

        public static readonly CraftRecipe ShortBarrel = new(
            "ShortBarrel", "Short Barrel", "Compact barrel for better handling.",
            CraftCategory.WeaponMods, "Short_Barrel", 1,
            new[]
            {
                new CraftIngredient("Metal_Parts", 2),
                new CraftIngredient("Adhesive", 1),
            });

        // ── Weapon Mods: Muzzle ──────────────────────────────────

        public static readonly CraftRecipe Suppressor = new(
            "Suppressor", "Suppressor", "Reduces muzzle flash and sound signature.",
            CraftCategory.WeaponMods, "Suppressor", 1,
            new[]
            {
                new CraftIngredient("Metal_Parts", 2),
                new CraftIngredient("Cloth", 1),
                new CraftIngredient("Adhesive", 1),
            });

        public static readonly CraftRecipe Compensator = new(
            "Compensator", "Compensator", "Redirects gas to reduce recoil.",
            CraftCategory.WeaponMods, "Compensator", 1,
            new[]
            {
                new CraftIngredient("Metal_Parts", 2),
                new CraftIngredient("Mechanical_Parts", 1),
            });

        // ── Weapon Mods: Magazines ───────────────────────────────

        public static readonly CraftRecipe ExtendedMag = new(
            "ExtendedMag", "Extended Mag", "Higher capacity magazine for sustained fire.",
            CraftCategory.WeaponMods, "Extended_Mag", 1,
            new[]
            {
                new CraftIngredient("Metal_Parts", 2),
                new CraftIngredient("Springs", 2),
            });

        public static readonly CraftRecipe FastReloadMag = new(
            "FastReloadMag", "Fast Reload Mag", "Ergonomic magazine for faster reloads.",
            CraftCategory.WeaponMods, "Fast_Reload_Mag", 1,
            new[]
            {
                new CraftIngredient("Metal_Parts", 2),
                new CraftIngredient("Mechanical_Parts", 1),
            });

        // ── Weapon Mods: Grip / Stock ────────────────────────────

        public static readonly CraftRecipe RecoilGrip = new(
            "RecoilGrip", "Recoil Grip", "Rubberized grip that absorbs recoil.",
            CraftCategory.WeaponMods, "Recoil_Grip", 1,
            new[]
            {
                new CraftIngredient("Rubber", 2),
                new CraftIngredient("Metal_Parts", 1),
            });

        public static readonly CraftRecipe StabilizedStock = new(
            "StabilizedStock", "Stabilized Stock", "Weighted stock for improved weapon stability.",
            CraftCategory.WeaponMods, "Stabilized_Stock", 1,
            new[]
            {
                new CraftIngredient("Metal_Parts", 2),
                new CraftIngredient("Adhesive", 1),
            });

        // ── Weapon Mods: Special ─────────────────────────────────

        public static readonly CraftRecipe APBarrel = new(
            "APBarrel", "Armor-Piercing Barrel", "Military-grade barrel for penetrating armor.",
            CraftCategory.WeaponMods, "AP_Barrel", 1,
            new[]
            {
                new CraftIngredient("Metal_Parts", 3),
                new CraftIngredient("Military_Components", 1),
            });

        public static readonly CraftRecipe OverclockReceiver = new(
            "OverclockReceiver", "Overclock Receiver", "Overclocked firing mechanism for higher rate of fire.",
            CraftCategory.WeaponMods, "Overclock_Receiver", 1,
            new[]
            {
                new CraftIngredient("Electronics", 2),
                new CraftIngredient("Mechanical_Parts", 2),
            });

        // ── Registry ─────────────────────────────────────────────

        static readonly CraftRecipe[] AllRecipes =
        {
            Bandage, FieldMedkit, AdvancedMedkit,
            // Weapon Builder modules (Cluster A 2026-05-01)
            BallisticRoundModule, LaserChargeModule,
            SingleActionModule, AutoModule, ScatterModule,
            RifleAmmo, RifleAPAmmo,
            BasicScope, AdvancedScope,
            LongBarrel, ShortBarrel,
            Suppressor, Compensator,
            ExtendedMag, FastReloadMag,
            RecoilGrip, StabilizedStock,
            APBarrel, OverclockReceiver,
        };

        static Dictionary<string, CraftRecipe> _registry;
        static Dictionary<CraftCategory, List<CraftRecipe>> _byCategory;

        static void EnsureRegistry()
        {
            if (_registry != null) return;
            _registry = new Dictionary<string, CraftRecipe>(AllRecipes.Length);
            _byCategory = new Dictionary<CraftCategory, List<CraftRecipe>>();

            foreach (var r in AllRecipes)
            {
                _registry[r.RecipeId] = r;
                if (!_byCategory.TryGetValue(r.Category, out var list))
                {
                    list = new List<CraftRecipe>();
                    _byCategory[r.Category] = list;
                }
                list.Add(r);
            }
        }

        public static IReadOnlyList<CraftRecipe> GetAll()
        {
            EnsureRegistry();
            return AllRecipes;
        }

        public static IReadOnlyList<CraftRecipe> GetByCategory(CraftCategory category)
        {
            EnsureRegistry();
            return _byCategory.TryGetValue(category, out var list) ? list : new List<CraftRecipe>();
        }

        public static bool TryGet(string recipeId, out CraftRecipe recipe)
        {
            EnsureRegistry();
            return _registry.TryGetValue(recipeId, out recipe);
        }
    }
}
