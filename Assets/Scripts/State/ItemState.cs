namespace State
{
    public class ItemState
    {
        public EId Id;
        public string DefinitionId;
        public int StackCount = 1;

        // Armor durability (persisted on item for loot transfer)
        // -1 = use ItemDefinition defaults on first equip
        public float CurrentDurability = -1f;
        public float MaxDurability = -1f;

        // Weapon-builder composition (only populated for weapon items).
        // Set via CreateWeapon() / WeaponItemFactory.
        // Assembled into a runtime WeaponEntityState by WeaponSyncSystem / PlayerSpawnSystem.
        public bool HasWeaponConfiguration;
        public WeaponConfiguration WeaponConfiguration;

        public bool HasCustomDurability => CurrentDurability >= 0f;

        public ItemDefinition Definition => ItemDefinition.Get(DefinitionId);
        public string DisplayName => Definition?.DisplayName ?? DefinitionId;

        public static ItemState Create(EId id, string definitionId, int stackCount = 1)
        {
            return new ItemState { Id = id, DefinitionId = definitionId, StackCount = stackCount };
        }

        public static ItemState CreateWeapon(EId id, string definitionId, WeaponConfiguration configuration)
        {
            return new ItemState
            {
                Id = id,
                DefinitionId = definitionId,
                StackCount = 1,
                HasWeaponConfiguration = true,
                WeaponConfiguration = configuration,
            };
        }
    }
}
