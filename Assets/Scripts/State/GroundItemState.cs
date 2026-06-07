using UnityEngine;

namespace State
{
    public class GroundItemState
    {
        public EId Id;
        public string DefinitionId;
        public Vector3 Position;
        public int StackCount = 1;

        // Consumable resource pool (e.g. medkit charge). Preserved through
        // inventory → ground → inventory so a half-used medkit isn't refilled
        // by dropping it. -1 = full/uninitialized.
        public int Resource = -1;

        // Weapon-builder composition (only populated for weapon items).
        // Preserved through inventory → ground → inventory cycles so dropped
        // weapons keep their build.
        public bool HasWeaponConfiguration;
        public WeaponConfiguration WeaponConfiguration;

        public string DisplayName => ItemDefinition.Get(DefinitionId)?.DisplayName ?? DefinitionId;

        public static GroundItemState Create(EId id, string definitionId, Vector3 position, int stackCount = 1, int resource = -1)
        {
            return new GroundItemState
            {
                Id = id,
                DefinitionId = definitionId,
                Position = position,
                StackCount = stackCount,
                Resource = resource,
            };
        }

        public static GroundItemState CreateWeapon(EId id, string definitionId, Vector3 position,
            WeaponConfiguration configuration)
        {
            return new GroundItemState
            {
                Id = id,
                DefinitionId = definitionId,
                Position = position,
                StackCount = 1,
                HasWeaponConfiguration = true,
                WeaponConfiguration = configuration,
            };
        }
    }
}
