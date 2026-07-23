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
    }
}
