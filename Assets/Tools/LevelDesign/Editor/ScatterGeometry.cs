using System;
using System.Collections.Generic;
using UnityEngine;

namespace LevelDesign.Editor
{
    public static class ScatterGeometry
    {
        public static float Range(System.Random random, float min, float max) =>
            Mathf.Lerp(Mathf.Min(min, max), Mathf.Max(min, max), (float)random.NextDouble());

        public static int WeightedIndex(IReadOnlyList<float> weights, System.Random random)
        {
            double total = 0;
            for (int i = 0; i < weights.Count; i++) if (weights[i] > 0) total += weights[i];
            if (total <= 0 || double.IsInfinity(total)) return -1;
            double pick = random.NextDouble() * total;
            for (int i = 0; i < weights.Count; i++)
                if (weights[i] > 0 && (pick -= weights[i]) < 0) return i;
            return -1;
        }

        public static Bounds TransformBounds(Bounds bounds, Matrix4x4 matrix)
        {
            var center = matrix.MultiplyPoint3x4(bounds.center);
            var x = matrix.MultiplyVector(new Vector3(bounds.extents.x, 0, 0));
            var y = matrix.MultiplyVector(new Vector3(0, bounds.extents.y, 0));
            var z = matrix.MultiplyVector(new Vector3(0, 0, bounds.extents.z));
            return new Bounds(center, 2 * new Vector3(Mathf.Abs(x.x) + Mathf.Abs(y.x) + Mathf.Abs(z.x),
                Mathf.Abs(x.y) + Mathf.Abs(y.y) + Mathf.Abs(z.y), Mathf.Abs(x.z) + Mathf.Abs(y.z) + Mathf.Abs(z.z)));
        }

        public static bool TryPrefabBounds(GameObject prefab, out Bounds bounds)
        {
            bounds = default;
            bool found = false;
            foreach (var renderer in prefab.GetComponentsInChildren<Renderer>())
            {
                if (!renderer.enabled || !renderer.gameObject.activeSelf) continue;
                var local = TransformBounds(renderer.localBounds,
                    prefab.transform.worldToLocalMatrix * renderer.transform.localToWorldMatrix);
                if (!found) bounds = local; else bounds.Encapsulate(local);
                found = true;
            }
            return found && bounds.size.sqrMagnitude > .000001f;
        }
    }

    // The distance hash uses the largest requested separation; only nine cells are queried.
    public sealed class ScatterPlacementValidator
    {
        readonly float cellSize;
        readonly Dictionary<Vector2Int, List<(Vector3 point, float distance)>> cells =
            new Dictionary<Vector2Int, List<(Vector3, float)>>();
        readonly List<Bounds> occupied = new List<Bounds>();
        public ScatterPlacementValidator(float maxDistance) { cellSize = Mathf.Max(.1f, maxDistance); }
        Vector2Int Cell(Vector3 point) => new Vector2Int(Mathf.FloorToInt(point.x / cellSize), Mathf.FloorToInt(point.z / cellSize));
        public bool Separated(Vector3 point, float distance)
        {
            var cell = Cell(point);
            for (int x = -1; x <= 1; x++) for (int z = -1; z <= 1; z++)
                if (cells.TryGetValue(cell + new Vector2Int(x, z), out var values))
                    foreach (var previous in values)
                    {
                        float dx = point.x - previous.point.x, dz = point.z - previous.point.z;
                        float limit = Mathf.Max(distance, previous.distance);
                        if (dx * dx + dz * dz < limit * limit) return false;
                    }
            return true;
        }
        public bool Overlaps(Bounds bounds)
        {
            foreach (var other in occupied) if (bounds.Intersects(other)) return true;
            return false;
        }
        public void Add(Vector3 point, float distance, Bounds bounds)
        {
            var cell = Cell(point);
            if (!cells.TryGetValue(cell, out var values)) cells[cell] = values = new List<(Vector3, float)>();
            values.Add((point, distance)); occupied.Add(bounds);
        }
    }
}
