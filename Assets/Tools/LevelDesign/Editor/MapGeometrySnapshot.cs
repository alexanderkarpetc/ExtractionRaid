using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LevelDesign.Editor
{
    public sealed class MapGeometrySnapshot
    {
        public BoundsIndex interesting, cover, density;
        public readonly List<(Vector3 center, float radius)> pois = new List<(Vector3, float)>();
        public int GeometryCount { get; private set; }
        public IEnumerator Collect(MapAnalysisSettings settings)
        {
            var interestBounds = new Dictionary<int, Bounds>();
            var coverBounds = new Dictionary<int, Bounds>();
            var densityBounds = new Dictionary<int, Bounds>();
            var pending = new Stack<Transform>();
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (!scene.isLoaded) continue;
                foreach (var root in scene.GetRootGameObjects()) pending.Push(root.transform);
            }
            while (pending.Count > 0)
            {
                var transform = pending.Pop();
                if (!transform || !transform.gameObject.activeInHierarchy || transform.gameObject.hideFlags != HideFlags.None) continue;
                for (int i = 0; i < transform.childCount; i++) pending.Push(transform.GetChild(i));
                var go = transform.gameObject;
                int mask = 1 << go.layer;
                if ((mask & (settings.interestingLayerMask | settings.coverLayerMask | settings.densityLayerMask)) != 0)
                {
                    bool found = false; Bounds bounds = default;
                    foreach (var renderer in go.GetComponents<Renderer>())
                    {
                        if (!renderer.enabled) continue;
                        if (!found) bounds = renderer.bounds; else bounds.Encapsulate(renderer.bounds);
                        found = true;
                    }
                    foreach (var collider in go.GetComponents<Collider>())
                    {
                        if (!collider.enabled || collider.isTrigger) continue;
                        if (!found) bounds = collider.bounds; else bounds.Encapsulate(collider.bounds);
                        found = true;
                    }
                    if (found)
                    {
                        var owner = PrefabUtility.GetNearestPrefabInstanceRoot(go);
                        int id = (owner ? owner : go).GetInstanceID();
                        if ((mask & settings.interestingLayerMask) != 0) Add(interestBounds, id, bounds);
                        if ((mask & settings.coverLayerMask) != 0) Add(coverBounds, id, bounds);
                        if ((mask & settings.densityLayerMask) != 0) Add(densityBounds, id, bounds);
                        GeometryCount++;
                    }
                }
                yield return null;
            }
            interesting = new BoundsIndex(new List<Bounds>(interestBounds.Values)); yield return null;
            cover = new BoundsIndex(new List<Bounds>(coverBounds.Values)); yield return null;
            density = new BoundsIndex(new List<Bounds>(densityBounds.Values)); yield return null;
            foreach (var poi in settings.pois)
            {
                var obj = poi.sceneObject.Resolve();
                var go = obj as GameObject;
                if (!go && obj is Component component) go = component.gameObject;
                if (go && go.scene.IsValid() && go.activeInHierarchy)
                    pois.Add((go.transform.position, Mathf.Max(0, poi.radius)));
            }
        }
        static void Add(Dictionary<int, Bounds> values, int id, Bounds bounds)
        {
            if (values.TryGetValue(id, out var previous)) { previous.Encapsulate(bounds); values[id] = previous; }
            else values.Add(id, bounds);
        }
        public float NearestPOI(Vector3 point)
        {
            float nearest = float.PositiveInfinity;
            foreach (var poi in pois)
            {
                float dx = poi.center.x - point.x, dz = poi.center.z - point.z;
                nearest = Mathf.Min(nearest, Mathf.Max(0, Mathf.Sqrt(dx * dx + dz * dz) - poi.radius));
            }
            return nearest;
        }
    }
}
