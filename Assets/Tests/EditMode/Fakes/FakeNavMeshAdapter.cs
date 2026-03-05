using Adapters;
using UnityEngine;

namespace Tests.EditMode.Fakes
{
    public class FakeNavMeshAdapter : INavMeshAdapter
    {
        public bool SamplePosition(Vector3 source, float maxDistance, out Vector3 result)
        {
            result = source;
            return true;
        }
    }
}