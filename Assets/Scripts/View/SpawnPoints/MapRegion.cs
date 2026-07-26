using System.Collections.Generic;
using UnityEngine;

namespace View.SpawnPoints
{
    /// <summary>
    /// Author-time polygon marker. Drop one on the map and add points (via the custom
    /// editor's scene handles / Ctrl-click) to outline an area. The DevCheats
    /// <c>🌍 Meta → Region raid simulator</c> scans these, buckets every loot / enemy
    /// spawn by which region polygon contains it (XZ point-in-polygon), and simulates
    /// looting a whole region into the backpack.
    ///
    /// Pure marker — no runtime gameplay behaviour. Points are stored in LOCAL space so
    /// moving/rotating the host object carries the whole outline with it.
    /// </summary>
    public class MapRegion : MonoBehaviour
    {
        [Tooltip("Shown in the DevCheats region list.")]
        public string regionName = "Region";

        [Tooltip("Polygon vertices in local space. Only X/Z matter — regions are 2D map " +
                 "areas. Order defines the outline; the last point links back to the first.")]
        public List<Vector3> points = new();

        [Tooltip("How dangerous this area is. Feeds the region raid sim's survival roll: the " +
                 "gear-based base chance is raised to this power, so 1 = the plain 70%..99% " +
                 "band, 2 = squared (a bad kit drops to ~49%), 0.5 = a safe milk run.")]
        [Range(0.25f, 4f)] public float difficultyMultiplier = 1f;

        [Tooltip("Gizmo colour (outline + fill).")]
        public Color color = new(0.25f, 0.7f, 1f, 1f);

        public int PointCount => points != null ? points.Count : 0;
        public bool IsValid => PointCount >= 3;

        public Vector3 WorldPoint(int i) => transform.TransformPoint(points[i]);

        /// <summary>XZ point-in-polygon (ray cast). Y is ignored.</summary>
        public bool ContainsXZ(Vector3 world)
        {
            if (!IsValid) return false;
            float x = world.x, z = world.z;
            bool inside = false;
            for (int i = 0, j = PointCount - 1; i < PointCount; j = i++)
            {
                Vector3 pi = WorldPoint(i), pj = WorldPoint(j);
                bool crosses = (pi.z > z) != (pj.z > z);
                if (!crosses) continue;
                float t = (z - pi.z) / (pj.z - pi.z);
                if (x < pi.x + t * (pj.x - pi.x)) inside = !inside;
            }
            return inside;
        }

#if UNITY_EDITOR
        void OnDrawGizmos() => DrawGizmo(false);
        void OnDrawGizmosSelected() => DrawGizmo(true);

        void DrawGizmo(bool selected)
        {
            if (PointCount == 0) return;

            var line = color; line.a = selected ? 1f : 0.55f;
            Gizmos.color = line;

            // Outline (closed loop).
            for (int i = 0; i < PointCount; i++)
            {
                Vector3 a = WorldPoint(i);
                Gizmos.DrawSphere(a, 0.25f);
                if (PointCount >= 2)
                    Gizmos.DrawLine(a, WorldPoint((i + 1) % PointCount));
            }

            // Centroid label.
            Vector3 c = Vector3.zero;
            for (int i = 0; i < PointCount; i++) c += WorldPoint(i);
            c /= PointCount;
            UnityEditor.Handles.color = line;
            UnityEditor.Handles.Label(c + Vector3.up * 0.5f,
                $"{regionName}  ×{difficultyMultiplier:0.##}{(IsValid ? "" : "  (needs ≥3 points)")}");
        }
#endif
    }
}
