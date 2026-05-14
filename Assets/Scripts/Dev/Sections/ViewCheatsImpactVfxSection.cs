using UnityEngine;

namespace Dev
{
    /// <summary>
    /// Per-payload impact VFX dispatch — switches body/head impact prefabs + rim-flash tint +
    /// blood-decal suppression based на projectile archetype (Ballistic vs Laser). A2 task у
    /// archetype-differentiation pass.
    ///
    /// Authored prefab refs are optional — null = ProjectilePresenter falls back to standard
    /// ballistic prefabs з programmatic tint applied at spawn (Approach C, hybrid).
    /// </summary>
    public class ViewCheatsImpactVfxSection : ScriptableObject
    {
        public bool Enabled = true;

        [Header("Laser impact prefabs (optional — null = programmatic tint of ballistic prefab)")]
        public GameObject LaserBodyImpactPrefab;
        public GameObject LaserHeadImpactPrefab;

        [Header("Laser programmatic-fallback colors")]
        [Tooltip("Particle main.startColor applied when laser hits + no LaserImpactPrefab override exists. Drives flash/ember tint.")]
        public Color LaserFlashColor = new Color(1f, 0.55f, 0.15f, 1f);

        [Tooltip("Smoke wisp tint at laser impact (used for sub-emitter recolor).")]
        public Color LaserSmokeColor = new Color(0.15f, 0.12f, 0.10f, 0.7f);

        [Header("Blood decal suppression")]
        [Tooltip("Skip blood pool / blood splatter projector spawn when archetype is Laser. Cauterize identity.")]
        public bool SuppressBloodDecalForLaser = true;

        [Header("Rim-flash tint (CharacterHitFx)")]
        [Tooltip("Color blended into hit-flash при laser impact. KEEP LOW (~0.2): rim flash + impact VFX share orange palette, " +
                 "high blend merges rim glow into impact particles → user perceives 'no rim glow'. Low blend = rim stays visually " +
                 "distinct on silhouette while signaling 'laser hit'. 0 = no tint (rim identical to ballistic), 1 = full warm tint.")]
        [Range(0f, 1f)] public float LaserRimFlashBlend = 0.2f;

        [Tooltip("Target tint для rim-flash when laser hits. Warm orange = burn feel.")]
        public Color LaserRimFlashTint = new Color(1f, 0.55f, 0.15f, 1f);

        [Tooltip("Color tint applied to per-bone bullet-decal at laser hits. VibeCharacterShader does pure albedo replacement → keep red-bias (less yellow) to read as scorch, not sand. Sweet spot ≈ (0.40, 0.10, 0.05) (dark blood-scorch). 0 alpha = unchanged blood splat texture.")]
        public Color LaserDecalTint = new Color(0.40f, 0.10f, 0.05f, 1f);
    }
}
