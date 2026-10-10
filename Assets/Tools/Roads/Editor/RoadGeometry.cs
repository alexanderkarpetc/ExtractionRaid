using System;
using System.Collections.Generic;
using UnityEngine;

namespace ExtractionRaid.Editor.Roads
{
    public delegate bool RoadSurfaceProjector(Vector3 position, out Vector3 projected);

    public sealed class RoadMeshData
    {
        public Vector3[] vertices;
        public Vector2[] paintUV;
        public Vector2[] detailUV;
        public Vector2[] markingUV;
        public int[] triangles;
        public float length;
        public Vector2[] exposedEdgePairs;
    }

    public static class RoadGeometry
    {
        public const int MaxRows = 8192;

        public static List<Vector3> SamplePath(IReadOnlyList<Vector3> points, float spacing, bool smooth)
        {
            if (points == null || points.Count < 2 || points.Count > 256)
                throw new ArgumentException("Use between 2 and 256 control points.");
            if (!Finite(spacing) || spacing < 0.05f) throw new ArgumentException("Sample spacing must be at least 0.05 metres.");
            for (int i = 0; i < points.Count; i++)
            {
                Vector3 p = points[i];
                if (!Finite(p.x) || !Finite(p.y) || !Finite(p.z)) throw new ArgumentException("Control points must be finite.");
                if (i > 0 && new Vector2(p.x - points[i - 1].x, p.z - points[i - 1].z).sqrMagnitude < 0.0001f)
                    throw new ArgumentException("Consecutive points must be separated horizontally by at least 0.01 metres.");
            }
            var dense = new List<Vector3> { points[0] };
            for (int i = 0; i < points.Count - 1; i++)
            {
                if (!smooth || points.Count == 2) { dense.Add(points[i + 1]); continue; }
                Vector3 p1 = points[i], p2 = points[i + 1];
                Vector3 p0 = i > 0 ? points[i - 1] : 2 * p1 - p2;
                Vector3 p3 = i + 2 < points.Count ? points[i + 2] : 2 * p2 - p1;
                int subdivisions = Mathf.Clamp(Mathf.CeilToInt(Vector3.Distance(p1, p2) / spacing * 4), 8, 256);
                for (int j = 1; j <= subdivisions; j++) dense.Add(CatmullRom(p0, p1, p2, p3, (float)j / subdivisions));
            }
            float length = 0;
            for (int i = 1; i < dense.Count; i++) length += Vector3.Distance(dense[i - 1], dense[i]);
            if (!Finite(length) || length < 0.01f) throw new ArgumentException("The road has no usable length.");
            int segments = Mathf.CeilToInt(length / spacing);
            if (segments >= MaxRows) throw new ArgumentException("The road has too many samples. Increase Sample Spacing or split the road.");
            var result = new List<Vector3>(segments + 1) { dense[0] };
            int edge = 1;
            float edgeStart = 0;
            float edgeLength = Vector3.Distance(dense[0], dense[1]);
            for (int i = 1; i < segments; i++)
            {
                float distance = length * i / segments;
                while (edge < dense.Count - 1 && edgeStart + edgeLength < distance)
                {
                    edgeStart += edgeLength;
                    edge++;
                    edgeLength = Vector3.Distance(dense[edge - 1], dense[edge]);
                }
                result.Add(Vector3.Lerp(dense[edge - 1], dense[edge], (distance - edgeStart) / Mathf.Max(edgeLength, 0.000001f)));
            }
            result.Add(points[points.Count - 1]);
            return result;
        }

