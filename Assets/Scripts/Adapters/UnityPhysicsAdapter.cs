using UnityEngine;

namespace Adapters
{
    public class UnityPhysicsAdapter : IPhysicsAdapter
    {
        public bool Linecast(Vector3 from, Vector3 to)
        {
            return Physics.Linecast(from, to);
        }
    }
}
