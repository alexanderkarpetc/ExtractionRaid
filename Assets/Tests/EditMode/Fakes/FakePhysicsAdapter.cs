using Adapters;
using UnityEngine;

namespace Tests.EditMode.Fakes
{
    public class FakePhysicsAdapter : IPhysicsAdapter
    {
        public bool Blocked;

        // RaycastFirstWallHit canned result
        public bool WallHit;
        public Vector3 WallHitPoint;
        public int RaycastFirstWallHitCallCount;

        public bool Linecast(Vector3 from, Vector3 to, int layerMask)
        {
            return Blocked;
        }

        public bool LinecastIgnoring(Vector3 from, Vector3 to, int layerMask,
            Transform ignoreRootA, Transform ignoreRootB)
        {
            return Blocked;
        }

        public bool RaycastFirstWallHit(Vector3 from, Vector3 to, int layerMask,
            Transform ignoreRoot, out Vector3 hitPoint)
        {
            RaycastFirstWallHitCallCount++;
            hitPoint = WallHit ? WallHitPoint : default;
            return WallHit;
        }

        public bool IsLineOfSightBlocked(Vector3 from, Vector3 to, int layerMask,
            Vector3 ignoreNearA, Vector3 ignoreNearB, float ignoreRadius)
        {
            return Blocked;
        }
    }
}
