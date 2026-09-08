using System;
using System.Collections.Generic;
using UnityEngine;

namespace LevelDesign.Editor
{
    // XZ bounding-volume tree. Distances are to geometry bounds, not object pivots.
    public sealed class BoundsIndex
    {
        sealed class Node
        {
            public Bounds bounds;
            public Node left, right;
            public bool Leaf => left == null;
        }
        readonly Node root;
        public BoundsIndex(IReadOnlyList<Bounds> source)
        {
            var values = new Bounds[source.Count];
            for (int i = 0; i < source.Count; i++) values[i] = source[i];
            root = Build(values, 0, values.Length);
        }
        static Node Build(Bounds[] values, int start, int count)
        {
            if (count == 0) return null;
            var node = new Node { bounds = values[start] };
            for (int i = start + 1; i < start + count; i++) node.bounds.Encapsulate(values[i]);
            if (count == 1) return node;
            bool x = node.bounds.size.x >= node.bounds.size.z;
            Array.Sort(values, start, count, Comparer<Bounds>.Create((a, b) =>
                (x ? a.center.x : a.center.z).CompareTo(x ? b.center.x : b.center.z)));
            int half = count / 2;
            node.left = Build(values, start, half); node.right = Build(values, start + half, count - half);
            return node;
        }
        public static float SquaredDistance(Vector3 point, Bounds bounds)
        {
            float x = Mathf.Max(bounds.min.x - point.x, 0, point.x - bounds.max.x);
            float z = Mathf.Max(bounds.min.z - point.z, 0, point.z - bounds.max.z);
            return x * x + z * z;
        }
        public float Nearest(Vector3 point)
        {
            float best = float.PositiveInfinity;
            Search(root, point, ref best);
            return Mathf.Sqrt(best);
        }
        static void Search(Node node, Vector3 point, ref float best)
        {
            if (node == null || SquaredDistance(point, node.bounds) >= best) return;
            if (node.Leaf) { best = SquaredDistance(point, node.bounds); return; }
            var first = node.left; var second = node.right;
            if (SquaredDistance(point, second.bounds) < SquaredDistance(point, first.bounds))
            { first = node.right; second = node.left; }
            Search(first, point, ref best); Search(second, point, ref best);
        }
        public int CountWithin(Vector3 point, float radius) => Count(root, point, radius * radius);
        static int Count(Node node, Vector3 point, float radiusSquared)
        {
            if (node == null || SquaredDistance(point, node.bounds) > radiusSquared) return 0;
            return node.Leaf ? 1 : Count(node.left, point, radiusSquared) + Count(node.right, point, radiusSquared);
        }
    }
}
