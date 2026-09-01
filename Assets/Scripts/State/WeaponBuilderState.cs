namespace State
{
    /// <summary>
    /// Transient selection state of the Weapon Builder UI — lives only while the
    /// Builder screen is open. Each selection holds the DefinitionId + Rarity the
    /// user has chosen. Empty string DefinitionId means "not selected yet".
    ///
    /// Tier 1 scope: rarity is locked to Common (no per-selection rarity UI).
    /// Exotic slot is exposed on the state but will be hidden in the UI until Tier 5.
    ///
    /// See docs/ai/weapon-builder/README.md.
    /// </summary>
    public struct WeaponBuilderState
    {
        public PayloadCoreInstance  SelectedPayload;
        public DeliveryCoreInstance SelectedDelivery;
        public bool                 HasSelectedExotic;
        public ExoticModInstance    SelectedExotic;

        public bool HasPayload  => !string.IsNullOrEmpty(SelectedPayload.DefinitionId);
        public bool HasDelivery => !string.IsNullOrEmpty(SelectedDelivery.DefinitionId);

        public static WeaponBuilderState Empty => default;
    }
}
