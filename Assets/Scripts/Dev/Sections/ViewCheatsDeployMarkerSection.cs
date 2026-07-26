using UnityEngine;

namespace Dev
{
    /// <summary>
    /// Deploy-point BEACON VFX in the HIDEOUT (<see cref="View.DeployBeaconPresenter"/>): the
    /// always-on ground pool + beam on the exit-to-raid deploy point. Helps a new player find
    /// where to leave the bunker. Runtime-tunable; read live by the view.
    /// (The off-screen direction arrow's knobs live in the Quest Marker section.)
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
    }
}
