using System;

namespace State
{
    /// <summary>
    /// A single attachment installed on a weapon, stored in
    /// <see cref="WeaponConfiguration.Attachments"/>. Holds the slot it occupies
    /// plus the <see cref="AttachmentDefinition"/> id. Attachments have no rarity,
    /// matching <see cref="ExoticModInstance"/>.
    ///
    /// Value semantics + IEquatable, mirroring PayloadCoreInstance / DeliveryCoreInstance.
    /// </summary>
    [Serializable]
    public readonly struct AttachmentInstance : IEquatable<AttachmentInstance>
    {
        public readonly AttachmentSlot Slot;
        public readonly string DefinitionId;

        public AttachmentInstance(AttachmentSlot slot, string definitionId)
        {
            Slot = slot;
            DefinitionId = definitionId;
        }

        public bool Equals(AttachmentInstance other)
            => Slot == other.Slot && DefinitionId == other.DefinitionId;

        public override bool Equals(object obj) => obj is AttachmentInstance other && Equals(other);

        public override int GetHashCode()
            => ((int)Slot * 397) ^ (DefinitionId?.GetHashCode() ?? 0);
    }
}
