using UnityEngine;

namespace State
{
    public class DeployPointState
    {
        public EId Id;
        public Vector3 Position;

        public static DeployPointState Create(EId id, Vector3 position)
        {
            return new DeployPointState
            {
                Id = id,
                Position = position,
            };
        }
    }
}
