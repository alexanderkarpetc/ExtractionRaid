namespace Systems
{
    /// <summary>
    /// One-line flavor text for a weapon archetype, keyed by (payloadId, deliveryId).
    /// Lives next to <c>WeaponArchetypeLabel</c> and answers a different question:
    /// <i>Label</i> says what it is ("Ballistic Pistol"), <i>Flavor</i> says how it
    /// feels ("Reliable single-shot sidearm").
    ///
    /// Hardcoded for the 6 Tier 1-2 archetypes. Returns empty for unknown combos —
    /// callers should hide the line when empty rather than show "—" or similar.
    ///
    /// Migration path (Tier 4+): move to a `[SerializeField] string _flavor` on
    /// <c>PayloadCoreDefinition</c> + per-Delivery override table when the data
    /// authoring story matures (rarity authoring forces SO migration anyway).
    /// </summary>
    public static class WeaponArchetypeFlavor
    {
        public static string For(string payloadId, string deliveryId)
        {
            if (string.IsNullOrEmpty(payloadId) || string.IsNullOrEmpty(deliveryId))
                return string.Empty;

            return (payloadId, deliveryId) switch
            {
                ("BallisticRound", "SingleAction") => "Reliable single-shot sidearm",
                ("BallisticRound", "Auto")         => "Versatile sustained-fire rifle",
                ("BallisticRound", "Scatter")      => "Close-range pellet burst",
                ("LaserCharge",    "SingleAction") => "High-damage charged pistol shot",
                ("LaserCharge",    "Auto")         => "Charged auto-fire energy rifle",
                ("LaserCharge",    "Scatter")      => "Charged scatter beam burst",
                _                                  => string.Empty,
            };
        }
    }
}
