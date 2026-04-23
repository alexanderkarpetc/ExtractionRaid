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
        /// True if the line from <paramref name="from"/> to <paramref name="to"/> is blocked
        /// by a real obstacle — colliders whose transform position lies within
        /// <paramref name="ignoreRadius"/> of either <paramref name="ignoreNearA"/> or
        /// <paramref name="ignoreNearB"/> are treated as character colliders and skipped.
        /// Used by FOV occlusion checks where character CapsuleColliders can otherwise
        /// spuriously block sight lines.
        /// </summary>
        bool IsLineOfSightBlocked(Vector3 from, Vector3 to, int layerMask,
            Vector3 ignoreNearA, Vector3 ignoreNearB, float ignoreRadius);
    }
}
