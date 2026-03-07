using System;

namespace State
{
    public enum SlotType : byte
    {
        Weapon,
        Helmet,
        BodyArmor,
        Backpack,
    }

    public readonly struct InventorySlotRef : IEquatable<InventorySlotRef>
    {
        public readonly SlotType Type;
        public readonly int Index;

        public InventorySlotRef(SlotType type, int index = 0)
        {
            Type = type;
            Index = index;
        }

        public static InventorySlotRef Weapon(int index) => new(SlotType.Weapon, index);
        public static InventorySlotRef Helmet() => new(SlotType.Helmet);
        public static InventorySlotRef BodyArmor() => new(SlotType.BodyArmor);
        public static InventorySlotRef BackpackSlot(int index) => new(SlotType.Backpack, index);

        public ItemSlotType ToItemSlotType()
        {
            return Type switch
            {
                SlotType.Weapon => ItemSlotType.Weapon,
                SlotType.Helmet => ItemSlotType.Helmet,
                SlotType.BodyArmor => ItemSlotType.BodyArmor,
                SlotType.Backpack => ItemSlotType.Backpack,
                _ => ItemSlotType.None,
            };
        }

        public bool Equals(InventorySlotRef other) => Type == other.Type && Index == other.Index;
        public override bool Equals(object obj) => obj is InventorySlotRef other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(Type, Index);
        public override string ToString() => $"{Type}[{Index}]";
    }
}
