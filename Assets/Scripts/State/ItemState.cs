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

        // Consumable resource pool (e.g. medkit healing charge), persisted on the
        // item so a half-used medkit keeps its remaining charge through loot/save.
        // -1 = uninitialized → treated as a full pool (ItemDefinition.MaxResource).
        public int Resource = -1;

        // Weapon-builder composition (only populated for weapon items).
        // Set via CreateWeapon() / WeaponItemFactory.
        // Assembled into a runtime WeaponEntityState by WeaponSyncSystem / PlayerSpawnSystem.
        public bool HasWeaponConfiguration;
        public WeaponConfiguration WeaponConfiguration;
        // Bumped whenever the configuration changes in place (e.g. attachment install/
        // remove). WeaponSyncSystem rebuilds the equipped runtime weapon when this differs
        // from the built weapon's ConfigVersion — the D6 re-assembly trigger for live edits.
        public int WeaponConfigVersion;

        public bool HasCustomDurability => CurrentDurability >= 0f;

        // Resource-pool helpers (medkit-style consumables). MaxResource comes from
        // the definition; CurrentResource resolves the -1 "full" sentinel to max.
        public int MaxResource => Definition?.MaxResource ?? 0;
        public bool IsResourceItem => MaxResource > 0;
        public int CurrentResource => Resource >= 0 ? Resource : MaxResource;

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
