namespace Systems
{
    /// <summary>
    /// One-line description of what a module does, surfaced in tooltips so a player
    /// hovering a card learns the module's role before clicking.
    ///
    /// Hardcoded for the 5 Tier 1-2 modules. Returns empty for unknown ids — callers
    /// should hide the description block when empty rather than show a placeholder.
    ///
    /// Migration path (Tier 4+): move to `[SerializeField] string _description` on
    /// <c>PayloadCoreDefinition</c> / <c>DeliveryCoreDefinition</c> when SO authoring
    /// matures. Same shape as <see cref="WeaponArchetypeFlavor"/>.
    /// </summary>
    public static class WeaponModuleFlavor
    {
        public static string ForPayload(string payloadId) => payloadId switch
        {
            "BallisticRound" => "Solid bullet, grounded baseline.",
            "LaserCharge"    => "Charged energy beam — high damage, slower fire.",
            _                => string.Empty,
        };

        public static string ForDelivery(string deliveryId) => deliveryId switch
        {
            "SingleAction" => "One heavy shot, high commitment.",
            "Auto"         => "Sustained automatic fire.",
            "Scatter"      => "Close-range cone burst (multi-pellet).",
            _              => string.Empty,
        };
    }
}
