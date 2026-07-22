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
        [Range(0f, 1f)] public float Music = 0.5f;

        [Header("Pistol")]
        [Range(0f, 1f)] public float CloseShot = 0.4f;
        [Range(0f, 1f)] public float DistantShot = 0.4f;
        [Range(0f, 1f)] public float DryFire = 1f;
        [Range(0f, 1f)] public float Reload = 1f;
        [Range(0f, 1f)] public float Holster = 1f;
        [Range(0f, 1f)] public float Unholster = 1f;

        [Header("Rifle")]
        [Range(0f, 1f)] public float RifleShot = 0.4f;
        [Range(0f, 1f)] public float RifleDryFire = 1f;
        [Range(0f, 1f)] public float RifleReload = 1f;

        [Header("Shotgun")]
        [Range(0f, 1f)] public float ShotgunShot = 0.4f;

        [Header("Impacts")]
        [Range(0f, 1f)] public float HardSurfaceImpact = 1f;
        [Range(0f, 1f)] public float MetalImpact = 1f;
        [Range(0f, 1f)] public float FleshImpact = 1f;
        [Range(0f, 1f)] public float ArmorImpact = 1f;
        [Range(0f, 1f)] public float Ricochet = 1f;
        [Range(0f, 2f)] public float Headshot = 2f;

        [Header("Characters")]
        [Range(0f, 1f)] public float Bleeding = 1f;
        [Range(0f, 1f)] public float BodyFall = 1f;
        [Range(0f, 1f)] public float WalkFootsteps = 0.4f;
        [Range(0f, 1f)] public float SprintFootsteps = 0.4f;

        [Header("UI")]
        [Range(0f, 1f)] public float BackpackOpen = 1f;
    }
}
