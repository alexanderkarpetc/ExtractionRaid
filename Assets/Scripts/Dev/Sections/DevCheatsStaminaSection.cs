using Constants;
using UnityEngine;

namespace Dev
{
    /// <summary>
    /// Gameplay stamina tunables (sprint resource). Migrated from <see cref="StaminaConstants"/>
    /// 2026-05-26 so the exhaustion lockout threshold + drain/regen feel can be tuned live.
    /// Read by systems via <c>RaidContext.StaminaConfig</c> (never DevCheats directly — rule 6.7).
    /// Defaults mirror StaminaConstants so behavior is unchanged until tuned.
    /// </summary>
    public class DevCheatsStaminaSection : ScriptableObject
    {
        [Tooltip("Max stamina pool.")]
        public float MaxStamina = StaminaConstants.MaxStamina;

        [Tooltip("Stamina drained per second while sprinting.")]
        public float SprintDrainRate = StaminaConstants.SprintDrainRate;

        [Tooltip("Stamina regained per second once regen kicks in.")]
        public float RegenRate = StaminaConstants.RegenRate;

        [Tooltip("Seconds after sprint stops before regen begins.")]
        public float RegenDelay = StaminaConstants.RegenDelay;

        [Tooltip("Movement speed multiplier while sprinting.")]
        public float SprintSpeedMultiplier = StaminaConstants.SprintSpeedMultiplier;

        [Header("Exhaustion lockout (hysteresis)")]
        [Tooltip("When stamina hits 0 the player is locked out of sprinting until stamina " +
                 "recovers to this fraction of max (0.10 = 10%). Prevents stutter-sprint.")]
        [Range(0f, 0.9f)] public float ExhaustionRecoveryRatio = 0.10f;
    }
}
