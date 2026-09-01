namespace State
{
    /// <summary>
    /// Attachment slot categories. Slots are granted by the weapon's cores
    /// (Payload: Buttstock/Optic/Magazine; Delivery: Muzzle/Grip) — see
    /// docs/ai/weapon-builder/README.md. The category an attachment
    /// occupies is fixed by its <see cref="AttachmentDefinition.Slot"/>.
    /// </summary>
    public enum AttachmentSlot : byte
    {
        Muzzle,
        Grip,
        Buttstock,
        Optic,
        Magazine,
    }
}
