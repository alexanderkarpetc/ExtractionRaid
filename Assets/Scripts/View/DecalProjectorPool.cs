using System.Collections.Generic;
using UnityEngine;

namespace View
{
    /// <summary>
    /// Generic decal pool — used by Gunplay A.4 (blood decals), A.8 (bullet holes),
    /// A.10 (blood pools под bodies), B.6 (bleeding floor trail). Holds bounded sets
    /// of active GameObjects per category; oldest-replaces when over capacity. Per-decal
    /// lifetime drives auto-cleanup; last 30% of lifetime fades alpha (when supported).
    ///
    /// Pool itself is shared (single instance) but capacities + prefab pools are
    /// configured per category by callers. Decal "kind" is just an integer key —
    /// callers define their own enum and pass int values.
    /// </summary>
    public class DecalProjectorPool
    {
        struct ActiveDecal
        {
            public GameObject Go;
            public Transform  Transform;
            public Vector3    InitialScale;
            public float      SpawnedUnscaled;
            public float      Lifetime;
        }

        readonly Dictionary<int, Queue<ActiveDecal>> _byKind = new();
        readonly Dictionary<int, int>               _capacityByKind = new();
        Transform _root;

        /// <summary>Sets capacity for a kind. Default 0 → kind не initialized; first SetCapacity creates queue.</summary>
        public void SetCapacity(int kind, int capacity)
        {
            _capacityByKind[kind] = Mathf.Max(1, capacity);
            if (!_byKind.TryGetValue(kind, out _))
                _byKind[kind] = new Queue<ActiveDecal>(capacity);
        }

        /// <summary>
        /// Spawn a decal at <paramref name="position"/> with the supplied <paramref name="rotation"/>.
        /// Caller is responsible for computing rotation так, щоб mesh orientation matched
        /// surface normal — pool itself stays generic (works for floor / wall / any axis).
        /// </summary>
        public GameObject Spawn(int kind, GameObject[] prefabs, Vector3 position, Quaternion rotation,
            float lifetime, float scale = 1f)
        {
            if (prefabs == null || prefabs.Length == 0) return null;

            EnsureRoot();
            EnsureKind(kind);

            var queue = _byKind[kind];
            int capacity = _capacityByKind[kind];

            // Evict oldest якщо overcapacity (LRU-style — оptimistically queue is FIFO).
            while (queue.Count >= capacity)
            {
                var old = queue.Dequeue();
                if (old.Go != null) Object.Destroy(old.Go);
            }

            int idx = Random.Range(0, prefabs.Length);
            var prefab = prefabs[idx];
            if (prefab == null) return null;

            var go = Object.Instantiate(prefab, position, rotation, _root);
            var initialScale = prefab.transform.localScale * scale;
            go.transform.localScale = initialScale;

            // Strip colliders just у case (decals shouldn't block raycasts/agents).
            foreach (var col in go.GetComponentsInChildren<Collider>(true))
                col.enabled = false;

            var entry = new ActiveDecal
            {
                Go              = go,
                Transform       = go.transform,
                InitialScale    = initialScale,
                SpawnedUnscaled = Time.unscaledTime,
                Lifetime        = Mathf.Max(0.1f, lifetime),
            };
            queue.Enqueue(entry);
            return go;
        }

        /// <summary>
        /// Tick all decals — called each frame by owner (e.g. App.LateTick). Fades + cleans up
        /// expired. Uses Time.unscaledTime so decals don't pause during hit pause.
        ///
        /// Fade method: scale shrink toward 0 у last 30% of lifetime. Works on будь-якому
        /// shader (no transparency requirement) — universal across opaque + transparent
        /// materials. Decal "evaporates" naturally як diminishing puddle.
        /// </summary>
        public void Tick()
        {
            float now = Time.unscaledTime;
            foreach (var kv in _byKind)
            {
                var queue = kv.Value;
                int count = queue.Count;
                for (int i = 0; i < count; i++)
                {
                    var d = queue.Dequeue();
                    if (d.Go == null) continue;

                    float age = now - d.SpawnedUnscaled;
                    if (age >= d.Lifetime)
                    {
                        Object.Destroy(d.Go);
                        continue;
                    }

                    // Scale shrink у last 30% of lifetime — decal "evaporates."
                    float fadeStart = d.Lifetime * 0.7f;
                    if (age >= fadeStart && d.Transform != null)
                    {
                        float fadeT = (age - fadeStart) / (d.Lifetime - fadeStart);
                        // Ease-out cubic — feels gentle near end, hides pop.
                        float k = 1f - fadeT;
                        float scaleK = k * k * k;
                        d.Transform.localScale = d.InitialScale * scaleK;
                    }

                    queue.Enqueue(d);
                }
            }
        }

        public void ClearAll()
        {
            foreach (var queue in _byKind.Values)
            {
                while (queue.Count > 0)
                {
                    var d = queue.Dequeue();
                    if (d.Go != null) Object.Destroy(d.Go);
                }
            }
        }

        void EnsureRoot()
        {
            if (_root != null) return;
            var go = new GameObject("[DecalPool]");
            _root = go.transform;
        }

        void EnsureKind(int kind)
        {
            if (!_byKind.ContainsKey(kind))
                _byKind[kind] = new Queue<ActiveDecal>(_capacityByKind.GetValueOrDefault(kind, 32));
            if (!_capacityByKind.ContainsKey(kind))
                _capacityByKind[kind] = 32; // sane default — caller повинен SetCapacity явно
        }
    }
}
