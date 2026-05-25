using State;
using UnityEngine;

namespace View.UI
{
    /// <summary>
    /// Shared visual catalog for status effects — single source of truth bound to
    /// <see cref="StatusEffectInstance"/>. Consumed by:
    ///   - Stage 3 HUD tiles (`BattleHudOverlay`) — emoji + USS class
    ///   - Stage 4 worldspace mini-icons (`WorldStatusIcons`) — bg color tier
    ///
    /// Adding a new status: extend each switch — that's the only place to touch
    /// for both surfaces to stay in lock-step.
    /// </summary>
    public static class StatusEffectVisualMap
    {
        /// <summary>
        /// Composite key — used by both HUD + worldspace diff loops to detect L1→L2 escalation
        /// and avoid recreating tiles on every frame.
        /// </summary>
        public static string KeyFor(StatusEffectInstance e) => $"{e.Type}-L{e.Level}";

        /// <summary>
        /// Emoji character for HUD tile (UI Toolkit Label). Worldspace ignores emoji and uses
        /// <see cref="BgColorFor"/> only — too small to read text at peripheral distance.
        /// </summary>
        public static string EmojiFor(StatusEffectInstance e) => e.Type switch
        {
            StatusEffectType.Bleeding => "🩸",
            _ => "?",
        };

        /// <summary>
        /// USS class suffix for HUD tile background style (matches color tiers defined in
        /// <c>BattleHudOverlay.uss</c>).
        /// </summary>
        public static string UssClassFor(StatusEffectInstance e) => e.Type switch
        {
            StatusEffectType.Bleeding when e.Level >= 2 => "bh-status-tile--bleed-heavy",
            StatusEffectType.Bleeding                   => "bh-status-tile--bleed-light",
            _ => "bh-status-tile--bleed-light",
        };

        /// <summary>
        /// Worldspace square fill color. Mirrors the HUD tile background tints (defined in
        /// <c>BattleHudOverlay.uss</c>) so player learns "this red = bleed" once and reads it
        /// in both places.
        ///
        /// Keep in sync with the USS rules manually until we move to a token system.
        /// </summary>
        public static Color BgColorFor(StatusEffectInstance e) => e.Type switch
        {
            StatusEffectType.Bleeding when e.Level >= 2 => new Color(0.82f, 0.10f, 0.10f, 0.95f), // ≈ rgba(210,25,25,0.95)
            StatusEffectType.Bleeding                   => new Color(0.59f, 0.12f, 0.12f, 0.85f), // ≈ rgba(150,30,30,0.85)
            _ => new Color(0.5f, 0.5f, 0.5f, 0.8f),
        };
    }
}
