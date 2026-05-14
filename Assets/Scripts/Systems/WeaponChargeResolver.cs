using State;

namespace Systems
{
    /// <summary>
    /// Utility that determines whether a weapon requires a charge-up window before
    /// firing and, if so, how long it is. Charge-up is a payload-level concern —
    /// currently only <see cref="LaserPayloadDefinition"/> uses it.
    ///
    /// Pure functions; no state. Consumed by <c>WeaponStateMachineSystem</c>
    /// (to time the Charging phase) and <c>ShootingSystem</c> (to gate the initial
    /// Ready → Charging transition on AttackPressed).
    ///
    /// See docs/ai/weapon-builder/architecture.md §D2.
    /// </summary>
    public static class WeaponChargeResolver
    {
        /// <summary>
        /// True when the weapon's payload triggers a Charging phase before each shot.
        /// </summary>
        public static bool RequiresChargeUp(WeaponEntityState weapon)
        {
            return weapon?.PayloadDefinition is LaserPayloadDefinition;
        }

        /// <summary>
        /// Charge duration (seconds) sourced from the weapon's payload-specific stats
        /// at the selected rarity. Returns 0 for non-charge-up payloads — caller should
        /// check <see cref="RequiresChargeUp"/> first. This is the RAW payload value
        /// without delivery modulation (A4 archetype differentiation) — see
        /// <see cref="GetChargeTime(WeaponEntityState, float)"/> for the gameplay-effective time.
        /// </summary>
        public static float GetChargeTime(WeaponEntityState weapon)
        {
            if (weapon?.PayloadDefinition is LaserPayloadDefinition laser)
                return laser.SpecificByTier(weapon.PayloadCore.Rarity).ChargeTime;
            return 0f;
        }

        /// <summary>
        /// A4 — Effective charge time після applying per-delivery multiplier
        /// (pistol fast, rifle baseline, shotgun slow). Caller passes the multiplier
        /// resolved from <c>LaserConfig.ChargeTimeMultiplierFor(pattern)</c> or
        /// <c>DevCheatsLaserSection.ChargeTimeMultiplierFor(pattern)</c>.
        /// </summary>
        public static float GetChargeTime(WeaponEntityState weapon, float deliveryMultiplier)
        {
            return GetChargeTime(weapon) * deliveryMultiplier;
        }

        /// <summary>
        /// Definition-level overload for callers that don't yet have a built
        /// <see cref="WeaponEntityState"/> (e.g. Weapon Builder preview pane).
        /// </summary>
        public static bool RequiresChargeUp(PayloadCoreDefinition payload)
        {
            return payload is LaserPayloadDefinition;
        }

        /// <summary>
        /// Definition-level overload for charge time at a given rarity tier.
        /// Returns 0 for non-charge-up payloads.
        /// </summary>
        public static float GetChargeTime(PayloadCoreDefinition payload, RarityTier rarity)
        {
            if (payload is LaserPayloadDefinition laser)
                return laser.SpecificByTier(rarity).ChargeTime;
            return 0f;
        }
    }
}
