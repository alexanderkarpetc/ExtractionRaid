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
    }
}
