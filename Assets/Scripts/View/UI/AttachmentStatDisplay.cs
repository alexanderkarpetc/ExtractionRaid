using System.Globalization;
using State;

namespace View.UI
{
    /// <summary>
    /// Pure presentation helpers for attachment stat deltas — the player-facing axis label,
    /// the "is this change good?" rule (drives green/red), and the signed-percent format.
    /// Single-sourced so the attachment editor tags and the item tooltip agree.
    /// </summary>
    public static class AttachmentStatDisplay
    {
        // Hex colors match the editor's green/red tags (rgb 80,200,120 / 220,100,100).
        public const string GoodHex = "#50C878";
        public const string BadHex  = "#DC6464";

        public static string AxisLabel(WeaponStatAxis axis) => axis switch
        {
            WeaponStatAxis.Damage       => "Damage",
            WeaponStatAxis.RateOfFire   => "Rate of Fire",
            WeaponStatAxis.MagazineSize => "Magazine",
            WeaponStatAxis.ReloadTime   => "Reload",
            WeaponStatAxis.Recoil       => "Recoil",
            WeaponStatAxis.Spread       => "Spread",
            WeaponStatAxis.Ergonomics   => "Ergonomics",
            WeaponStatAxis.SightRange   => "Sight Range",
            WeaponStatAxis.ProjectileSpeed => "Projectile Speed",
            WeaponStatAxis.Headshot     => "Headshot",
            _                           => axis.ToString(),
        };

        /// <summary>
        /// Whether a positive/negative change on this axis is an improvement. Higher-is-better
        /// for Damage/RateOfFire/MagazineSize/Ergonomics; lower-is-better for the rest
        /// (Recoil/Spread/ReloadTime).
        /// </summary>
        public static bool DeltaIsGood(WeaponStatAxis axis, float percent)
        {
            bool higherBetter = axis == WeaponStatAxis.Damage
                             || axis == WeaponStatAxis.RateOfFire
                             || axis == WeaponStatAxis.MagazineSize
                             || axis == WeaponStatAxis.Ergonomics
                             || axis == WeaponStatAxis.SightRange
                             || axis == WeaponStatAxis.ProjectileSpeed
                             || axis == WeaponStatAxis.Headshot;
            return higherBetter ? percent > 0f : percent < 0f;
        }

        public static string Hex(bool good) => good ? GoodHex : BadHex;

        /// <summary>Signed whole-percent string, e.g. "+50%" / "-10%".</summary>
        public static string FormatPercent(float percent)
        {
            string sign = percent > 0f ? "+" : "";
            return sign + percent.ToString("0.#", CultureInfo.InvariantCulture) + "%";
        }

        /// <summary>
        /// Signed delta with the axis's natural unit — SightRange is raw meters ("+16.5m"),
        /// every other axis is a percent ("+50%"). Single-sourced so tooltip + editor agree.
        /// </summary>
        public static string FormatDelta(WeaponStatAxis axis, float value)
        {
            string sign = value > 0f ? "+" : "";
            string unit = axis == WeaponStatAxis.SightRange ? "m" : "%";
            return sign + value.ToString("0.#", CultureInfo.InvariantCulture) + unit;
        }
    }
}
