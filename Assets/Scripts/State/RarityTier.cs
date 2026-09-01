namespace State
{
    /// <summary>
    /// Rarity tier for Payload and Delivery core modules.
    /// Explicit int values are required — they are used as indices into
    /// serialized stats tables (CommonPayloadStats[], DeliveryStats[]).
    /// See docs/ai/weapons.md.
    /// </summary>
    public enum RarityTier
    {
        Common    = 0,
        Uncommon  = 1,
        Rare      = 2,
        Epic      = 3,
        Legendary = 4,
    }
}
