using UnityEngine;

namespace Dev
{
    /// <summary>
    /// Deploy-point wayfinding VFX in the HIDEOUT (<see cref="View.DeployBeaconPresenter"/> +
    /// <see cref="View.DeployArrowPresenter"/>): the always-on beacon on the exit-to-raid
    /// deploy point (ground pool + beam) + a screen-edge arrow to the nearest deploy point.
    /// Helps a new player find where to leave the bunker and start a raid. Runtime-tunable;
    /// read live by the views.
    /// </summary>
    public class ViewCheatsDeployMarkerSection : ScriptableObject
    {
        [Header("Beacon color")]
        [Tooltip("Beacon tint. Green reads as 'go / head out on a raid'.")]
        public Color Color = new(0.35f, 0.92f, 0.55f, 1f);

        [Header("Ground light pool")]
        [Range(0.3f, 12f)] public float GroundRadius = 4.8f;
        [Range(0f, 1f)] public float GroundAlphaMin = 0.45f;
        [Range(0f, 1f)] public float GroundAlphaMax = 0.80f;
        [Range(0.05f, 4f)] public float GroundSoftFade = 0.5f;
        [Range(0f, 0.5f)] public float GroundY = 0.06f;

        [Header("Vertical beam column")]
        [Range(1f, 40f)] public float BeamHeight = 13.5f;
        [Range(0.05f, 4f)] public float BeamHalfWidth = 1.2f;
        [Range(0f, 0.5f)] public float BeamBaseY = 0.10f;
        [Range(0f, 1f)] public float BeamAlphaMin = 0.45f;
        [Range(0f, 1f)] public float BeamAlphaMax = 0.85f;

        [Header("Shared")]
        [Range(0.1f, 3f)] public float PulseHz = 0.7f;

        [Header("Screen-edge direction arrow")]
        [Tooltip("Show the screen-edge arrow to the nearest deploy point when it's off-screen.")]
        public bool ArrowEnabled = true;
        [Range(24f, 160f)] public float ArrowSizePx = 64f;
        [Tooltip("Distance from the screen edge (px) the arrow sits at.")]
        [Range(20f, 200f)] public float ArrowEdgeInsetPx = 64f;
    }
}
