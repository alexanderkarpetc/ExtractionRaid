namespace Constants
{
    /// <summary>
    /// Tunables for extraction zones. Per-level overrides can override
    /// <see cref="DefaultZoneRadius"/> on the spawn point; the timer length is
    /// global so the HUD can hardcode a single "required" reference.
    /// </summary>
    public static class ExtractionConstants
    {
        /// <summary>Total seconds the player must stay in a zone to extract.</summary>
        public const float ExtractDurationSeconds = 10f;

        /// <summary>Default zone radius applied to a freshly-placed spawn point.</summary>
        public const float DefaultZoneRadius = 3f;
    }
}
