using UnityEngine;

namespace Dev
{
    /// <summary>Which screen corner a HUD element anchors to. Offset reads as "padding inward".</summary>
    public enum HudCorner { TopLeft, TopRight, BottomLeft, BottomRight }

    /// <summary>Row anchor relative to the HP bar above the character.</summary>
    public enum WorldStatusAlignment { Left, Center, Right }

    /// <summary>
    /// Battle HUD tunables — status effects (HUD row + worldspace),
    /// radial stamina ring (worldspace), hotbar weapon slots. Multi-stage shipping pass.
    /// Stage 2 (armor paper-doll) was attempted then reverted 2026-05-21 — existing armor
    /// stripe on WorldHealthBar is sufficient. See <c>docs/ai/gunplay/battle-hud.md</c>.
    /// </summary>
    public class ViewCheatsBattleHudSection : ScriptableObject
    {
        [Tooltip("Master toggle. OFF = canvas hidden, overlay skips work.")]
        public bool Enabled = true;

        [Header("Stage 3 — Status effects row (UI Toolkit)")]
        [Tooltip("Which screen corner the status row anchors to. Tooltip auto-flips/pushes to stay on screen.")]
        public HudCorner StatusRowCorner = HudCorner.TopRight;
        [Tooltip("Padding inward from chosen corner (px in reference resolution 1920×1080).")]
        public Vector2 StatusRowOffset = new Vector2(40f, 40f);

        [Header("Stage 4 — Worldspace status mini-icons (universal — player + bots)")]
        [Tooltip("Icon size (world units). ~0.3 reads cleanly при HP bar height 0.12.")]
        [Range(0.05f, 0.6f)] public float WorldStatusIconSize = 0.3f;
        [Tooltip("Gap between icons (world units).")]
        [Range(0f, 0.2f)] public float WorldStatusIconGap = 0.04f;
        [Tooltip("Y offset below HP bar (world units, negative = down).")]
        [Range(-1f, 0f)] public float WorldStatusYOffset = -0.2f;
        [Tooltip("Row alignment relative to HP bar. Left = WoW-style debuff row.")]
        public WorldStatusAlignment WorldStatusAlignment = WorldStatusAlignment.Left;

        // Stage 5+ tunables added as each stage ships:
        //   Stage 5: radial stamina ring (colors, radius, thickness, hide threshold, fade time)
        //   Stage 6: hotbar weapon slot styling (separator gap, tints)
    }
}
