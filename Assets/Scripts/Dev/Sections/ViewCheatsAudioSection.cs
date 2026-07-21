using UnityEngine;

namespace Dev
{
    /// <summary>
    /// Runtime volume multipliers for balancing the current gameplay sound library.
    /// </summary>
    public class ViewCheatsAudioSection : ScriptableObject
    {
        [Range(0f, 1f)] public float MasterSfx = 1f;

        [Header("Music")]
        [Range(0f, 1f)] public float Music = 0.35f;

        [Header("Pistol")]
        [Range(0f, 1f)] public float CloseShot = 1f;
        [Range(0f, 1f)] public float DistantShot = 1f;
        [Range(0f, 1f)] public float DryFire = 1f;
        [Range(0f, 1f)] public float Reload = 1f;
        [Range(0f, 1f)] public float Holster = 1f;
        [Range(0f, 1f)] public float Unholster = 1f;

        [Header("Impacts")]
        [Range(0f, 1f)] public float HardSurfaceImpact = 1f;
        [Range(0f, 1f)] public float MetalImpact = 1f;
        [Range(0f, 1f)] public float FleshImpact = 1f;
        [Range(0f, 1f)] public float ArmorImpact = 1f;
        [Range(0f, 1f)] public float Ricochet = 1f;
        [Range(0f, 1f)] public float Headshot = 1f;

        [Header("Characters")]
        [Range(0f, 1f)] public float Bleeding = 1f;
        [Range(0f, 1f)] public float BodyFall = 1f;
        [Range(0f, 1f)] public float WalkFootsteps = 1f;
        [Range(0f, 1f)] public float SprintFootsteps = 1f;
    }
}
