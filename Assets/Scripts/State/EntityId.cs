using System;

namespace State
{
    public readonly struct EntityId : IEquatable<EntityId>
    {
        public readonly int Value;

        public EntityId(int value)
        {
            Value = value;
        }

        public static EntityId None => new(0);
        public bool IsValid => Value != 0;

        public bool Equals(EntityId other) => Value == other.Value;
        public override bool Equals(object obj) => obj is EntityId other && Equals(other);
        public override int GetHashCode() => Value;
        public override string ToString() => $"Entity({Value})";

        public static bool operator ==(EntityId a, EntityId b) => a.Value == b.Value;
        public static bool operator !=(EntityId a, EntityId b) => a.Value != b.Value;
    }
}
