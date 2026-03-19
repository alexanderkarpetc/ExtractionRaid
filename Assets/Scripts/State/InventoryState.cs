namespace State
{
    public class InventoryState
    {
        public const int WeaponSlotCount = 2;
        public const int BackpackSize = 20;
        public const int QuickSlotCount = 6;
        public const int QuickSlotKeyOffset = 4;

        public ItemState[] WeaponSlots = new ItemState[WeaponSlotCount];
        public ItemState HelmetSlot;
        public ItemState BodyArmorSlot;
        public ItemState[] Backpack = new ItemState[BackpackSize];
        public int[] QuickSlotBindings = { -1, -1, -1, -1, -1, -1 };

        public ItemState GetSlot(InventorySlotRef slot)
        {
            return slot.Type switch
            {
                SlotType.Weapon => WeaponSlots[slot.Index],
                SlotType.Helmet => HelmetSlot,
                SlotType.BodyArmor => BodyArmorSlot,
                SlotType.Backpack => Backpack[slot.Index],
                _ => null,
            };
        }

        public void SetSlot(InventorySlotRef slot, ItemState item)
        {
            switch (slot.Type)
            {
                case SlotType.Weapon:
                    WeaponSlots[slot.Index] = item;
                    break;
                case SlotType.Helmet:
                    HelmetSlot = item;
                    break;
                case SlotType.BodyArmor:
                    BodyArmorSlot = item;
                    break;
                case SlotType.Backpack:
                    Backpack[slot.Index] = item;
                    break;
            }
        }

        public int FindFreeBackpackSlot()
        {
            for (int i = 0; i < BackpackSize; i++)
            {
                if (Backpack[i] == null) return i;
            }
            return -1;
        }
    }
}
