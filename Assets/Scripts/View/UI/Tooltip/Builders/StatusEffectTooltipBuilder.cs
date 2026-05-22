using Constants;
using State;

namespace View.UI.Tooltip.Builders
{
    /// <summary>
    /// Tooltip content for an active status effect on the player. Shown when the player
    /// hovers a status tile у Battle HUD status row. Joins existing builders namespace
    /// (Weapon / Item / Module) so the rest of the project pattern stays uniform.
    ///
    /// Pure C# — no Unity refs.
    /// </summary>
    public static class StatusEffectTooltipBuilder
    {
        public static TooltipModel For(StatusEffectInstance e)
        {
            if (e == null) return new TooltipModel(string.Empty);

            switch (e.Type)
            {
                case StatusEffectType.Bleeding:
                    return BuildBleed(e);
                default:
                    return new TooltipModel(e.Type.ToString());
            }
        }

        static TooltipModel BuildBleed(StatusEffectInstance e)
        {
            bool heavy = e.Level >= 2;
            string title = heavy ? "Bleeding — Heavy" : "Bleeding — Light";
            string subtitle = "Status effect";

            float dps = heavy
                ? StatusEffectConstants.BleedL2DamagePerTick / StatusEffectConstants.BleedTickInterval
                : StatusEffectConstants.BleedL1DamagePerTick / StatusEffectConstants.BleedTickInterval;

            var rows = new[]
            {
                new TooltipRow("Damage", $"{dps:0.#} HP/sec"),
                new TooltipRow("Stop with", $"Bandage ({StatusEffectConstants.BandageUseTime:0.#}s)"),
            };
            var sections = new[] { new TooltipSection("Effect", rows) };

            string description = heavy
                ? "Severe wound bleeding fast. Apply a bandage now."
                : "Open wound. Apply a bandage when safe.";

            return new TooltipModel(title, subtitle, sections, description: description);
        }
    }
}
