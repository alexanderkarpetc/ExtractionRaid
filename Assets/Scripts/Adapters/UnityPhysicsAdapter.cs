using UnityEngine;

namespace Adapters
{
    public class UnityPhysicsAdapter : IPhysicsAdapter
    {
        static readonly RaycastHit[] HitBuffer = new RaycastHit[32];

        public bool Linecast(Vector3 from, Vector3 to, int layerMask)
        {
            return Physics.Linecast(from, to, layerMask);
        }

        public bool LinecastIgnoring(Vector3 from, Vector3 to, int layerMask,
            Transform ignoreRootA, Transform ignoreRootB)
        {
            var dir = to - from;
            float dist = dir.magnitude;
            if (dist < 0.001f) return false;

            int count = Physics.RaycastNonAlloc(from, dir / dist, HitBuffer, dist, layerMask);

            for (int i = 0; i < count; i++)
            {
                var hitRoot = HitBuffer[i].transform.root;
                if (ignoreRootA != null && hitRoot == ignoreRootA) continue;
                if (ignoreRootB != null && hitRoot == ignoreRootB) continue;
                return true; // blocked by something that isn't ignored
            }

            return false;
        }

        public bool RaycastFirstWallHit(Vector3 from, Vector3 to, int layerMask,
            Transform ignoreRoot, out Vector3 hitPoint)
        {
            hitPoint = default;
            var dir = to - from;
            float dist = dir.magnitude;
            if (dist < 0.001f) return false;

            int count = Physics.RaycastNonAlloc(from, dir / dist, HitBuffer, dist, layerMask);
            if (count == 0) return false;

            // Find nearest hit that is NOT in the caller's hierarchy.
            // Hierarchy check (IsChildOf) is robust vs tiled walls where proximity-based
            // filters would incorrectly skip brick colliders near the player.
            float closest = float.PositiveInfinity;
            int closestIdx = -1;
            for (int i = 0; i < count; i++)
            {
                var h = HitBuffer[i];
                if (ignoreRoot != null && h.collider.transform.IsChildOf(ignoreRoot)) continue;
                if (h.distance < closest)
                {
                    closest = h.distance;
                    closestIdx = i;
                }
            }

            if (closestIdx < 0) return false;
            hitPoint = HitBuffer[closestIdx].point;
            return true;
        }
    }
}
