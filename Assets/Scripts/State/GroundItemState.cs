using UnityEngine;

namespace State
{
    public class GroundItemState
    {
        public EId Id;
        public string DefinitionId;
        public Vector3 Position;

        public string DisplayName => ItemDefinition.Get(DefinitionId)?.DisplayName ?? DefinitionId;

        public static GroundItemState Create(EId id, string definitionId, Vector3 position)
        {
            return new GroundItemState
            {
                Id = id,
                DefinitionId = definitionId,
                Position = position,
            };
        }
    }
}
