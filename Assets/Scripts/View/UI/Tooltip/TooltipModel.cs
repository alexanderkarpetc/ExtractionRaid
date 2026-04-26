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
        public IReadOnlyList<TooltipSection> Sections { get; }

        public TooltipModel(
            string title,
            string subtitle = null,
            IReadOnlyList<TooltipSection> sections = null)
        {
            Title    = title    ?? string.Empty;
            Subtitle = subtitle ?? string.Empty;
            Sections = sections ?? Array.Empty<TooltipSection>();
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

    /// <summary>A single key/value line inside a tooltip section.</summary>
    public readonly struct TooltipRow
    {
        public string Label { get; }
        public string Value { get; }

        public TooltipRow(string label, string value)
        {
            Label = label ?? string.Empty;
            Value = value ?? string.Empty;
        }
    }
}
