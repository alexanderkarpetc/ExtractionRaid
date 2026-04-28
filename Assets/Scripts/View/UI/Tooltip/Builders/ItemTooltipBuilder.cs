using System.Collections.Generic;
using Adapters;
using State;

namespace View.UI.Tooltip.Builders
{
    /// <summary>
    /// Builds a <see cref="TooltipModel"/> for any inventory item. Delegates to
    /// <see cref="WeaponTooltipBuilder"/> for built weapon items, and to
    /// <see cref="ModuleTooltipBuilder"/> for Weapon Builder module items
    /// (Payload / Delivery cores stored у backpack as standalone items — Tier 6 G1).
    /// Falls through to the generic item view (stack count, armor, ammo) otherwise.
    ///
    /// Pure C# — no Unity refs — fully unit-testable.
    /// </summary>
    public static class ItemTooltipBuilder
    {
        public static TooltipModel For(ItemState item, ICoreDefinitionRegistry registry = null)
        {
            if (item == null) return new TooltipModel(string.Empty);
            if (item.HasWeaponConfiguration) return WeaponTooltipBuilder.For(item, registry);

            // Tier 6 G1: module items у backpack share identity з palette cards у
            // Builder — tooltip має бути однаковий, не generic "module name only".
            if (registry != null)
            {
                if (registry.TryGetPayload(item.DefinitionId, out var payloadDef))
                    return ModuleTooltipBuilder.ForPayload(payloadDef);
                if (registry.TryGetDelivery(item.DefinitionId, out var deliveryDef))
                    return ModuleTooltipBuilder.ForDelivery(deliveryDef);
            }

            var def   = item.Definition;
            var title = def?.DisplayName ?? item.DefinitionId;

            var sections = new List<TooltipSection>();

            if (item.StackCount > 1)
            {
                sections.Add(new TooltipSection(null, new[]
                {
                    new TooltipRow("Quantity", $"x{item.StackCount}"),
                }));
            }

            if (def != null && def.ArmorPoints > 0f)
            {
                float max = item.HasCustomDurability ? item.MaxDurability     : def.MaxDurability;
                float cur = item.HasCustomDurability ? item.CurrentDurability : max;
                sections.Add(new TooltipSection("Armor", new[]
                {
                    new TooltipRow("Armor Points", def.ArmorPoints.ToString("0")),
                    new TooltipRow("Durability",   $"{cur:0}/{max:0}"),
                }));
            }

            if (def != null && !string.IsNullOrEmpty(def.AmmoType))
            {
                var rows = new List<TooltipRow>();
                if (def.Penetration > 0f) rows.Add(new TooltipRow("Penetration",   def.Penetration.ToString("0.##")));
                if (def.ArmorDamage > 0f) rows.Add(new TooltipRow("Armor Damage",  def.ArmorDamage.ToString("0.##")));
                if (def.BleedChance > 0f) rows.Add(new TooltipRow("Bleed Chance",  $"{def.BleedChance * 100f:0}%"));
                if (rows.Count > 0)
                    sections.Add(new TooltipSection("Ammo", rows));
            }

            return new TooltipModel(title, subtitle: null, sections);
        }
    }
}
