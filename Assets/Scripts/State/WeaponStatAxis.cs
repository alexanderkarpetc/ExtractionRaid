namespace State
{
    /// <summary>
    /// Player-facing weapon stat axes that attachments modify (delta-representation
    /// option A — see docs/ai/weapon-builder/attachments/stats.md). Each axis maps to
    /// one or more raw <see cref="WeaponStats"/> fields in WeaponStatComposer.ApplyAttachments.
    ///
    /// Delta semantics are "raw stat change" (catalog convention): a positive percent
    /// changes the named stat upward, so its natural direction decides good/bad —
    /// +Damage/+MagazineSize/+Ergonomics are better; +Recoil/+Spread/+ReloadTime are worse.
    /// </summary>
    public enum WeaponStatAxis : byte
    {
        Damage,
        RateOfFire,
        MagazineSize,
        ReloadTime,
        Recoil,
        Spread,
        Ergonomics,
    }
}
