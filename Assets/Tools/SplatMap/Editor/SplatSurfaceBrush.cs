using System;
using UnityEngine;

namespace ExtractionRaid.Editor.SplatMap
{
    // Cached world geometry; UVs only address texels, never determine the brush radius.
    public sealed class SplatSurfaceBrush
    {
        struct Triangle
        {
            public bool valid;
            public Vector3 origin, edge1, edge2, normal, min, max, gradientU, gradientV;
            public Vector2 uv, uvEdge1, uvEdge2, uvMin, uvMax;
            public float inverseUVDeterminant;
        }

        readonly Triangle[] triangles;
        readonly int size;
        readonly float[] coverage;
        readonly int[] touched;

        public SplatSurfaceBrush(Vector3[] worldVertices, Vector2[] uv, int[] indices, int size)
        {
            if (worldVertices == null || uv == null || indices == null || uv.Length != worldVertices.Length || indices.Length % 3 != 0 || size < 1)
                throw new ArgumentException("Invalid surface brush geometry or map size.");
            this.size = size;
            coverage = new float[size * size];
            touched = new int[coverage.Length];
            triangles = new Triangle[indices.Length / 3];
            for (int i = 0; i < triangles.Length; i++)
            {
                int a = indices[i * 3], b = indices[i * 3 + 1], c = indices[i * 3 + 2];
                var t = new Triangle
                {
                    origin = worldVertices[a], edge1 = worldVertices[b] - worldVertices[a], edge2 = worldVertices[c] - worldVertices[a],
                    uv = uv[a], uvEdge1 = uv[b] - uv[a], uvEdge2 = uv[c] - uv[a],
                    min = Vector3.Min(worldVertices[a], Vector3.Min(worldVertices[b], worldVertices[c])),
                    max = Vector3.Max(worldVertices[a], Vector3.Max(worldVertices[b], worldVertices[c])),
                    uvMin = Vector2.Min(uv[a], Vector2.Min(uv[b], uv[c])),
                    uvMax = Vector2.Max(uv[a], Vector2.Max(uv[b], uv[c]))
                };
                float uvDet = t.uvEdge1.x * t.uvEdge2.y - t.uvEdge1.y * t.uvEdge2.x;
                float aa = Vector3.Dot(t.edge1, t.edge1), ab = Vector3.Dot(t.edge1, t.edge2), bb = Vector3.Dot(t.edge2, t.edge2);
                float worldDet = Vector3.Cross(t.edge1, t.edge2).sqrMagnitude;
                if (Mathf.Abs(uvDet) < 1e-12f || worldDet < 1e-16f) continue;
                Vector3 gradientB = (bb * t.edge1 - ab * t.edge2) / worldDet;
                Vector3 gradientC = (aa * t.edge2 - ab * t.edge1) / worldDet;
                t.gradientU = t.uvEdge1.x * gradientB + t.uvEdge2.x * gradientC;
                t.gradientV = t.uvEdge1.y * gradientB + t.uvEdge2.y * gradientC;
                t.normal = Vector3.Cross(t.edge1, t.edge2).normalized;
                t.inverseUVDeterminant = 1 / uvDet;
                t.valid = true;
                triangles[i] = t;
            }
        }

        public bool Stamp(Color[] pixels, Vector3 center, Vector3 normal, float radius, float strength,
            bool soft, int channel, bool mask = false, float visibility = 1)
        {
            if (pixels == null || pixels.Length != coverage.Length) throw new ArgumentException("Map size does not match brush cache.");
            if (radius <= 0 || float.IsNaN(radius) || float.IsInfinity(radius)) return false;
            strength = Mathf.Clamp01(strength);
            if (strength <= 0) return false;
            float radiusSquared = radius * radius;
            int count = 0;
            foreach (Triangle t in triangles)
            {
                if (!t.valid || Vector3.Dot(t.normal, normal) <= 0) continue;
                Vector3 nearest = new Vector3(Mathf.Clamp(center.x, t.min.x, t.max.x), Mathf.Clamp(center.y, t.min.y, t.max.y), Mathf.Clamp(center.z, t.min.z, t.max.z));
                if ((nearest - center).sqrMagnitude > radiusSquared) continue;
                Vector3 delta = center - t.origin;
                Vector2 centerUV = t.uv + new Vector2(Vector3.Dot(t.gradientU, delta), Vector3.Dot(t.gradientV, delta));
                Vector2 extent = new Vector2(t.gradientU.magnitude, t.gradientV.magnitude) * radius;
                Vector2 minUV = Vector2.Max(t.uvMin, centerUV - extent), maxUV = Vector2.Min(t.uvMax, centerUV + extent);
                int minX = Mathf.Max(0, Mathf.CeilToInt(minUV.x * size - 0.5f));
                int maxX = Mathf.Min(size - 1, Mathf.FloorToInt(maxUV.x * size - 0.5f));
                int minY = Mathf.Max(0, Mathf.CeilToInt(minUV.y * size - 0.5f));
                int maxY = Mathf.Min(size - 1, Mathf.FloorToInt(maxUV.y * size - 0.5f));
                for (int y = minY; y <= maxY; y++)
                for (int x = minX; x <= maxX; x++)
                {
                    Vector2 offset = new Vector2((x + 0.5f) / size, (y + 0.5f) / size) - t.uv;
                    float b = (offset.x * t.uvEdge2.y - offset.y * t.uvEdge2.x) * t.inverseUVDeterminant;
                    float c = (t.uvEdge1.x * offset.y - t.uvEdge1.y * offset.x) * t.inverseUVDeterminant;
                    if (b < -0.00001f || c < -0.00001f || b + c > 1.00001f) continue;
                    Vector3 point = t.origin + b * t.edge1 + c * t.edge2;
                    float distanceSquared = (point - center).sqrMagnitude;
                    if (distanceSquared > radiusSquared) continue;
                    float amount = strength * (soft ? 1 - Mathf.SmoothStep(0, 1, Mathf.Sqrt(distanceSquared) / radius) : 1);
                    if (amount <= 0) continue;
                    int index = y * size + x;
                    // Shared triangle edges and UV overlaps receive at most one application per stamp.
                    if (coverage[index] == 0) touched[count++] = index;
                    coverage[index] = Mathf.Max(coverage[index], amount);
                }
            }
            for (int i = 0; i < count; i++)
            {
                int index = touched[i];
                pixels[index] = mask ? SplatBrush.BlendMask(pixels[index], visibility, coverage[index])
                    : SplatBrush.Blend(pixels[index], channel, coverage[index]);
                coverage[index] = 0;
            }
            return count > 0;
        }
    }
}
