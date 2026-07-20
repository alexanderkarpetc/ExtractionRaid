namespace State
{
    public class InventoryState
    {
        public const int WeaponSlotCount = 2;
        public const int BackpackSize = 20;
        public const int QuickSlotCount = 7;
        public const int QuickSlotKeyOffset = 3;

        public ItemState[] WeaponSlots = new ItemState[WeaponSlotCount];
        public ItemState HelmetSlot;
        public ItemState BodyArmorSlot;
        public ItemState[] Backpack = new ItemState[BackpackSize];
        public int[] QuickSlotBindings = { -1, -1, -1, -1, -1, -1, -1 };

        /// <summary>
        /// Mutation counter bumped by <see cref="SetSlot"/> + Systems that touch
        /// backpack arrays directly. Views compare this against a cached value
        /// to gate per-frame rebinds — when unchanged, skip the rebind work.
        /// Wraps naturally if a player ever triggers 2bn mutations у one session.
        /// </summary>
        public int Version;

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
            Version++;
        }

        public int FindFreeBackpackSlot()
        {
            for (int i = 0; i < BackpackSize; i++)
            {
                if (Backpack[i] == null) return i;
            }
            return -1;
        }

        /// <summary>
        /// Nulls every slot (weapons, helmet, body armor, backpack) and unbinds all
        /// quick slots. Used by the KIA gear-loss path (App.EndRaid) and the
        /// fresh-player loadout reset (PlayerSpawnSystem). Stash is a separate
        /// collection on the Player and is never touched here.
        /// </summary>
        public void ClearAll()
        {
            for (int i = 0; i < WeaponSlotCount; i++)
                WeaponSlots[i] = null;
            HelmetSlot = null;
            BodyArmorSlot = null;
            for (int i = 0; i < BackpackSize; i++)
                Backpack[i] = null;
            for (int i = 0; i < QuickSlotCount; i++)
                QuickSlotBindings[i] = -1;
            Version++;
        }
    }
}
