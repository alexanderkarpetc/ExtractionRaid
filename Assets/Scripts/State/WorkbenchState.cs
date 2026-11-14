using UnityEngine;

namespace State
{
    public class WorkbenchState
    {
        public EId Id;
        public Vector3 Position;
        public BuildingKind Kind;
        public Vector3 InteractionCenter;
        public Vector3 InteractionSize = new Vector3(2f, 3f, 2f);
        public Quaternion InteractionRotation = Quaternion.identity;
        public bool IsPlayerInInteractionZone;

        public static WorkbenchState Create(EId id, Vector3 position,
            BuildingKind kind = BuildingKind.Crafting)
        {
            return new WorkbenchState
            {
                Id = id,
                Position = position,
                Kind = kind,
                InteractionCenter = position + Vector3.forward,
            };
        }
    }
}
