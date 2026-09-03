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

        /// <summary>
        /// Raycast from 'from' toward 'to', returning the first hit point on a collider
        /// whose transform is NOT a child of 'ignoreRoot' (use for ignoring caller's own
        /// hierarchy, e.g., player colliders). Hierarchy-based filter is robust vs tiled
        /// geometry (brick walls) where position-based filters can misclassify.
        /// </summary>
        bool RaycastFirstWallHit(Vector3 from, Vector3 to, int layerMask,
            Transform ignoreRoot, out Vector3 hitPoint);

        /// <summary>
        /// True if the line from <paramref name="from"/> to <paramref name="to"/> is blocked.
        /// Callers provide a world-geometry-only mask; character colliders live on dedicated
        /// Player/Bot layers and must not be filtered by position heuristics.
        /// </summary>
        bool IsLineOfSightBlocked(Vector3 from, Vector3 to, int layerMask);
    }
}
