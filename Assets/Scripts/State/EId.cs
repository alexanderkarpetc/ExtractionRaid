using System;

namespace State
{
    public readonly struct EId : IEquatable<EId>
    {
        public readonly int Value;

        public EId(int value)
        {
            Value = value;
        }

        public static EId None => new(0);
        public bool IsValid => Value != 0;

        public bool Equals(EId other) => Value == other.Value;
        public override bool Equals(object obj) => obj is EId other && Equals(other);
        public override int GetHashCode() => Value;
        public override string ToString() => $"Entity({Value})";

        public static bool operator ==(EId a, EId b) => a.Value == b.Value;
        public static bool operator !=(EId a, EId b) => a.Value != b.Value;
    }
}
