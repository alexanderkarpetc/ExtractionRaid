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
    /// stripe on WorldHealthBar is sufficient.
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

        [Header("Stage 5 — Worldspace radial stamina ring (player only)")]
        [Tooltip("Master toggle for the stamina ring.")]
        public bool StaminaRingEnabled = true;
        [Tooltip("World-space offset of the ring relative to the player center. " +
                 "Default = to the left. Top-down: world-left ≈ screen-left. " +
                 "Use (0,0,-x) to sit under the feet instead.")]
        public Vector3 StaminaRingOffset = new Vector3(-0.65f, 0.05f, 0f);
        [Tooltip("Ring diameter in world units.")]
        [Range(0.2f, 1.5f)] public float StaminaRingWorldSize = 0.55f;
        [Tooltip("Ring thickness (shader UV units, fraction of the quad).")]
        [Range(0.02f, 0.3f)] public float StaminaRingThickness = 0.11f;

        [Tooltip("Spring follow time (SmoothDamp). Higher = laggier 'rubber-band' catch-up. 0 = rigid.")]
        [Range(0f, 0.5f)] public float StaminaRingSpringTime = 0.13f;

        [Header("Stage 5 — colors")]
        public Color StaminaRingTrackColor = new Color(0.18f, 0.18f, 0.20f, 0.70f);
        public Color StaminaRingColorHigh  = new Color(0.25f, 1.00f, 0.40f, 1f);
        public Color StaminaRingColorMid   = new Color(1.00f, 0.62f, 0.08f, 1f);
        public Color StaminaRingColorLow   = new Color(1.00f, 0.18f, 0.14f, 1f);
        [Tooltip("HDR intensity boost on the fill arc — >1 makes colors pop through URP tonemap (juicy).")]
        [Range(1f, 3f)] public float StaminaRingFillIntensity = 1.5f;

        [Header("Stage 5 — outline")]
        public Color StaminaRingOutlineColor = new Color(0.02f, 0.02f, 0.03f, 0.95f);
        [Tooltip("Outline band width hugging the donut's inner+outer edges (UV units).")]
        [Range(0f, 0.1f)] public float StaminaRingOutlineWidth = 0.028f;

        [Header("Stage 5 — exhaustion blink")]
        [Tooltip("Blink frequency (Hz) while exhausted (locked out of sprint).")]
        [Range(0.5f, 8f)] public float StaminaRingBlinkFrequency = 3f;
        [Tooltip("Dim point of the exhaustion opacity pulse — ring fades to this alpha fraction " +
                 "at the bottom of each pulse (gentle up/down, not a hard flash).")]
        [Range(0f, 1f)] public float StaminaRingBlinkMinAlpha = 0.25f;

        [Header("Stage 5 — visibility")]
        [Tooltip("If ON, ring always shown. If OFF, fades out when stamina full (after delay).")]
        public bool StaminaRingAlwaysVisible = false;
        [Tooltip("Stamina ratio at/above which the ring counts as 'full' and begins to hide.")]
        [Range(0.8f, 1f)] public float StaminaRingHideThreshold = 0.999f;
        [Tooltip("Seconds the ring stays full before it starts fading out.")]
        [Range(0f, 3f)] public float StaminaRingHideDelay = 0.8f;
        [Tooltip("Fade in/out duration (seconds).")]
        [Range(0.05f, 1f)] public float StaminaRingFadeTime = 0.3f;

        [Header("Stage 7 — Ammo counter")]
        [Tooltip("Master toggle for the ammo counter block.")]
        public bool AmmoEnabled = true;
        [Tooltip("Which screen corner the ammo block anchors to.")]
        public HudCorner AmmoCorner = HudCorner.BottomRight;
        [Tooltip("Padding inward from chosen corner (px in 1920×1080 ref).")]
        public Vector2 AmmoOffset = new Vector2(48f, 40f);
        [Tooltip("Magazine fraction at/below which the count turns gold (low-ammo warning). Empty = red.")]
        [Range(0f, 0.5f)] public float AmmoLowThreshold = 0.25f;

        [Header("Stage 6 — Hotbar weapon slots")]
        [Tooltip("Gap (px, 1920×1080 ref) between the weapon strip (1-2) and quick strip (3-9).")]
        [Range(0f, 60f)] public float HotbarWeaponSeparatorPx = 18f;
        [Tooltip("Resting background tint of weapon slots (warm — distinct from consumables).")]
        public Color WeaponSlotBgTint = new Color(0.55f, 0.42f, 0.22f, 0.5f);
        [Tooltip("Background tint of the equipped (selected) weapon slot.")]
        public Color WeaponSlotActiveTint = new Color(0.85f, 0.62f, 0.25f, 0.7f);
        [Tooltip("Resting background tint of consumable quick slots (cool). Applied only in the " +
                 "normal occupied state — empty/active keep their existing USS treatment.")]
        public Color ConsumableSlotBgTint = new Color(0.28f, 0.38f, 0.5f, 0.4f);
    }
}
