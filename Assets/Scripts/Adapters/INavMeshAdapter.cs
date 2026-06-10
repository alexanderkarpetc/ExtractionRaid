using UnityEngine;

namespace Adapters
{
    public interface INavMeshAdapter
    {
        bool SamplePosition(Vector3 source, float maxDistance, out Vector3 result);

        /// <summary>
        /// Computes a path from <paramref name="from"/> to <paramref name="to"/> and writes
        /// its corners (including both endpoints) into <paramref name="corners"/>.
        /// Returns the corner count, capped at the buffer length; 0 means no path exists.
        /// </summary>
        int CalculatePath(Vector3 from, Vector3 to, Vector3[] corners);
    }
}
