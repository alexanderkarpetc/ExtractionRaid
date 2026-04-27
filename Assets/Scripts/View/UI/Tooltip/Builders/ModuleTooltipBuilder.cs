using System.Collections.Generic;
using State;
using Systems;

namespace View.UI.Tooltip.Builders
{
    /// <summary>
    /// Tooltips for individual Weapon Builder modules (Payload / Delivery cores).
    /// Used by Builder UI when the player hovers over a module card or dropdown
    /// option — shows what stats this module brings and its identity (ammo / pattern).
    ///
    /// Pure C# — no Unity refs.
    /// </summary>
    public static class ModuleTooltipBuilder
    {
        public static TooltipModel ForPayload(
            PayloadCoreDefinition def,
            RarityTier rarity = RarityTier.Common)
        {
            if (def == null) return new TooltipModel(string.Empty);

            var stats = def.StatsByTier(rarity);
            var rows = new List<TooltipRow>();
            if (stats.Damage > 0f)          rows.Add(new TooltipRow("Damage",          stats.Damage.ToString("0.##")));
            if (stats.BasePenetration > 0f) rows.Add(new TooltipRow("Penetration",     stats.BasePenetration.ToString("0.##")));
            if (stats.ProjectileSpeed > 0f) rows.Add(new TooltipRow("Projectile Speed", stats.ProjectileSpeed.ToString("0.##")));

            if (WeaponChargeResolver.RequiresChargeUp(def))
            {
                float chargeTime = WeaponChargeResolver.GetChargeTime(def, rarity);
                rows.Add(new TooltipRow("Charge", $"{chargeTime:0.##} s"));
            }

            var sections = rows.Count > 0
                ? new[] { new TooltipSection("Stats", rows) }
                : System.Array.Empty<TooltipSection>();

            string subtitle = string.IsNullOrEmpty(def.AmmoType)
                ? "Payload"
                : $"Payload · {def.AmmoType}";

            return new TooltipModel(
                def.DisplayName,
                subtitle,
                sections,
                description: WeaponModuleFlavor.ForPayload(def.Id));
        }

        public static TooltipModel ForDelivery(
            DeliveryCoreDefinition def,
            RarityTier rarity = RarityTier.Common)
        {
            if (def == null) return new TooltipModel(string.Empty);

            var stats = def.StatsByTier(rarity);
            var rows = new List<TooltipRow>
            {
                new("Fire Interval",     $"{stats.FireInterval:0.##} s"),
                new("Magazine",          stats.MagazineSize.ToString()),
                new("Reload",            $"{stats.ReloadTime:0.##} s"),
                new("Projectiles/Shot",  stats.ProjectilesPerShot.ToString()),
            };

            return new TooltipModel(
                def.FormFactor,
                $"Delivery · {def.Pattern}",
                new[] { new TooltipSection("Stats", rows) },
                description: WeaponModuleFlavor.ForDelivery(def.Id));
        }
    }
}
