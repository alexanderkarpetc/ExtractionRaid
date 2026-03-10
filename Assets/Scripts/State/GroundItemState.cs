using UnityEngine;

namespace State
{
    public class GroundItemState
    {
        public EId Id;
        public string DefinitionId;
        public Vector3 Position;
        public int StackCount = 1;

        public string DisplayName => ItemDefinition.Get(DefinitionId)?.DisplayName ?? DefinitionId;

        public static GroundItemState Create(EId id, string definitionId, Vector3 position, int stackCount = 1)
        {
            return new GroundItemState
            {
                Id = id,
                DefinitionId = definitionId,
                Position = position,
                StackCount = stackCount,
            };
        }
    }
}
