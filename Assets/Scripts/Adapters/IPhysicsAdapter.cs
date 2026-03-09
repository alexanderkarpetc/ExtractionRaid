using UnityEngine;

namespace Adapters
{
    public interface IPhysicsAdapter
    {
        bool Linecast(Vector3 from, Vector3 to, int layerMask);
    }
}
