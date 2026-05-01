using UnityEngine;

namespace Dev
{
    /// <summary>
    /// Gunplay A.5 — Muzzle flash + real-time light pulse tunables.
    /// Existing <c>WeaponView.PlayMuzzleFlash</c> particle behavior preserved; this section
    /// drives the optional Point Light pulse layered on top.
    /// </summary>
    public class DevCheatsMuzzleVfxSection : ScriptableObject
    {
        public bool LightEnabled = true;

        [Tooltip("Peak intensity of muzzle Light at flash moment (URP Lit units).")]
        [Range(0f, 50f)] public float LightIntensity = 12f;

        [Tooltip("Duration of light pulse decay to zero (seconds, unscaled).")]
        [Range(0.02f, 0.5f)] public float LightDuration = 0.06f;

        [Tooltip("Color of muzzle light (Ballistic warm orange, Laser cool blue/white — per-prefab override at material level).")]
        public Color LightColor = new(1f, 0.7f, 0.3f, 1f);

        [Tooltip("Light range in world units.")]
        [Range(0.5f, 10f)] public float LightRange = 3f;
    }
}
