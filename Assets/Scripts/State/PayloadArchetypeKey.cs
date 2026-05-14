namespace State
{
    /// <summary>
    /// Typed key for payload archetype routing — used by views для swap impact VFX +
    /// suppress blood decals + tint rim flash per archetype. Single source of truth =
    /// <c>PayloadCoreDefinition.Archetype</c> string ("Ballistic"/"Laser"). Converted to
    /// this enum at projectile-spawn time so subsequent paths (events, presenters) avoid
    /// string compares + allocations.
    /// </summary>
    public enum PayloadArchetypeKey : byte
    {
        Ballistic = 0,
        Laser     = 1,
    }

    public static class PayloadArchetypeKeyExt
    {
        /// <summary>Map archetype string (як з <c>PayloadCoreDefinition.Archetype</c>) на typed key. Default = Ballistic.</summary>
        public static PayloadArchetypeKey FromArchetypeString(string s)
            => s == "Laser" ? PayloadArchetypeKey.Laser : PayloadArchetypeKey.Ballistic;
    }
}
