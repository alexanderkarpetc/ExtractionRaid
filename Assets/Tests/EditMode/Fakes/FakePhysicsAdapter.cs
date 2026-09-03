using Adapters;
using UnityEngine;

namespace Tests.EditMode.Fakes
{
    public class FakePhysicsAdapter : IPhysicsAdapter
    {
        public bool Blocked;

        // Optional geometry hook — cover tests need per-ray answers ("ray to the
        // cover point blocked, ray to the peek point clear"), not one global flag.
        public System.Func<Vector3, Vector3, bool> LinecastFunc;

        // RaycastFirstWallHit canned result
        public bool WallHit;
        public Vector3 WallHitPoint;
        public int RaycastFirstWallHitCallCount;
        public int LineOfSightCallCount;

        public bool Linecast(Vector3 from, Vector3 to, int layerMask)
        {
            return LinecastFunc?.Invoke(from, to) ?? Blocked;
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

        public bool IsLineOfSightBlocked(Vector3 from, Vector3 to, int layerMask)
        {
            LineOfSightCallCount++;
            return Blocked;
        }
    }
}
