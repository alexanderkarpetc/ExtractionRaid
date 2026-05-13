using UnityEngine;

namespace Dev
{
    /// <summary>
    /// Ballistic Rifle signature mechanic (B1) — barrel heat from sustained fire grows spread.
    /// Identity: rifle = workhorse що rewards burst discipline. Mag-dump розмазує кулі, tap-burst
    /// stays accurate. Applies ONLY to Ballistic payload + Auto delivery; other 5 archetypes
    /// have no increment path (HeatLevel stays 0).
    ///
    /// Decay tikes continuously through <c>WeaponHeatSystem</c> — heat persists across reload
    /// + weapon swap, never hard-reset. Smooth curve via <c>HeatCurvePower</c> (default 1.8 →
    /// early forgiving, late punishing). Telegraph через crosshair bloom + tint + barrel emission glow.
    /// </summary>
    public class DevCheatsBarrelHeatSection : ScriptableObject
    {
        [Tooltip("Master switch. False = heat increments + spread modulation are skipped (decay still runs but stays at 0).")]
        public bool Enabled = true;

        [Header("Heat accumulation")]
        [Tooltip("How many sustained shots до full heat (1.0). 10 = ~1.9s at 0.19s/shot rifle pace.")]
        [Range(2, 30)] public int MaxHeatShots = 10;

        [Tooltip("Heat decay per second while not firing (and continuously). 0.4 = full→empty in 2.5s. Lower = sustained-fire penalty stays longer.")]
        [Range(0.05f, 3f)] public float DecayPerSecond = 0.4f;

        [Header("Heat → spread curve")]
        [Tooltip("Curve exponent for heat → spread multiplier. 1 = linear, 2+ = early forgiving. Lower = penalty bites at mid-heat.")]
        [Range(1f, 4f)] public float HeatCurvePower = 1.3f;

        [Tooltip("Spread cone multiplier at HeatLevel = 1. 3 = noticeable, 5 = clearly punishing, 8+ = sniper-rifle-into-blunderbuss.")]
        [Range(1f, 10f)] public float MaxSpreadMultiplier = 5f;

        [Header("Telegraph — crosshair tint")]
        [Tooltip("Tint applied to crosshair lines at full heat (heat=1). Lerped from default color по pow(heat, CurvePower).")]
        public Color HotTintColor = new Color(1f, 0.5f, 0.2f, 1f);

        [Header("Telegraph — barrel emission glow")]
        [Tooltip("Emission color at full heat. Multiplied by HeatLevel² so early glow is soft, late = bright.")]
        public Color BarrelEmissionColor = new Color(1.5f, 0.4f, 0.1f, 1f);

        [Tooltip("Emission intensity multiplier. 0 = no glow.")]
        [Range(0f, 8f)] public float BarrelEmissionIntensity = 3f;
    }
}
