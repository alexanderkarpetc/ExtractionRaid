using UnityEngine;

namespace Dev
{
    /// <summary>
    /// Tier 8.x* — weapon drop on death. WeaponPivot живе як sibling of skeleton у
    /// CharacterBody, тому коли character ragdoll'иться — weapon "висить у повітрі".
    /// На death event RagdollPresenter detach'ає weapon, dispose'ить через physics drop:
    /// reparent у [WeaponDropPool], add Rigidbody + small collider, AddForce у напрямку
    /// shot, despawn разом з ragdoll.
    /// </summary>
    public class ViewCheatsWeaponDropSection : ScriptableObject
    {
        public bool Enabled = true;

        [Header("Physics")]
        [Tooltip("Mass of dropped weapon Rigidbody. Heavier = more inertia, less spin.")]
        [Range(0.1f, 10f)] public float Mass = 1.5f;

        [Tooltip("Linear drag — slows tumble через air. Higher = settles faster.")]
        [Range(0f, 5f)] public float LinearDamping = 0.3f;

        [Tooltip("Angular drag — slows spin.")]
        [Range(0f, 10f)] public float AngularDamping = 1f;

        [Header("Impulse")]
        [Tooltip("Multiplier на ragdoll impulse magnitude — weapon flies у same direction " +
                 "як shot trajectory but scaled. 0.5 = half ragdoll speed, нерезкий drop.")]
        [Range(0f, 2f)] public float ImpulseScale = 0.5f;

        [Tooltip("Random angular impulse magnitude — weapon spins randomly як it falls. " +
                 "Adds natural feel; without — weapon flies stably (looks robotic).")]
        [Range(0f, 5f)] public float TorqueScale = 0.8f;

        [Tooltip("Upward bias for impulse — adds lift so weapon arcs out of dead hand " +
                 "instead of dropping straight down.")]
        [Range(0f, 1f)] public float UpwardImpulseBias = 0.3f;

        [Header("Lifetime")]
        [Tooltip("Total weapon lifetime у scene (seconds). Should match ragdoll lifetime " +
                 "so corpse + dropped weapon vanish together.")]
        [Range(5f, 120f)] public float Lifetime = 30f;

        [Header("Collision")]
        [Tooltip("Add a primitive Box collider to dropped weapon. Lets it land on floor " +
                 "instead of clipping through. False = no collider, weapon free-falls + " +
                 "auto-despawns.")]
        public bool AddCollider = true;

        [Tooltip("Collider size (half-extents). Approximate weapon bounding box.")]
        public Vector3 ColliderHalfSize = new Vector3(0.05f, 0.05f, 0.20f);
    }
}
