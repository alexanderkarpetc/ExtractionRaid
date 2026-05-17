namespace State
{
    /// <summary>
    /// Identifies what kind of building the player is interacting with. Drives the
    /// option list shown by <c>BuildingDialoguePresenter</c>. All kinds today share
    /// a single state class (<see cref="WorkbenchState"/>) and a single spawn point
    /// (<c>WorkbenchSpawnPoint</c>) — the legacy "Workbench" names predate this
    /// generalization. Per-kind gameplay logic is added later; v1 just shows a
    /// kind-appropriate dialogue with placeholder actions.
    /// </summary>
    public enum BuildingKind
    {
        Crafting = 0,
        WeaponBuilder = 1,
        Stash = 2,
        SupplyTerminal = 3,
        MedStation = 4,
        QuestTerminal = 5,
    }
}
