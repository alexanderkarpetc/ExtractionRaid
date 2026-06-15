using System.Collections.Generic;
using Adapters;
using State;
using Systems;
using View.UI;

namespace View.UI.Tooltip.Builders
{
    /// <summary>
    /// Builds a <see cref="TooltipModel"/> for a built weapon: the two cores with
    /// per-core rarity (color = rarity) plus the player-facing stat readout from
    /// <see cref="WeaponStatDisplay"/> (number rows + bar rows). Reuses
    /// <see cref="WeaponStatComposer"/> so numbers match the Weapon Builder.
    ///
    /// Display rules per docs/ai/weapon-builder/attachments/stats.md: no Penetration
    /// (ammo channel), no Reload base value (delta-only later), Rate of Fire instead
    /// of raw Fire Interval. Charge row kept for Laser payloads.
    ///
    /// Pure C# — rarity color via <see cref="RarityVisuals"/> hex (string), no Unity object refs.
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

            // Two cores, each tinted by its own rarity (the "weapon = 2 cores" signal).
            var pRarity = config.Payload.Rarity;
            var dRarity = config.Delivery.Rarity;
            var subtitle =
                $"<color={RarityVisuals.Hex(pRarity)}>{payloadDef.DisplayName} ({pRarity})</color>"
                + " · "
                + $"<color={RarityVisuals.Hex(dRarity)}>{deliveryDef.FormFactor} ({dRarity})</color>";

            var stats = WeaponStatComposer.Compose(payloadDef, pRarity, deliveryDef, dRarity);
            stats = WeaponStatComposer.ApplyAttachments(stats, config, registry);

            var rows = new List<TooltipRow>();

            // Charge (Laser only) — payload-specific cadence, shown first.
            if (WeaponChargeResolver.RequiresChargeUp(payloadDef))
            {
                float chargeTime = WeaponChargeResolver.GetChargeTime(payloadDef, pRarity);
                rows.Add(new TooltipRow("Charge", $"{chargeTime:0.##} s"));
            }

            foreach (var r in WeaponStatDisplay.Build(stats))
            {
                if (r.Label == "Magazine")
                    rows.Add(new TooltipRow("Magazine", $"{config.AmmoInMagazine}/{stats.MagazineSize}"));
                else if (r.HasBar)
                    rows.Add(new TooltipRow(r.Label, r.Value, r.BarRatio01));
                else
                    rows.Add(new TooltipRow(r.Label, r.Value));
            }

            return new TooltipModel(title, subtitle, new[]
            {
                new TooltipSection("Stats", rows),
            });
        }
    }
}
