using State;
using UnityEngine;

namespace View.UI
{
    /// <summary>
    /// Maps <see cref="RarityTier"/> → display color. Single source of truth for
    /// rarity coloring across the Weapon Attachments UI:
    ///   • weapon tooltip — rich-text <c>&lt;color=#hex&gt;</c> per core (see WeaponTooltipBuilder)
    ///   • inventory dual-rarity frame — Unity <c>Color</c> on corner brackets (P1.3)
    ///
    /// Classic looter ramp (gray → green → blue → purple → gold), kept restrained
    /// per project aesthetic. Pure value-type helper — no Unity object refs.
    /// </summary>
    public static class RarityVisuals
    {
        public static Color Color(RarityTier tier) => tier switch
        {
            RarityTier.Common    => new Color(0.60f, 0.63f, 0.65f),
            RarityTier.Uncommon  => new Color(0.37f, 0.75f, 0.38f),
            RarityTier.Rare      => new Color(0.29f, 0.56f, 0.85f),
            RarityTier.Epic      => new Color(0.66f, 0.47f, 0.88f),
            RarityTier.Legendary => new Color(0.88f, 0.66f, 0.23f),
            _                    => new Color(0.60f, 0.63f, 0.65f),
        };

        /// <summary>"#RRGGBB" for UI Toolkit rich-text color tags.</summary>
        public static string Hex(RarityTier tier) => "#" + ColorUtility.ToHtmlStringRGB(Color(tier));
    }
}
