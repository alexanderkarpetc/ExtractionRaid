using System;
using System.Collections.Generic;
using UnityEngine;

namespace ExtractionRaid.Editor.Roads
{
    public static class IntersectionGeometry
    {
        public static RoadMeshData Trim(RoadMeshData source, int stride, bool start, float distance, out Vector3[] mouth)
        {
            if (stride < 2 || source.vertices.Length % stride != 0 || !Finite(distance) || distance <= 0)
                throw new ArgumentException("Invalid approach geometry or Cutback.");
            int rows = source.vertices.Length / stride;
            int row = start ? 0 : rows - 1, step = start ? 1 : -1;
            float travelled = 0;
            while (travelled < distance && row + step >= 0 && row + step < rows)
            {
                int next = row + step;
                travelled += Vector3.Distance(Center(source, row, stride), Center(source, next, stride));
                row = next;
            }
            if (travelled < distance || (start ? rows - row : row + 1) < 2)
                throw new ArgumentException("Cutback consumes an entire road. Reduce Cutback or extend the approach.");
            mouth = new Vector3[stride];
            Array.Copy(source.vertices, row * stride, mouth, 0, stride);
            int first = start ? row : 0, keptRows = start ? rows - row : row + 1;
            var result = new RoadMeshData
            {
                vertices = Slice(source.vertices, first * stride, keptRows * stride),
                paintUV = Slice(source.paintUV, first * stride, keptRows * stride),
                detailUV = Slice(source.detailUV, first * stride, keptRows * stride),
                markingUV = Slice(source.markingUV, first * stride, keptRows * stride),
                triangles = new int[(keptRows - 1) * (stride - 1) * 6],
                length = source.length
            };
            int index = 0;
            for (int r = 0; r < keptRows - 1; r++)
            for (int c = 0; c < stride - 1; c++)
            {
                int a = r * stride + c, b = a + stride;
                result.triangles[index++] = a; result.triangles[index++] = b; result.triangles[index++] = a + 1;
                result.triangles[index++] = a + 1; result.triangles[index++] = b; result.triangles[index++] = b + 1;
            }
            return result;
        }

