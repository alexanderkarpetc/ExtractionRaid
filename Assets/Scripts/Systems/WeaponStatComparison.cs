using System.Collections.Generic;
using System.Text;

namespace Systems
{
    /// <summary>
    /// Pairs two weapons' <see cref="WeaponStatDisplay"/> rows into comparison rows for the
    /// side-by-side weapon-compare tooltip: the hovered weapon's value/bar plus the baseline's
    /// bar and a signed numeric delta. Every WeaponStatDisplay row is framed higher = better,
    /// so a positive delta is always an improvement.
    ///
    /// Pure C# — no engine refs, unit-tested.
    /// </summary>
    public static class WeaponStatComparison
    {
        public readonly struct Row
        {
            public readonly string Label;
            public readonly string Value;       // hovered weapon's display value
            public readonly float HoveredBar;   // 0..1, or -1 when value-only
            public readonly float BaselineBar;  // 0..1, or -1 when value-only / no baseline
            public readonly float Delta;        // hovered − baseline (parsed); 0 when equal/unparseable
            public readonly bool HasBar;

            public Row(string label, string value, float hoveredBar, float baselineBar, float delta, bool hasBar)
            {
                Label = label ?? string.Empty;
                Value = value ?? string.Empty;
                HoveredBar = hoveredBar;
                BaselineBar = baselineBar;
                Delta = delta;
                HasBar = hasBar;
            }

            public bool Improved => Delta > 1e-4f;
            public bool Worsened => Delta < -1e-4f;
        }

        /// <summary>
        /// Builds comparison rows from the hovered + baseline row lists (both from
        /// <see cref="WeaponStatDisplay.Build"/>, so same order/labels). Baseline may be null
        /// (no equipped weapon) → deltas are 0 and baseline bars absent.
        /// </summary>
        public static IReadOnlyList<Row> Build(
            IReadOnlyList<WeaponStatDisplay.StatDisplayRow> hovered,
            IReadOnlyList<WeaponStatDisplay.StatDisplayRow> baseline)
        {
            var rows = new List<Row>(hovered?.Count ?? 0);
            if (hovered == null) return rows;

            for (int i = 0; i < hovered.Count; i++)
            {
                var h = hovered[i];
                bool hasBaseline = baseline != null && i < baseline.Count;
                var b = hasBaseline ? baseline[i] : default;

                float delta = 0f;
                if (hasBaseline && TryNum(h.Value, out var hv) && TryNum(b.Value, out var bv))
                    delta = hv - bv;

                rows.Add(new Row(
                    h.Label,
                    h.Value,
                    h.HasBar ? h.BarRatio01 : -1f,
                    (hasBaseline && b.HasBar) ? b.BarRatio01 : -1f,
                    delta,
                    h.HasBar));
            }
            return rows;
        }

        // Parses a leading number out of a display value ("19.5", "2.5/s", "70", "2×", "8").
        // "—" / non-numeric → false.
        static bool TryNum(string s, out float v)
        {
            v = 0f;
            if (string.IsNullOrEmpty(s)) return false;
            var sb = new StringBuilder();
            foreach (var c in s)
            {
                if (char.IsDigit(c) || c == '.' || (c == '-' && sb.Length == 0)) sb.Append(c);
                else break;
            }
            return sb.Length > 0 &&
                   float.TryParse(sb.ToString(), System.Globalization.NumberStyles.Float,
                                  System.Globalization.CultureInfo.InvariantCulture, out v);
        }
    }
}
