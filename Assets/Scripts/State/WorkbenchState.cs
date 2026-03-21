using UnityEngine;

namespace State
{
    public class WorkbenchState
    {
        public EId Id;
        public Vector3 Position;

        public static WorkbenchState Create(EId id, Vector3 position)
        {
            return new WorkbenchState
            {
                Id = id,
                Position = position,
            };
        }
    }
}
