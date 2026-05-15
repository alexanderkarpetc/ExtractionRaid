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
        [Header("Charge duration (seconds, before delivery multiplier)")]
        [Tooltip("Base charge time override (seconds). 0 = use payload asset value (per-rarity from LaserPayloadDefinition). " +
                 ">0 = override all rarities. Delivery multiplier (Single/Auto/Scatter) still applies on top.")]
        [Range(0f, 5f)] public float ChargeTimeOverrideSeconds = 0f;

        [Header("Charge time → ratio shape")]
        [Tooltip("Exponent on linear t (elapsed/chargeTime) → chargeRatio. 1 = linear (legacy). " +
                 ">1 = ease-in (slow start, fast finish — feels like building tension; reward for full commitment). " +
                 "<1 = ease-out (fast start, slow finish — quick-tap players get more bang for short holds). " +
                 "Examples з t=0.3: power=0.2 → ratio≈0.79; power=0.5 → ratio≈0.55; power=2 → ratio≈0.09. " +
                 "Drives BOTH gameplay damage/burst/spread AND cursor charge fill in sync.")]
        [Range(0.1f, 6f)] public float ChargeRatioPower = 1f;

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

        /// <summary>
        /// Map linear charge time t (elapsed / chargeTime, clamped to [0,1]) to a curved chargeRatio
        /// via <c>ratio = t^ChargeRatioPower</c>. Drives gameplay AND cursor fill in sync.
        /// Power=1 → linear identity (backward compat). >1 = ease-in (slow start). &lt;1 = ease-out (fast start).
        /// </summary>
        public float EvaluateChargeRatio(float linearT)
        {
            float t = Mathf.Clamp01(linearT);
            float power = Mathf.Max(0.01f, ChargeRatioPower);
            return Mathf.Pow(t, power);
        }
    }
}
