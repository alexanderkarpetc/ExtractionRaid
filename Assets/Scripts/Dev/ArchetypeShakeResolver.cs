using State;
using UnityEngine;

namespace Dev
{
    /// <summary>
    /// Pure logic для composing per-archetype camera shake profile з delivery shape ×
    /// payload modifier. Lives separately from <see cref="View.CameraShakePresenter"/>
    /// so unit-testable without scene / mono / event plumbing.
    /// </summary>
    public static class ArchetypeShakeResolver
    {
        public struct Resolved
        {
            public float KickMagnitude;
            public float KickDuration;
            public Vector3 KickDirOffset;    // world-space additive to base -fireDir
            public float TremorMagnitude;
            public float TremorDuration;
            public float TremorFrequency;
        }

        /// <summary>
        /// Compose shape × modifier. <paramref name="payloadArchetype"/> = "Ballistic"/"Laser"
        /// (case-sensitive — same strings ShootingSystem emits). Unknown payload → Ballistic
        /// modifier як safe default. Unknown delivery → Single (pistol).
        /// </summary>
        public static Resolved Resolve(ViewCheatsCameraShakeSection cfg,
            string payloadArchetype, FiringPattern deliveryPattern)
        {
            var shape = ShapeFor(cfg, deliveryPattern);
            var mod   = ModifierFor(cfg, payloadArchetype);

            return new Resolved
            {
                KickMagnitude    = shape.KickMagnitude * mod.KickMagnitudeScale,
                KickDuration     = shape.KickDuration,
                KickDirOffset    = shape.KickDirOffset,
                TremorMagnitude  = shape.TremorMagnitude * mod.TremorMagnitudeScale,
                TremorDuration   = shape.TremorDuration,
                TremorFrequency  = mod.TremorFrequency,
            };
        }

        static DeliveryShakeShape ShapeFor(ViewCheatsCameraShakeSection cfg, FiringPattern p)
        {
            switch (p)
            {
                case FiringPattern.Auto:    return cfg.AutoShape;
                case FiringPattern.Scatter: return cfg.ScatterShape;
                default:                    return cfg.SingleActionShape;
            }
        }

        static PayloadShakeModifier ModifierFor(ViewCheatsCameraShakeSection cfg, string archetype)
        {
            // String match — slow path but only fires once per shot; trivial cost.
            return archetype == "Laser" ? cfg.LaserModifier : cfg.BallisticModifier;
        }
    }
}
