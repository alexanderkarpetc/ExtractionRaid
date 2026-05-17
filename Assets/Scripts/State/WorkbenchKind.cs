namespace State
{
    /// <summary>
    /// Determines which building-dialogue actions a workbench offers.
    /// Designer picks this on <c>WorkbenchSpawnPoint</c> and it's copied into
    /// <see cref="WorkbenchState"/> at raid start. View-side
    /// <c>BuildingDialoguePresenter</c> reads it to populate the choice list.
    /// </summary>
    public enum WorkbenchKind
    {
        Crafting = 0,
        WeaponBuilder = 1,
    }
}
