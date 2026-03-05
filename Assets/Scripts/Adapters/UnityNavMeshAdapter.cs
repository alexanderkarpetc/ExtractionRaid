using UnityEngine;
using UnityEngine.AI;

namespace Adapters
{
    public class UnityNavMeshAdapter : INavMeshAdapter
    {
        public bool SamplePosition(Vector3 source, float maxDistance, out Vector3 result)
        {
            if (NavMesh.SamplePosition(source, out var hit, maxDistance, NavMesh.AllAreas))
            {
                result = hit.position;
                return true;
            }

            result = source;
            return false;
        }
    }
}
