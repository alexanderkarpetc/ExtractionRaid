using UnityEngine;

namespace Adapters
{
    public class UnityPhysicsAdapter : IPhysicsAdapter
    {
        public bool Linecast(Vector3 from, Vector3 to, int layerMask)
        {
            return Physics.Linecast(from, to, layerMask);
        }
    }
}