        public static RoadMeshData Build(IReadOnlyList<Vector3[]> mouths, Vector3 center, float rounding,
            int cornerSegments, int rings, float tileSize, RoadSurfaceProjector project = null, float lift = 0)
        {
            if (mouths == null || mouths.Count < 3 || mouths.Count > 4 || !Finite(rounding) || rounding < 0 || rounding > 1 ||
                cornerSegments < 1 || cornerSegments > 32 || rings < 1 || rings > 32 || !Finite(tileSize) || tileSize < 0.1f)
                throw new ArgumentException("Use 3 or 4 approaches, valid rounding, subdivision and texture settings.");
            if (!Finite(center.x) || !Finite(center.y) || !Finite(center.z) || !Finite(lift) || lift < 0)
                throw new ArgumentException("Intersection position and lift must be finite.");
            var ports = new List<Vector3[]>();
            foreach (var mouth in mouths)
            {
                if (mouth == null || mouth.Length < 2) throw new ArgumentException("Invalid road mouth.");
                foreach (var p in mouth)
                    if (!Finite(p.x) || !Finite(p.y) || !Finite(p.z)) throw new ArgumentException("Road mouth contains non-finite coordinates.");
                var copy = (Vector3[])mouth.Clone();
                var mid = (copy[0] + copy[copy.Length - 1]) * 0.5f;
                Vector3 tangent = new Vector3(-(mid.z - center.z), 0, mid.x - center.x);
                Array.Sort(copy, (a, b) => Vector3.Dot(a, tangent).CompareTo(Vector3.Dot(b, tangent)));
                ports.Add(copy);
            }
            ports.Sort((a, b) => Angle(Mid(a) - center).CompareTo(Angle(Mid(b) - center)));
            var boundary = new List<Vector3>();
            for (int i = 0; i < ports.Count; i++)
            {
                var port = ports[i]; var next = ports[(i + 1) % ports.Count];
                boundary.AddRange(port);
                Vector3 a = port[port.Length - 1], b = next[0];
                Vector3 control = (a + b) * 0.5f;
                Vector3 d1 = Mid(port) - center, d2 = Mid(next) - center;
                d1.y = d2.y = 0;
                float det = Cross(d1, d2);
                if (Mathf.Abs(det) > 0.001f)
                {
                    Vector3 intersection = a + d1 * (Cross(b - a, d2) / det);
                    intersection.y = (a.y + b.y) * 0.5f;
                    // Reject mouths before the inside corner; their road strips would overlap.
                    if (Vector3.Dot(intersection - a, d1) > 0.001f || Vector3.Dot(intersection - b, d2) > 0.001f)
                        throw new ArgumentException("Approaches overlap. Increase Cutback or straighten the road ends.");
                    control = Vector3.Lerp(control, intersection, rounding);
                }
                for (int j = 1; j < cornerSegments; j++)
                {
                    float t = (float)j / cornerSegments;
                    boundary.Add((1 - t) * (1 - t) * a + 2 * (1 - t) * t * control + t * t * b);
                }
            }
            for (int i = 0; i < boundary.Count; i++)
                if (Cross(boundary[i] - center, boundary[(i + 1) % boundary.Count] - center) <= 0.00001f)
                    throw new ArgumentException("Intersection folds or its center is outside the boundary. Use outward-facing T/X approaches and increase Cutback.");
            int n = boundary.Count;
            var data = new RoadMeshData
            {
                vertices = new Vector3[1 + n * rings], paintUV = new Vector2[1 + n * rings],
                detailUV = new Vector2[1 + n * rings], markingUV = new Vector2[1 + n * rings],
                triangles = new int[n * 3 + (rings - 1) * n * 6]
            };
            data.vertices[0] = Project(center, project, lift);
            for (int r = 1; r <= rings; r++)
            for (int i = 0; i < n; i++)
            {
                // Exact boundary matches the approach mesh, including intermediate cross-section vertices.
                Vector3 p = Vector3.Lerp(center, boundary[i], (float)r / rings);
                data.vertices[1 + (r - 1) * n + i] = r == rings ? boundary[i] : Project(p, project, lift);
            }
            // Curved connectors have no approach counterpart and must follow the ground too.
            if (project != null)
            {
                var preserved = new HashSet<Vector3>();
                foreach (var port in ports) foreach (var p in port) preserved.Add(p);
                for (int i = 0; i < n; i++)
                    if (!preserved.Contains(boundary[i])) data.vertices[1 + (rings - 1) * n + i] = Project(boundary[i], project, lift);
            }
            int k = 0;
            for (int i = 0; i < n; i++)
            { data.triangles[k++] = 0; data.triangles[k++] = 1 + (i + 1) % n; data.triangles[k++] = 1 + i; }
            for (int r = 1; r < rings; r++)
            for (int i = 0; i < n; i++)
            {
                int a = 1 + (r - 1) * n + i, b = 1 + (r - 1) * n + (i + 1) % n;
                int c = a + n, d = b + n;
                data.triangles[k++] = a; data.triangles[k++] = b; data.triangles[k++] = c;
                data.triangles[k++] = c; data.triangles[k++] = b; data.triangles[k++] = d;
            }
            Vector3 min = boundary[0], max = min;
            foreach (var p in boundary) { min = Vector3.Min(min, p); max = Vector3.Max(max, p); }
            for (int i = 0; i < data.vertices.Length; i++)
            {
                var p = data.vertices[i];
                data.paintUV[i] = new Vector2((p.x - min.x) / (max.x - min.x), (p.z - min.z) / (max.z - min.z));
                data.detailUV[i] = new Vector2(p.x / tileSize, p.z / tileSize);
                data.markingUV[i] = data.paintUV[i];
            }
            data.length = Mathf.Max(max.x - min.x, max.z - min.z);
            return data;
        }

        static Vector3 Project(Vector3 p, RoadSurfaceProjector project, float lift)
        {
            if (project == null) return p;
            if (!project(p, out var ground)) throw new InvalidOperationException("Ground is missing below the intersection.");
            return ground + Vector3.up * lift;
        }
        static T[] Slice<T>(T[] source, int first, int count)
        { var result = new T[count]; Array.Copy(source, first, result, 0, count); return result; }
        static Vector3 Center(RoadMeshData data, int row, int stride) => (data.vertices[row * stride] + data.vertices[row * stride + stride - 1]) * 0.5f;
        static Vector3 Mid(Vector3[] p) => (p[0] + p[p.Length - 1]) * 0.5f;
        static float Angle(Vector3 p) => Mathf.Atan2(p.z, p.x);
        static float Cross(Vector3 a, Vector3 b) => a.x * b.z - a.z * b.x;
        static bool Finite(float v) => !float.IsNaN(v) && !float.IsInfinity(v);
    }
}