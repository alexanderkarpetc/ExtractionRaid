using UnityEngine;

namespace Dev
{
    /// <summary>
    /// Battle HUD tunables — armor paper-doll (TL), status effects (HUD row + worldspace),
    /// radial stamina ring (worldspace), hotbar weapon slots. Multi-stage shipping pass.
    /// See <c>docs/ai/gunplay/battle-hud.md</c> for spec.
    /// </summary>
    public class ViewCheatsBattleHudSection : ScriptableObject
    {
        [Tooltip("Master toggle. OFF = canvas hidden, presenter skips work.")]
        public bool Enabled = true;

        // Stage 2+ tunables added here as each stage ships:
        //   Stage 2: armor paper-doll (position, scale, region colors, thresholds)
        //   Stage 3: HUD status row (icon size, gap)
        //   Stage 4: worldspace status mini-icons (size, y-offset, gap)
        //   Stage 5: radial stamina ring (colors, radius, thickness, hide threshold, fade time)
        //   Stage 6: hotbar weapon slot styling (separator gap, tints)
    }
}
