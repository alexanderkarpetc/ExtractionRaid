using System;
using System.Collections.Generic;

namespace View.UI.Tooltip
{
    /// <summary>
    /// Structured tooltip content. View-layer-only data (no Unity refs) so builders
    /// can be written as pure C# functions and unit-tested without the engine.
    ///
    /// Rendered by <see cref="TooltipController"/> into the UXML overlay panel.
    ///
    /// Naming note: kept distinct from "Payload" because that term is reserved for
    /// weapon-builder payload cores in this project.
    /// </summary>
    public sealed class TooltipModel
    {
        public string Title { get; }
        public string Subtitle { get; }
        /// <summary>
        /// Optional 1-2 line flavor / description block, rendered between the
        /// subtitle and the first section. Hidden when empty.
        /// </summary>
        public string Description { get; }
        public IReadOnlyList<TooltipSection> Sections { get; }

        public TooltipModel(
            string title,
            string subtitle = null,
            IReadOnlyList<TooltipSection> sections = null,
            string description = null)
        {
            Title       = title       ?? string.Empty;
            Subtitle    = subtitle    ?? string.Empty;
            Description = description ?? string.Empty;
            Sections    = sections    ?? Array.Empty<TooltipSection>();
        }

        public bool IsEmpty => string.IsNullOrEmpty(Title) && Sections.Count == 0;
    }

    /// <summary>One titled group of key/value rows inside a tooltip.</summary>
    public sealed class TooltipSection
    {
        public string Heading { get; }
        public IReadOnlyList<TooltipRow> Rows { get; }

        public TooltipSection(string heading, IReadOnlyList<TooltipRow> rows)
        {
            Heading = heading ?? string.Empty;
            Rows    = rows    ?? Array.Empty<TooltipRow>();
        }
    }

    /// <summary>
    /// A single line inside a tooltip section. Either a key/value text row
    /// (<see cref="Value"/> set, <see cref="HasBar"/> false) or a key/bar row
    /// (<see cref="BarRatio01"/> in 0..1, <see cref="Value"/> empty) — used for the
    /// "feel" stats (Recoil / Accuracy / Ergonomics) where a 0..1 goodness fill
    /// reads better than a raw number. See WeaponStatDisplay.
    /// </summary>
    public readonly struct TooltipRow
    {
        public string Label { get; }
        public string Value { get; }
        /// <summary>0..1 fill (fuller = better) for bar rows; -1 = text row, no bar.</summary>
        public float BarRatio01 { get; }

        public bool HasBar => BarRatio01 >= 0f;

        public TooltipRow(string label, string value)
        {
            Label = label ?? string.Empty;
            Value = value ?? string.Empty;
            BarRatio01 = -1f;
        }

        public TooltipRow(string label, float barRatio01)
        {
            Label = label ?? string.Empty;
            Value = string.Empty;
            BarRatio01 = barRatio01 < 0f ? 0f : barRatio01 > 1f ? 1f : barRatio01;
        }

        /// <summary>Row with both a value (header, right) and a progress bar (below).</summary>
        public TooltipRow(string label, string value, float barRatio01)
        {
            Label = label ?? string.Empty;
            Value = value ?? string.Empty;
            BarRatio01 = barRatio01 < 0f ? 0f : barRatio01 > 1f ? 1f : barRatio01;
        }
    }
}
