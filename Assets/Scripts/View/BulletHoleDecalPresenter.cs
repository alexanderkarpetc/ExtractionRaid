using System.Collections.Generic;
using Adapters;
using Dev;
using Session;
using UnityEngine;

namespace View
{
    /// <summary>
    /// Gunplay A.8 — listens до <see cref="RaidEventType.ProjectileHit"/> and projects
    /// bullet hole decals at surface impact points. Reuses <see cref="DecalProjectorPool"/>
    /// (kind=3 для bullet holes — distinct з floor/wall blood pools).
    ///
    /// Skips character hits — those covered by blood decal pipeline. Uses event's `Direction`
    /// field as surface normal (zero = character hit, signal to skip).
    /// </summary>
    public class BulletHoleDecalPresenter
    {
        const int BulletHoleKind = 3;

        readonly DecalProjectorPool _pool = new();

        // Per-collider proxy throttle. Без direct collider EId, we approximate using
        // hit position rounded to ~10cm bucket — auto fire on same surface won't stack.
        readonly Dictionary<Vector3Int, float> _lastHitByBucket = new();

        GameObject[] _prefabs;
        bool _prefabsLoaded;

        public DecalProjectorPool Pool => _pool;

        public void LateTick(RaidSession session)
        {
            if (session == null) return;
            var cfg = ViewCheats.Config?.BulletHole;
            if (cfg == null || !cfg.Enabled) return;

            EnsurePrefabsLoaded();
            _pool.SetCapacity(BulletHoleKind, cfg.MaxActive);

            foreach (var e in session.ConsumeEvents().All)
            {
                if (e.Type != RaidEventType.ProjectileHit) continue;
                ProcessHit(e, cfg);
            }

            _pool.Tick();
        }

        void ProcessHit(RaidEvent e, ViewCheatsBulletHoleSection cfg)
        {
            // Skip character hits. ProjectileHit event packs:
            //   Position  = hitPoint
            //   Direction = surface normal (Vector3.zero для character hits — DamageSystem passes zero)
            //   StringPayload = hitType ("body:0.45", "head:0.00", "surface", тощо)
            if (e.Direction.sqrMagnitude < 0.0001f) return;
            if (!string.IsNullOrEmpty(e.StringPayload)
                && (e.StringPayload.StartsWith("body") || e.StringPayload.StartsWith("head")))
                return;

            // Spawn-chance gate.
            if (cfg.SpawnChance < 1f && Random.value > cfg.SpawnChance) return;

            // Per-bucket throttle — quantize hit position до 10cm grid, prevent same-spot stacking.
            var bucket = new Vector3Int(
                Mathf.RoundToInt(e.Position.x * 10f),
                Mathf.RoundToInt(e.Position.y * 10f),
                Mathf.RoundToInt(e.Position.z * 10f));
            float now = Time.unscaledTime;
            if (_lastHitByBucket.TryGetValue(bucket, out var last)
                && now - last < cfg.MinTimeBetweenSameSurface)
                return;
            _lastHitByBucket[bucket] = now;

            // Mesh is flat у XZ plane (Y is normal axis) — same orientation as floor blood pools.
            // Step 1: align local Y з surface normal. Step 2: spin around normal for variety.
            var normal = e.Direction.normalized;
            var alignToNormal = Quaternion.FromToRotation(Vector3.up, normal);
            var spin          = Quaternion.AngleAxis(Random.Range(0f, 360f), normal);
            var rotation      = spin * alignToNormal;

            // Random offset along surface plane breaks "trail line" cluster from top-down camera
            // angle (всі shots land on same height → holes form a perfect horizontal track).
            // Project world up onto surface plane = "up" along ramp/wall.
            var pos = e.Position + normal * cfg.SurfaceOffset
                                  + ComputeSurfaceJitter(normal, cfg.SurfaceUpJitter, cfg.SurfaceRightJitter);

            float scale = Random.Range(cfg.ScaleMin, cfg.ScaleMax);

            _pool.Spawn(BulletHoleKind, _prefabs, pos, rotation, cfg.Lifetime, scale);
        }

        // Random offset along surface plane (perpendicular to normal). Vertical-biased: shots
        // у top-down cluster horizontally, треба здебільшого scatter Y. Returns Vector3.zero
        // якщо surface is flat (horizontal floor) — no clustering issue there.
        static Vector3 ComputeSurfaceJitter(Vector3 surfaceNormal, float upJitter, float rightJitter)
        {
            // Up axis projected onto surface plane.
            var planeUp = Vector3.up - Vector3.Dot(Vector3.up, surfaceNormal) * surfaceNormal;
            if (planeUp.sqrMagnitude < 0.01f) return Vector3.zero;
            planeUp.Normalize();
            var planeRight = Vector3.Cross(surfaceNormal, planeUp).normalized;

            return planeUp    * Random.Range(-upJitter,    upJitter)
                 + planeRight * Random.Range(-rightJitter, rightJitter);
        }

        void EnsurePrefabsLoaded()
        {
            if (_prefabsLoaded) return;
            _prefabsLoaded = true;

            // Authored prefabs з PolygonApocalypse_Material_01_A + texture. Variants 01-03 are
            // single-hole — match our 1-per-shot logic. Variants 04+ are multi-hole "spray"
            // patterns — visually wrong для individual hits (excluded).
            var prefabs = new List<GameObject>();
            for (int i = 1; i <= 3; i++)
            {
                var prefab = Resources.Load<GameObject>($"PolygonApocalypse/Prefabs/Props/SM_Prop_BulletHoles_{i:D2}");
                if (prefab != null) prefabs.Add(prefab);
            }
            _prefabs = prefabs.ToArray();
        }

        public void Dispose()
        {
            _pool.ClearAll();
            _lastHitByBucket.Clear();
        }
    }
}
