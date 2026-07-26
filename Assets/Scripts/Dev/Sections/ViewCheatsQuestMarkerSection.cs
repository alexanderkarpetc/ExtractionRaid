using UnityEngine;

namespace Dev
{
    /// <summary>
    /// Quest-giver marker VFX (<see cref="View.NpcQuestIndicator"/>): the additive ground
    /// light pool + vertical beam column shown above NPCs with an offer. Sizes and
    /// intensity are runtime-tunable so the beacon can be dialed in per art pass.
    /// The SDF "!" badge itself is not driven from here.
    /// Read live by the view; changing a value updates in play mode next frame.
    /// </summary>
    public class ViewCheatsQuestMarkerSection : ScriptableObject
    {
        [Header("Ground light pool")]
        [Tooltip("Radius of the light pool on the floor, in meters.")]
        [Range(0.3f, 12f)] public float GroundRadius = 4.8f;
        [Range(0f, 1f)] public float GroundAlphaMin = 0.50f;
        [Range(0f, 1f)] public float GroundAlphaMax = 0.90f;
        [Tooltip("Meters over which the pool fades out near geometry (soft-particle depth fade).")]
        [Range(0.05f, 4f)] public float GroundSoftFade = 0.5f;
        [Tooltip("Height above the floor to beat z-fighting.")]
        [Range(0f, 0.5f)] public float GroundY = 0.06f;

        [Header("Vertical beam column")]
        [Tooltip("Beam height in meters.")]
        [Range(1f, 40f)] public float BeamHeight = 13.5f;
        [Tooltip("Beam half-width in meters (→ full width ×2).")]
        [Range(0.05f, 4f)] public float BeamHalfWidth = 1.2f;
        [Tooltip("Beam base offset above the floor.")]
        [Range(0f, 0.5f)] public float BeamBaseY = 0.10f;
        [Range(0f, 1f)] public float BeamAlphaMin = 0.50f;
        [Range(0f, 1f)] public float BeamAlphaMax = 0.95f;

        [Header("Shared")]
        [Tooltip("Breathing pulse frequency (Hz) — drives both the badge glow and the light intensity.")]
        [Range(0.1f, 3f)] public float PulseHz = 0.9f;

        // Off-screen direction arrow to the nearest deploy point (exit-to-raid), drawn by
        // DeployArrowPresenter. Grouped here so all on-screen marker guidance is tuned in
        // one Dev Cheats section.
        [Header("Exit direction arrow (screen-edge)")]
        [Tooltip("Show the screen-edge arrow to the nearest deploy point when it's off-screen.")]
        public bool ArrowEnabled = true;
        [Range(24f, 160f)] public float ArrowSizePx = 64f;
        [Tooltip("Distance from the screen edge (px) the arrow sits at.")]
        [Range(20f, 200f)] public float ArrowEdgeInsetPx = 64f;
        [Tooltip("Pulse depth — arrow scales ±this fraction while shown. 0 = no pulse.")]
        [Range(0f, 0.5f)] public float ArrowPulseAmount = 0.12f;
        [Tooltip("Arrow pulse frequency (Hz).")]
        [Range(0.1f, 4f)] public float ArrowPulseHz = 1.6f;
        [Tooltip("Arrow tint (defaults to the deploy 'go' green).")]
        public Color ArrowColor = new(0.35f, 0.92f, 0.55f, 1f);
    }
}
