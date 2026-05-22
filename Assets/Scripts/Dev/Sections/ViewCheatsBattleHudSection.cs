using UnityEngine;

namespace Dev
{
    /// <summary>Which screen corner a HUD element anchors to. Offset reads as "padding inward".</summary>
    public enum HudCorner { TopLeft, TopRight, BottomLeft, BottomRight }

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

        // Stage 4+ tunables added as each stage ships:
        //   Stage 4: worldspace status mini-icons (size, y-offset, gap)
        //   Stage 5: radial stamina ring (colors, radius, thickness, hide threshold, fade time)
        //   Stage 6: hotbar weapon slot styling (separator gap, tints)
    }
}
