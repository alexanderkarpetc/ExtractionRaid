using System.Collections.Generic;
using Adapters;
using State;
using Systems;

namespace View.UI.Tooltip.Builders
{
    /// <summary>
    /// Builds a <see cref="TooltipModel"/> for a built weapon — composition (Payload ·
    /// FormFactor) plus combat / cadence stat groups. Reuses <see cref="WeaponStatComposer"/>
    /// so the numbers exactly match what <see cref="WeaponBuilderPresenter"/> shows.
    ///
    /// Pure C# — no Unity refs.
    /// </summary>
    public static class WeaponTooltipBuilder
    {
        public static TooltipModel For(ItemState item, ICoreDefinitionRegistry registry)
        {
            if (item == null || !item.HasWeaponConfiguration)
                return new TooltipModel(string.Empty);

            var title  = WeaponDisplayName.For(item, registry);
            var config = item.WeaponConfiguration;

            if (registry == null)
                return new TooltipModel(title);

            registry.TryGetPayload(config.Payload.DefinitionId,   out var payloadDef);
            registry.TryGetDelivery(config.Delivery.DefinitionId, out var deliveryDef);

            if (payloadDef == null || deliveryDef == null)
                return new TooltipModel(title);

            var subtitle = $"{payloadDef.DisplayName} · {deliveryDef.FormFactor}";

            var stats = WeaponStatComposer.Compose(
                payloadDef,  config.Payload.Rarity,
                deliveryDef, config.Delivery.Rarity);

            var combat = new List<TooltipRow>
            {
                new("Damage",      stats.Damage.ToString("0.##")),
                new("Headshot",    $"{stats.HeadshotDamageMultiplier:0.##}×"),
                new("Penetration", stats.BasePenetration.ToString("0.##")),
            };

            var cadence = new List<TooltipRow>();
            if (WeaponChargeResolver.RequiresChargeUp(payloadDef))
            {
                float chargeTime = WeaponChargeResolver.GetChargeTime(payloadDef, config.Payload.Rarity);
                cadence.Add(new TooltipRow("Charge", $"{chargeTime:0.##} s"));
            }
            cadence.Add(new TooltipRow("Fire Interval", $"{stats.FireInterval:0.##} s"));
            cadence.Add(new TooltipRow("Magazine",      $"{config.AmmoInMagazine}/{stats.MagazineSize}"));
            cadence.Add(new TooltipRow("Reload",        $"{stats.ReloadTime:0.##} s"));

            return new TooltipModel(title, subtitle, new[]
            {
                new TooltipSection("Combat",  combat),
                new TooltipSection("Cadence", cadence),
            });
        }
    }
}
