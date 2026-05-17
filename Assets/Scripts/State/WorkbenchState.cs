using UnityEngine;

namespace State
{
    public class WorkbenchState
    {
        public EId Id;
        public Vector3 Position;
        public WorkbenchKind Kind;

        public static WorkbenchState Create(EId id, Vector3 position,
            WorkbenchKind kind = WorkbenchKind.Crafting)
        {
            return new WorkbenchState
            {
                Id = id,
                Position = position,
                Kind = kind,
            };
        }
    }
}
