using System.Collections.Generic;
using Adapters;
using Dev;
using Session;
using State;
using UnityEngine;

namespace View
{
    /// <summary>
    /// Gunplay A.4 — listens до EntityHit and projects blood decals on floor (under hit
    /// point) and optionally wall (behind penetrated targets). Reuses
    /// <see cref="DecalProjectorPool"/> для bounded active decal count.
    ///
    /// Throttle policy: per-target time gate + per-hit spawn chance — prevents auto fire
    /// от засіяння area з overlapping decals (visual mush). Tunable in ViewCheats.
    /// </summary>
    public class BloodDecalPresenter
    {
        const int FloorKind = 1;
        const int WallKind  = 2;

        readonly DecalProjectorPool _pool = new();
        readonly Dictionary<EId, float> _lastDecalUnscaled = new();

        GameObject[] _floorPrefabs;
        GameObject[] _wallPrefabs;
        bool _prefabsLoaded;

        // Reusable RaycastHit buffer to avoid GC.
        readonly RaycastHit[] _hitBuffer = new RaycastHit[8];

        // Decals stick to environment (Default + Water + unnamed worldspace layers) only.
        // Layer convention: see LayerUtils.cs.
        static readonly int DecalRaycastLayerMask =
            ~((1 << 1) // TransparentFX
              | (1 << LayerUtils.IgnoreRaycast)
              | (1 << LayerUtils.UI)
              | (1 << LayerUtils.Player)
              | (1 << LayerUtils.Bot)
              | (1 << LayerUtils.FOV));

        public DecalProjectorPool Pool => _pool;

        public void LateTick(RaidSession session)
        {
            if (session == null) return;
            var cfg = ViewCheats.Config?.BloodDecal;
            if (cfg == null || !cfg.Enabled) return;

            EnsurePrefabsLoaded();
            EnsureCapacities(cfg);

            foreach (var e in session.ConsumeEvents().All)
            {
                if (e.Type != RaidEventType.EntityHit) continue;
                ProcessHit(e, cfg, session.RaidState);
            }

            _pool.Tick();
        }

        void ProcessHit(RaidEvent e, ViewCheatsBloodDecalSection cfg, RaidState state)
        {
            // EntityHit packing (per RaidEventBuffer.EntityHit):
            //   Id           = targetEid
            //   Position     = hitPoint
            //   Direction    = projectileDirection
            //   Damage       = absorptionRatio
            //   CurrentHp    = isHeadshot ? 1 : 0
            //   MaxHp        = isKill     ? 1 : 0
            //   KillerId.V   = isRicochet ? 1 : 0
            bool isRicochet = e.KillerId.Value == 1;
            if (isRicochet) return; // armor deflected — no blood

            // A2 — laser cauterizes, no blood. Toggle тримається у ImpactVfx section.
            var impactCfg = Dev.ViewCheats.Config?.ImpactVfx;
            if (e.Archetype == PayloadArchetypeKey.Laser
                && impactCfg != null && impactCfg.Enabled && impactCfg.SuppressBloodDecalForLaser)
                return;

            float absorption = e.Damage;
            float penetrationFraction = 1f - absorption;
            if (penetrationFraction < cfg.MinPenetrationFraction) return;

            // Per-target throttle.
            float now = Time.unscaledTime;
            if (_lastDecalUnscaled.TryGetValue(e.Id, out var last)
                && now - last < cfg.MinTimeBetweenDecalsPerTarget)
                return;

            // Spawn-chance gate (organic gaps).
            if (cfg.SpawnChance < 1f && Random.value > cfg.SpawnChance) return;

            // Resolve target's center у raid state — feet/base position. Floor decal sticks
            // to ground beneath character, не до hit point on upper body.
            if (!TryResolveTargetCenter(e.Id, state, out var center))
                return;

            // Floor decal — origin = character center + small XZ random.
            SpawnFloorDecal(center, cfg);

            // Wall splatter — raycast from original hit point along projectile direction
            // (high-up hit = splatter on wall behind upper body — авtoматично splatter higher
            // than floor decal). Wall splatter still uses hit point, не center.
            if (cfg.EnableWallSplatter && _wallPrefabs != null && _wallPrefabs.Length > 0)
                SpawnWallDecal(e.Position, e.Direction, cfg);

            _lastDecalUnscaled[e.Id] = now;
        }

        // Look up bot or player position by EId. Returns false якщо not found
        // (entity died у the same frame, etc.).
        static bool TryResolveTargetCenter(EId targetId, RaidState state, out Vector3 center)
        {
            center = default;
            if (state == null) return false;

            if (state.PlayerEntity != null && state.PlayerEntity.Id == targetId)
            {
                center = state.PlayerEntity.Position;
                return true;
            }
            for (int i = 0; i < state.Bots.Count; i++)
            {
                if (state.Bots[i].Id == targetId)
                {
                    center = state.Bots[i].Position;
                    return true;
                }
            }
            return false;
        }

