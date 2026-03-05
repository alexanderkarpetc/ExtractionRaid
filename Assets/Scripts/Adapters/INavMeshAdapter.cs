using UnityEngine;

namespace Adapters
{
    public interface INavMeshAdapter
    {
        bool SamplePosition(Vector3 source, float maxDistance, out Vector3 result);
    }
}
