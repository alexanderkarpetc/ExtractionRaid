using State;
using UnityEngine;

namespace Dev
{
    /// <summary>
    /// Laser-archetype runtime tunables. Two concerns:
    /// 1. Parabolic charge → damage curve for ALL laser payloads (replaces linear lerp).
    ///    Quick-tap laser shots should feel weak; commitment to full charge = full damage.
    /// 2. Laser+Scatter (Laser Shotgun) signature mechanic — charge modulates BOTH spread cone
    ///    width AND projectile range. Low charge = wide buckshot, full charge = focused long-range
    ///    cluster. See docs/ai/gunplay/archetype-differentiation.md.
    /// </summary>
    public class DevCheatsLaserSection : ScriptableObject
    {
        [Header("Charge → damage (parabolic curve, all lasers)")]
        [Tooltip("Damage multiplier at 0 charge. Linear was 0.3 — parabolic lets us start lower.")]
        [Range(0f, 1f)] public float ChargeDamageMin = 0.1f;

        [Tooltip("Power exponent. 1 = linear, 2 = parabolic (slow start), >2 = even slower start.")]
        [Range(1f, 4f)] public float ChargeDamagePower = 2f;

        [Header("Laser Shotgun: charge → spread cone width")]
        [Tooltip("Multiplier on weapon SpreadAngle at full charge (1.0 chargeRatio). Lower = narrower focused beam.")]
        [Range(0.05f, 1f)] public float ShotgunMinSpreadMult = 0.15f;

        [Tooltip("Multiplier on weapon SpreadAngle at zero charge. Higher = wider buckshot cone.")]
        [Range(1f, 3f)] public float ShotgunMaxSpreadMult = 1.5f;

        [Header("Laser Shotgun: charge → projectile range (lifetime)")]
        [Tooltip("Multiplier on ProjectileLifetime at zero charge. Lower = shorter close-range pellets.")]
        [Range(0.1f, 1f)] public float ShotgunMinLifetimeMult = 0.3f;

        [Tooltip("Multiplier on ProjectileLifetime at full charge. Higher = longer-reaching beam cluster.")]
        [Range(1f, 3f)] public float ShotgunMaxLifetimeMult = 1.5f;

        [Header("Per-delivery charge time multiplier (A4)")]
        [Tooltip("Multiplier on payload ChargeTime when delivery = Single (pistol). Lower = lighter sidearm winds up faster.")]
        [Range(0.1f, 3f)] public float SingleActionChargeMult = 0.6f;

        [Tooltip("Multiplier when delivery = Auto (rifle). Baseline 1.0 = unchanged from payload value.")]
        [Range(0.1f, 3f)] public float AutoChargeMult = 1.0f;

        [Tooltip("Multiplier when delivery = Scatter (shotgun). Higher = heavy weapon winds up slower (commitment).")]
        [Range(0.1f, 3f)] public float ScatterChargeMult = 1.5f;

        /// <summary>Charge-time multiplier за <see cref="FiringPattern"/>. Single/unknown → SingleActionChargeMult.</summary>
        public float ChargeTimeMultiplierFor(FiringPattern pattern) => pattern switch
        {
            FiringPattern.Auto    => AutoChargeMult,
            FiringPattern.Scatter => ScatterChargeMult,
            _                     => SingleActionChargeMult,
        };
    }
}
