using UnityEngine;

namespace State
{
    /// <summary>
    /// Delivery Core definition (ScriptableObject) — describes how a payload is fired.
    /// Unlike Payload, Delivery does not use subclasses; the behavioural difference
    /// is expressed via <see cref="FiringPattern"/> and dispatched inside ShootingSystem.
    ///
    /// Pattern-specific parameters (SpinUp/Down for Rotary, Volley for Swarm) live here
    /// directly; handlers read only what's relevant to their pattern.
    ///
    /// See docs/ai/weapon-builder/architecture.md §2, §D1.
    /// </summary>
    [CreateAssetMenu(
        fileName = "NewDeliveryCore",
        menuName = "Weapon Builder/Delivery")]
    public class DeliveryCoreDefinition : ScriptableObject
    {
        const int RarityTierCount = 5;

        [SerializeField] string _id;
        [SerializeField] string _formFactor;
        [SerializeField] FiringPattern _pattern;
        [SerializeField] DeliveryStats[] _statsByTier = new DeliveryStats[RarityTierCount];

        // ── Visualization (Tier 8.x* — delivery as barrel insert) ───────────
        [Header("Visualization")]
        [Tooltip("Delivery BARREL prefab (the actual ствол / emitter — short for Pistol, longer for " +
                 "Rifle, longest for Shotgun). Instantiated як child of payload's DeliverySocket " +
                 "at equip time. Must contain MuzzlePoint child Transform at the barrel tip. " +
                 "Не carries Animator or WeaponView — those live on payload. " +
                 "See docs/ai/weapon-builder/plan/roadmap.md Tier 8.x.")]
        [SerializeField] GameObject _barrelPrefab;

        // ── Pattern-specific params (only meaningful for the corresponding Pattern) ──
        [Header("Rotary (ignored unless Pattern == Rotary)")]
        [SerializeField] float _spinUpTime;
        [SerializeField] float _spinDownTime;

        [Header("Swarm (ignored unless Pattern == Swarm)")]
        [SerializeField] int   _volleyCount;
        [SerializeField] float _volleyInterval;

        public string        Id         => _id;
        /// <summary>Form-factor name used in weapon archetype labels (e.g. "Pistol", "Rifle", "Shotgun").</summary>
        public string        FormFactor => _formFactor;
        public FiringPattern Pattern    => _pattern;
        /// <summary>
        /// Delivery barrel prefab (3D model + MuzzlePoint child). Instantiated як child of
        /// payload's DeliverySocket at equip time. Animator + WeaponView live on payload, не
        /// here. May be null у tests; production assets must wire this.
        /// See docs/ai/weapon-builder/plan/roadmap.md Tier 8.x.
        /// </summary>
        public GameObject    BarrelPrefab => _barrelPrefab;

        /// <summary>Delivery stats for the given rarity tier.</summary>
        public DeliveryStats StatsByTier(RarityTier tier) => _statsByTier[(int)tier];

        public float SpinUpTime     => _spinUpTime;
        public float SpinDownTime   => _spinDownTime;
        public int   VolleyCount    => _volleyCount;
        public float VolleyInterval => _volleyInterval;

        void OnValidate()
        {
            if (_statsByTier == null || _statsByTier.Length != RarityTierCount)
                System.Array.Resize(ref _statsByTier, RarityTierCount);
        }
    }
}