        public static RoadMeshData Build(IReadOnlyList<Vector3> points, float width, float spacing, bool smooth,
            int widthSegments, float tileSize, float lift, RoadSurfaceProjector project = null, float lateralOffset = 0)
        {
            if (!Finite(width) || width < 0.1f || !Finite(tileSize) || tileSize < 0.1f || !Finite(lift) || lift < 0)
                throw new ArgumentException("Width and Texture Tile Size must be at least 0.1 metres; Surface Lift must be non-negative.");
            if (!Finite(lateralOffset)) throw new ArgumentException("Lateral Offset must be finite.");
            if (widthSegments < 1 || widthSegments > 16) throw new ArgumentException("Width Segments must be between 1 and 16.");
            List<Vector3> centers = SamplePath(points, spacing, smooth);
            if (project != null)
                for (int i = 0; i < centers.Count; i++)
                {
                    if (!project(centers[i], out Vector3 projected)) throw new InvalidOperationException("Ground is missing below the road center. Check Ground Layers and ray distance.");
                    centers[i] = projected;
                }
            var distances = new float[centers.Count];
            for (int i = 1; i < centers.Count; i++) distances[i] = distances[i - 1] + Vector3.Distance(centers[i - 1], centers[i]);
            float length = distances[distances.Length - 1];
            if (length < 0.01f) throw new ArgumentException("The projected road has no usable length.");
            int stride = widthSegments + 1;
            var data = new RoadMeshData
            {
                vertices = new Vector3[centers.Count * stride],
                paintUV = new Vector2[centers.Count * stride],
                detailUV = new Vector2[centers.Count * stride],
                markingUV = new Vector2[centers.Count * stride],
                triangles = new int[(centers.Count - 1) * widthSegments * 6],
                length = length
            };
            for (int row = 0; row < centers.Count; row++)
            {
                Vector3 tangent = centers[Mathf.Min(row + 1, centers.Count - 1)] - centers[Mathf.Max(row - 1, 0)];
                tangent.y = 0;
                if (tangent.sqrMagnitude < 0.000001f) throw new ArgumentException("The road doubles back or becomes vertical. Move its control points.");
                Vector3 right = Vector3.Cross(Vector3.up, tangent.normalized);
                for (int column = 0; column <= widthSegments; column++)
                {
                    float u = (float)column / widthSegments;
                    Vector3 vertex = centers[row] + right * (lateralOffset + (u - 0.5f) * width);
                    if (project != null)
                    {
                        if (!project(vertex, out Vector3 projected)) throw new InvalidOperationException("Ground is missing below a road edge. Narrow the road or adjust ground layers/ray distance.");
                        vertex = projected;
                    }
                    int index = row * stride + column;
                    data.vertices[index] = vertex + Vector3.up * lift;
                    data.paintUV[index] = new Vector2(u, distances[row] / length);
                    data.detailUV[index] = new Vector2(u * width / tileSize, distances[row] / tileSize);
                    data.markingUV[index] = new Vector2(u, distances[row] / tileSize);
                }
            }
            int triangle = 0;
            for (int row = 0; row < centers.Count - 1; row++)
            for (int column = 0; column < widthSegments; column++)
            {
                int a = row * stride + column, b = a + stride, c = a + 1, d = b + 1;
                data.triangles[triangle++] = a; data.triangles[triangle++] = b; data.triangles[triangle++] = c;
                data.triangles[triangle++] = c; data.triangles[triangle++] = b; data.triangles[triangle++] = d;
            }
            return data;
        }

        public static Color[] InitialMask(int size, float width, float length, float fade, bool fadeEnds)
        {
            if (size < 2 || size > 2048 || !Finite(width) || !Finite(length) || width <= 0 || length <= 0 || !Finite(fade) || fade < 0)
                throw new ArgumentException("Invalid initial mask dimensions or fade.");
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float u = (float)x / (size - 1), v = (float)y / (size - 1);
                float visibility = fade > 0 ? Mathf.SmoothStep(0, 1, Mathf.Min(u, 1 - u) * width / fade) : 1;
                if (fadeEnds && fade > 0) visibility *= Mathf.SmoothStep(0, 1, Mathf.Min(v, 1 - v) * length / fade);
                pixels[y * size + x] = new Color(visibility, visibility, visibility, 1);
            }
            return pixels;
        }

        static Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float fraction)
        {
            // Centripetal parameterization is less prone to overshoot with uneven point spacing.
            float t0 = 0, t1 = t0 + Knot(p0, p1), t2 = t1 + Knot(p1, p2), t3 = t2 + Knot(p2, p3);
            float t = Mathf.Lerp(t1, t2, fraction);
            Vector3 a1 = Interpolate(p0, p1, t0, t1, t), a2 = Interpolate(p1, p2, t1, t2, t), a3 = Interpolate(p2, p3, t2, t3, t);
            Vector3 b1 = Interpolate(a1, a2, t0, t2, t), b2 = Interpolate(a2, a3, t1, t3, t);
            return Interpolate(b1, b2, t1, t2, t);
        }
        static float Knot(Vector3 a, Vector3 b) => Mathf.Max(0.0001f, (float)Math.Sqrt(Vector3.Distance(a, b)));
        static Vector3 Interpolate(Vector3 a, Vector3 b, float start, float end, float t) => ((end - t) * a + (t - start) * b) / (end - start);
        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
