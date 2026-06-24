using System.Collections.Generic;
using State;
using View.UI;

namespace View.UI.Tooltip.Builders
{
    /// <summary>
    /// Builds a <see cref="TooltipModel"/> for a weapon attachment item (mod) — the title, a
    /// "{Slot} Attachment" subtitle, and an "Effects" section listing each stat delta with a
    /// green (improvement) / red (downside) colored value. Reuses <see cref="AttachmentStatDisplay"/>
    /// so the good/bad rule + labels match the in-editor mod tags.
    ///
    /// Pure C# — color via rich-text hex in the value string (same approach as the weapon
    /// tooltip's rarity subtitle), no Unity object refs.
    /// </summary>
    public static class AttachmentTooltipBuilder
    {
        public static TooltipModel For(AttachmentDefinition def, ItemState item = null)
        {
            if (def == null) return new TooltipModel(string.Empty);

            var title    = string.IsNullOrEmpty(def.DisplayName) ? def.Id : def.DisplayName;
            var subtitle = $"{def.Slot} Attachment";

            var sections = new List<TooltipSection>();

            var mods = def.Modifiers;
            if (mods != null && mods.Count > 0)
            {
                var rows = new List<TooltipRow>();
                for (int i = 0; i < mods.Count; i++)
                {
                    var d = mods[i];
                    bool good = AttachmentStatDisplay.DeltaIsGood(d.Axis, d.Percent);
                    string value =
                        $"<color={AttachmentStatDisplay.Hex(good)}>{AttachmentStatDisplay.FormatPercent(d.Percent)}</color>";
                    rows.Add(new TooltipRow(AttachmentStatDisplay.AxisLabel(d.Axis), value));
                }
                sections.Add(new TooltipSection("Effects", rows));
            }

            if (item != null && item.StackCount > 1)
                sections.Add(new TooltipSection(null, new[]
                {
                    new TooltipRow("Quantity", $"x{item.StackCount}"),
                }));

            return new TooltipModel(title, subtitle, sections);
        }
    }
}