        void SpawnFloorDecal(Vector3 characterCenter, ViewCheatsBloodDecalSection cfg)
        {
            if (_floorPrefabs == null || _floorPrefabs.Length == 0) return;

            // Random XZ offset around character center for organic placement.
            float r = cfg.FloorRandomRadius;
            var offset = r > 0f
                ? new Vector3(Random.Range(-r, r), 0f, Random.Range(-r, r))
                : Vector3.zero;
            var sampledCenter = characterCenter + offset;

            // Raycast down to find ground. Start above character so we always start outside
            // any collider (character height ~2m).
            var origin = sampledCenter + Vector3.up * 1.5f;
            int count = Physics.RaycastNonAlloc(origin, Vector3.down, _hitBuffer,
                cfg.FloorRaycastMaxDistance + 1.5f, DecalRaycastLayerMask, QueryTriggerInteraction.Ignore);
            var (hit, gotHit) = PickClosest(count);
            if (!gotHit) return;

            float scale = Random.Range(cfg.FloorScaleMin, cfg.FloorScaleMax);
            var pos = hit.point + hit.normal * cfg.FloorOffset;
            // Floor pool meshes are flat у XZ plane — local Y is the "up out of surface" axis.
            // Step 1 — align local Y axis with surface normal (lay mesh flat on ground).
            // Step 2 — spin around surface normal (random yaw, для variety).
            var alignToNormal = Quaternion.FromToRotation(Vector3.up, hit.normal);
            var spin          = Quaternion.AngleAxis(Random.Range(0f, 360f), hit.normal);
            var rotation      = spin * alignToNormal;
            _pool.Spawn(FloorKind, _floorPrefabs, pos, rotation, cfg.Lifetime, scale);
        }

        void SpawnWallDecal(Vector3 hitPoint, Vector3 projectileDir, ViewCheatsBloodDecalSection cfg)
        {
            if (projectileDir.sqrMagnitude < 0.0001f) return;

            int count = Physics.RaycastNonAlloc(hitPoint, projectileDir.normalized,
                _hitBuffer, cfg.WallRaycastMaxDistance, DecalRaycastLayerMask, QueryTriggerInteraction.Ignore);
            var (hit, gotHit) = PickClosest(count);
            if (!gotHit) return;
            // Skip if surface is roughly horizontal (it's floor, not wall).
            if (Mathf.Abs(Vector3.Dot(hit.normal, Vector3.up)) > 0.7f) return;

            float scale = Random.Range(cfg.WallScaleMin, cfg.WallScaleMax);
            // Random offset along wall plane breaks "horizontal track" cluster from top-down camera angle.
            var pos = hit.point + hit.normal * cfg.WallOffset
                                 + ComputeSurfaceJitter(hit.normal, cfg.WallUpJitter, cfg.WallRightJitter);
            // Wall splat mesh is flat у XY plane (Z=0.006) — local Z is the surface-normal axis.
            // Step 1 — align local Z з wall normal so mesh hugs wall.
            // Step 2 — spin around wall normal for variety.
            var alignToNormal = Quaternion.LookRotation(hit.normal, Vector3.up);
            var spin          = Quaternion.AngleAxis(Random.Range(0f, 360f), hit.normal);
            var rotation      = spin * alignToNormal;
            _pool.Spawn(WallKind, _wallPrefabs, pos, rotation, cfg.Lifetime, scale);
        }

        // Random offset along surface plane. Vertical-biased для top-down camera —
        // shots cluster horizontally, vertical jitter breaks trail line.
        static Vector3 ComputeSurfaceJitter(Vector3 surfaceNormal, float upJitter, float rightJitter)
        {
            var planeUp = Vector3.up - Vector3.Dot(Vector3.up, surfaceNormal) * surfaceNormal;
            if (planeUp.sqrMagnitude < 0.01f) return Vector3.zero;
            planeUp.Normalize();
            var planeRight = Vector3.Cross(surfaceNormal, planeUp).normalized;
            return planeUp    * Random.Range(-upJitter,    upJitter)
                 + planeRight * Random.Range(-rightJitter, rightJitter);
        }

        // Pick closest valid hit from buffer. Layer mask already filtered character/bot/UI
        // layers — only worldspace geometry remaining.
        (RaycastHit, bool) PickClosest(int hitCount)
        {
            float bestDist = float.MaxValue;
            RaycastHit best = default;
            bool found = false;
            for (int i = 0; i < hitCount; i++)
            {
                var h = _hitBuffer[i];
                if (h.collider == null) continue;
                if (h.distance < bestDist)
                {
                    bestDist = h.distance;
                    best = h;
                    found = true;
                }
            }
            return (best, found);
        }

        void EnsurePrefabsLoaded()
        {
            if (_prefabsLoaded) return;
            _prefabsLoaded = true;

            // Floor pool: authored prefabs у Prefabs/Props/ (not bare FBX — those import з default
            // gray material). Authored prefabs use PolygonApocalypse_Material_01_A с blood texture.
            var floor = new List<GameObject>();
            for (int i = 1; i <= 5; i++)
            {
                var prefab = Resources.Load<GameObject>($"PolygonApocalypse/Prefabs/Props/SM_Prop_BloodPool_{i:D2}");
                if (prefab != null) floor.Add(prefab);
            }
            _floorPrefabs = floor.ToArray();

            // Wall splatter: authored prefab.
            var splat = Resources.Load<GameObject>("PolygonApocalypse/Prefabs/Props/SM_Prop_BloodSplat_01");
            _wallPrefabs = splat != null ? new[] { splat } : System.Array.Empty<GameObject>();
        }

        void EnsureCapacities(ViewCheatsBloodDecalSection cfg)
        {
            _pool.SetCapacity(FloorKind, cfg.MaxActiveFloorDecals);
            _pool.SetCapacity(WallKind,  cfg.MaxActiveWallDecals);
        }

        public void Dispose()
        {
            _pool.ClearAll();
            _lastDecalUnscaled.Clear();
        }
    }
}
