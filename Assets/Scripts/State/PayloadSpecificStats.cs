using System;

namespace State
{
    // Payload-specific stats live on typed *PayloadDefinition subclasses.
    // Each struct represents per-rarity specific values for one payload archetype.
    // Ballistic has no specific stats (empty marker struct not needed).
    // See docs/ai/weapon-builder/README.md.

    /// <summary>Laser Charge payload-specific stats.</summary>
    [Serializable]
    public struct LaserSpecificStats
    {
        /// <summary>Time (seconds) the weapon must hold-charge before a shot releases.</summary>
        public float ChargeTime;
    }

    /// <summary>Micro-Rocket payload-specific stats.</summary>
    [Serializable]
    public struct RocketSpecificStats
    {
        /// <summary>Radius (world units) of the impact explosion AoE.</summary>
        public float ExplosionRadius;
    }

    /// <summary>Adhesive Foam payload-specific stats.</summary>
    [Serializable]
    public struct FoamSpecificStats
    {
        /// <summary>Duration (seconds) of movement-speed slow applied to hit targets.</summary>
        public float SlowDuration;

        /// <summary>Duration (seconds) that foam residue sticks to surfaces / targets.</summary>
        public float StickDuration;
    }
}
