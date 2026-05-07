namespace State
{
    /// <summary>
    /// How the most recent raid ended. <see cref="ApplicationCore.App.LastRaidOutcome"/>
    /// is set when the raid finishes (player KIA or extraction request) and consumed
    /// by the end-of-raid screen, then reset back to <see cref="None"/> once the player
    /// returns to the hideout.
    /// </summary>
    public enum RaidOutcome
    {
        None = 0,
        Extracted,
        KIA,
    }
}
