using UnityEngine;

namespace Adapters
{
    public interface IPhysicsAdapter
    {
        bool Linecast(Vector3 from, Vector3 to, int layerMask);

        /// <summary>
        /// Linecast that ignores colliders belonging to specific transforms (root matching).
        /// Returns true if any NON-ignored collider blocks the line.
        /// </summary>
        bool LinecastIgnoring(Vector3 from, Vector3 to, int layerMask,
            Transform ignoreRootA, Transform ignoreRootB);
    }
}
