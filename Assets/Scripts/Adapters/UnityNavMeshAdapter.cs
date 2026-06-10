using UnityEngine;
using UnityEngine.AI;

namespace Adapters
{
    public class UnityNavMeshAdapter : INavMeshAdapter
    {
        // Endpoints are snapped to the mesh before pathing — CalculatePath fails outright
        // on off-mesh points, and bot positions drift slightly off-mesh between clamps.
        const float EndpointSnapDistance = 2f;

        readonly NavMeshPath _path = new NavMeshPath();

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

        public int CalculatePath(Vector3 from, Vector3 to, Vector3[] corners)
        {
            if (corners == null || corners.Length == 0)
                return 0;

            if (!NavMesh.SamplePosition(from, out var fromHit, EndpointSnapDistance, NavMesh.AllAreas))
                return 0;
            if (!NavMesh.SamplePosition(to, out var toHit, EndpointSnapDistance, NavMesh.AllAreas))
                return 0;

            if (!NavMesh.CalculatePath(fromHit.position, toHit.position, NavMesh.AllAreas, _path)
                || _path.status == NavMeshPathStatus.PathInvalid)
                return 0;

            return _path.GetCornersNonAlloc(corners);
        }
    }
}
