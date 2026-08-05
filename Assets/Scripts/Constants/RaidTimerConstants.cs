namespace Constants
{
    /// <summary>
    /// Raid clock defaults (M1.2). Live tuning goes through <c>Dev Cheats → ⏱ Raid</c>; these are
    /// the fallbacks tests and the no-DevCheats path use.
    ///
    /// A duration of 0 means "no limit" — that is how the hideout, TestScene and every shooting
    /// range opt out (see <c>RaidSession.ResolveRaidDuration</c>), so there is no separate on/off
    /// flag to keep in sync.
    /// </summary>
    public static class RaidTimerConstants
    {
        /// <summary>Seconds of a normal raid before the player is KIA'd.</summary>
        public const float DefaultDurationSeconds = 600f;   // 10 min

        /// <summary>Remaining seconds at which the HUD switches to the warning look.</summary>
        public const float DefaultWarnAtSeconds = 120f;

        /// <summary>Remaining seconds at which the HUD switches to the critical look.</summary>
        public const float DefaultCriticalAtSeconds = 30f;

        /// <summary>Sentinel for "this level has no raid clock".</summary>
        public const float NoLimit = 0f;
    }
}
