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

        public int CalculatePath(Vector3 from, Vector3 to, Vector3[] corners)
        {
            if (corners == null || corners.Length < 2)
                return 0;

            corners[0] = from;
            corners[1] = to;
            return 2;
        }
    }
}