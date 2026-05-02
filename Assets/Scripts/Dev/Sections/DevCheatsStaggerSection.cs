using UnityEngine;

namespace Dev
{
    /// <summary>
    /// Gunplay B.4 — Stagger / hit reaction tunables. Driven by hit events:
    /// state-side flags AI fire lockout (BotShootingSystem skips), view-side drives spine IK
    /// lean (FlinchPresenter rotates spine + neck + head bones temporarily).
    ///
    /// Stagger = enemy reaction to being shot. Critical for "alive feel" — без нього всі
    /// hits feel like shooting cardboard. Combat reading: bot стаггериться → не може
    /// strelyaty → counter-play stagger-lockom.
    /// </summary>
    public class DevCheatsStaggerSection : ScriptableObject
    {
        public bool Enabled = true;

        [Header("Duration")]
        [Tooltip("Stagger duration on light hit (seconds, scaled). AI fire lockout window.")]
        [Range(0f, 1.5f)] public float DurationLight = 0.25f;

        [Tooltip("Stagger duration on heavy hit (>= HeavyDamageThreshold% of MaxHp).")]
        [Range(0f, 1.5f)] public float DurationHeavy = 0.5f;

        [Tooltip("Stagger duration on headshot.")]
        [Range(0f, 1.5f)] public float DurationHeadshot = 0.6f;

        [Tooltip("Damage tier threshold (fraction of MaxHp). Hit dealing more than this = heavy stagger.")]
        [Range(0f, 1f)] public float HeavyDamageThreshold = 0.3f;

        [Header("Spine IK lean (visual)")]
        [Tooltip("Peak lean angle (degrees) for light hit — applied to spine. Distributed " +
                 "down the chain to neck/head з diminishing factors.")]
        [Range(0f, 45f)] public float LeanAngleLight = 6f;

        [Tooltip("Peak lean angle for heavy hit.")]
        [Range(0f, 45f)] public float LeanAngleHeavy = 16f;

        [Tooltip("Peak lean angle for headshot. Stronger for impact telegraph.")]
        [Range(0f, 45f)] public float LeanAngleHeadshot = 20f;

        [Tooltip("Spine bone share of lean angle. Spine + neck + head sum should ≈ 1.")]
        [Range(0f, 1f)] public float SpineLeanFraction = 0.5f;

        [Tooltip("Neck bone share of lean angle.")]
        [Range(0f, 1f)] public float NeckLeanFraction = 0.3f;

        [Tooltip("Head bone share of lean angle.")]
        [Range(0f, 1f)] public float HeadLeanFraction = 0.2f;

        [Tooltip("Ramp-up time (s, unscaled). 0 → peak. Short = sharp punch, long = soft.")]
        [Range(0f, 0.3f)] public float RampUpTime = 0.08f;

        [Tooltip("Hold time at peak (s, unscaled).")]
        [Range(0f, 0.3f)] public float HoldTime = 0.05f;

        [Tooltip("Return time (s, unscaled). Peak → zero. Length-aspect of recovery.")]
        [Range(0f, 0.6f)] public float ReturnTime = 0.2f;

        [Header("AI lockout")]
        [Tooltip("If true, staggered bots can't fire. False = visual only (cosmetic).")]
        public bool AIShootingLockout = true;
    }
}
