using System;

namespace State
{
    // Instances live inside WeaponConfiguration (persistent, in InventoryItem).
    // They hold *identity only* — DefinitionId (string) and Rarity (enum).
    // Actual definition data is resolved via ICoreDefinitionRegistry at equip time.
    //
    // Using [Serializable] readonly struct with IEquatable<T>:
    //   - value semantics (no GC pressure on copy)
    //   - immutable by design (changing rarity = new instance)
    //   - structural equality
    //   - Unity-friendly serialization via public readonly fields
    //
    // See docs/ai/weapon-builder/README.md.

    /// <summary>Reference to a Payload Core — DefinitionId + Rarity.</summary>
    [Serializable]
    public readonly struct PayloadCoreInstance : IEquatable<PayloadCoreInstance>
    {
        public readonly string     DefinitionId;
        public readonly RarityTier Rarity;

        public PayloadCoreInstance(string definitionId, RarityTier rarity)
        {
            DefinitionId = definitionId;
            Rarity = rarity;
        }

        public bool Equals(PayloadCoreInstance other) =>
            string.Equals(DefinitionId, other.DefinitionId, StringComparison.Ordinal)
            && Rarity == other.Rarity;

        public override bool Equals(object obj) =>
            obj is PayloadCoreInstance other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int h = DefinitionId != null ? StringComparer.Ordinal.GetHashCode(DefinitionId) : 0;
                return (h * 397) ^ (int)Rarity;
            }
        }

        public static bool operator ==(PayloadCoreInstance a, PayloadCoreInstance b) => a.Equals(b);
        public static bool operator !=(PayloadCoreInstance a, PayloadCoreInstance b) => !a.Equals(b);

        public override string ToString() => $"Payload[{DefinitionId}, {Rarity}]";
    }

    /// <summary>Reference to a Delivery Core — DefinitionId + Rarity.</summary>
    [Serializable]
    public readonly struct DeliveryCoreInstance : IEquatable<DeliveryCoreInstance>
    {
        public readonly string     DefinitionId;
        public readonly RarityTier Rarity;

        public DeliveryCoreInstance(string definitionId, RarityTier rarity)
        {
            DefinitionId = definitionId;
            Rarity = rarity;
        }

        public bool Equals(DeliveryCoreInstance other) =>
            string.Equals(DefinitionId, other.DefinitionId, StringComparison.Ordinal)
            && Rarity == other.Rarity;

        public override bool Equals(object obj) =>
            obj is DeliveryCoreInstance other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int h = DefinitionId != null ? StringComparer.Ordinal.GetHashCode(DefinitionId) : 0;
                return (h * 397) ^ (int)Rarity;
            }
        }

        public static bool operator ==(DeliveryCoreInstance a, DeliveryCoreInstance b) => a.Equals(b);
        public static bool operator !=(DeliveryCoreInstance a, DeliveryCoreInstance b) => !a.Equals(b);

        public override string ToString() => $"Delivery[{DefinitionId}, {Rarity}]";
    }

    /// <summary>
    /// Reference to an Exotic Mod — DefinitionId only.
    /// No rarity; rarity applies to Payload and Delivery cores only.
    /// </summary>
    [Serializable]
    public readonly struct ExoticModInstance : IEquatable<ExoticModInstance>
    {
        public readonly string DefinitionId;

        public ExoticModInstance(string definitionId)
        {
            DefinitionId = definitionId;
        }

        public bool Equals(ExoticModInstance other) =>
            string.Equals(DefinitionId, other.DefinitionId, StringComparison.Ordinal);

        public override bool Equals(object obj) =>
            obj is ExoticModInstance other && Equals(other);

        public override int GetHashCode() =>
            DefinitionId != null ? StringComparer.Ordinal.GetHashCode(DefinitionId) : 0;

        public static bool operator ==(ExoticModInstance a, ExoticModInstance b) => a.Equals(b);
        public static bool operator !=(ExoticModInstance a, ExoticModInstance b) => !a.Equals(b);

        public override string ToString() => $"Exotic[{DefinitionId}]";
    }
}
