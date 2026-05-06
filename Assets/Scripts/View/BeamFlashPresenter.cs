using System.Collections.Generic;
using Adapters;
using Dev;
using Session;
using UnityEngine;

namespace View
{
    /// <summary>
    /// Tau-cannon-style beam flash для Laser archetype. Listens to <see cref="RaidEventType.ProjectileSpawned"/>
    /// (per-pellet, не per-shot) + filters by archetype "Laser". На spawn spawns a one-shot
    /// LineRenderer GO that draws a jagged "electric" line from muzzle to forward raycast hit
    /// along projectile direction. So a 7-pellet laser shotgun emits 7 beams (each at the
    /// same chargeRatio). Lives for ~0.05-0.13s залежно від charge, then destroys itself.
    /// Independent visual layer — projectile entity continues to apply damage normally,
    /// beam is pure cosmetic.
    /// </summary>
    public class BeamFlashPresenter
    {
        const string MaterialPath  = "Vfx/Materials/VfxBeamFlash";
        const float  DefaultRange  = 60f;
        const int    SegmentCount  = 10;

        // Min/max ranges interpolated by chargeRatio (0..1). Quick tap = thin/short
        // pulse; full hold = thick/long-lived bright beam з пожирніший електро feel.
        const float  LifetimeMin     = 0.05f;
        const float  LifetimeMax     = 0.13f;
        const float  JaggedAmpMin    = 0.04f;
        const float  JaggedAmpMax    = 0.22f;
        const float  StartWidthMin   = 0.06f;
        const float  StartWidthMax   = 0.28f;
        const float  EndWidthMin     = 0.02f;
        const float  EndWidthMax     = 0.10f;

        readonly List<ActiveBeam> _active = new();
        Material _sharedMaterial;
        bool     _materialLoaded;

        struct ActiveBeam
        {
            public GameObject     Go;
            public LineRenderer   Line;
            public Vector3        Start;
            public Vector3        End;
            public float          ExpiresAt;
            public float          Lifetime;
            public float          StartWidth;
            public float          EndWidth;
            public float          JaggedAmp;
        }

        public void LateTick(RaidSession session)
        {
            if (session == null) return;
            EnsureMaterial();

            foreach (var e in session.ConsumeEvents().All)
            {
                if (e.Type != RaidEventType.ProjectileSpawned) continue;
                if (e.StringPayload != "Laser") continue;
                // chargeRatio packed into CurrentHp field (0..1). Damage стає raw shot
                // damage; CurrentHp reused як charge channel. Ballistic projectiles мають
                // default 1.0 — їх все одно skip'аємо через archetype filter.
                Spawn(e.Position, e.Direction, Mathf.Clamp01(e.CurrentHp));
            }

            UpdateActive();
        }

        void Spawn(Vector3 origin, Vector3 direction, float chargeRatio)
        {
            if (direction.sqrMagnitude < 0.0001f) return;
            direction = direction.normalized;

            // Forward raycast — beam ends де куля по гіпотетично попала б (or default range).
            // Uses Physics.Raycast напряму бо presenter — view-side, без context plumbing.
            float distance = DefaultRange;
            if (Physics.Raycast(origin, direction, out var hit, DefaultRange,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide))
            {
                distance = hit.distance;
            }
            Vector3 end = origin + direction * distance;

            // Scale visual params by chargeRatio: weak shot = thin/dim/short, full
            // charge = thick/bright/longer-lived з шаленою електро ламаною.
            float lifetime    = Mathf.Lerp(LifetimeMin,   LifetimeMax,   chargeRatio);
            float startWidth  = Mathf.Lerp(StartWidthMin, StartWidthMax, chargeRatio);
            float endWidth    = Mathf.Lerp(EndWidthMin,   EndWidthMax,   chargeRatio);
            float jaggedAmp   = Mathf.Lerp(JaggedAmpMin,  JaggedAmpMax,  chargeRatio);
            float colorBoost  = Mathf.Lerp(0.5f, 1.0f, chargeRatio);

            var go = new GameObject("BeamFlash");
            var line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.positionCount = SegmentCount;
            line.startWidth = startWidth;
            line.endWidth = endWidth;
            line.numCapVertices = 4;
            line.numCornerVertices = 2;
            line.alignment = LineAlignment.View;
            if (_sharedMaterial != null) line.sharedMaterial = _sharedMaterial;
            line.startColor = new Color(colorBoost, 0.2f * colorBoost, 0.2f * colorBoost, 1f);
            line.endColor   = new Color(colorBoost, 0.4f * colorBoost, 0.4f * colorBoost, 1f);

            ApplyJaggedPath(line, origin, end, jaggedAmp);

            _active.Add(new ActiveBeam
            {
                Go = go,
                Line = line,
                Start = origin,
                End = end,
                ExpiresAt = Time.unscaledTime + lifetime,
                Lifetime = lifetime,
                StartWidth = startWidth,
                EndWidth = endWidth,
                JaggedAmp = jaggedAmp,
            });
        }

        void UpdateActive()
        {
            float now = Time.unscaledTime;
            for (int i = _active.Count - 1; i >= 0; i--)
            {
                var beam = _active[i];
                if (beam.Go == null)
                {
                    _active.RemoveAt(i);
                    continue;
                }
                if (now >= beam.ExpiresAt)
                {
                    Object.Destroy(beam.Go);
                    _active.RemoveAt(i);
                    continue;
                }

                // Re-randomize jagged segments each frame for "electric flicker" feel.
                ApplyJaggedPath(beam.Line, beam.Start, beam.End, beam.JaggedAmp);

                // Linear width fade — beam thins out before disappearing.
                float t = (beam.ExpiresAt - now) / beam.Lifetime;
                beam.Line.startWidth = beam.StartWidth * t;
                beam.Line.endWidth   = beam.EndWidth * t;
            }
        }

        // Subdivide straight beam into N segments, displace each interior vertex
        // perpendicular to the beam direction by a small random amount. End vertices
        // stay locked at origin/end. Magnitude ramps zero → max → zero through the
        // length so endpoints land exactly on muzzle / target. Amplitude scaled by
        // chargeRatio externally — full charge = wider electric snake.
        static void ApplyJaggedPath(LineRenderer line, Vector3 start, Vector3 end, float amplitude)
        {
            if (line == null || line.positionCount < 2) return;
            int n = line.positionCount;
            Vector3 dir = end - start;
            float length = dir.magnitude;
            if (length < 0.001f)
            {
                for (int i = 0; i < n; i++) line.SetPosition(i, start);
                return;
            }
            dir /= length;
            // Build a stable perpendicular basis.
            Vector3 perpA = Vector3.Cross(dir, Vector3.up);
            if (perpA.sqrMagnitude < 0.001f) perpA = Vector3.Cross(dir, Vector3.right);
            perpA.Normalize();
            Vector3 perpB = Vector3.Cross(dir, perpA);

            for (int i = 0; i < n; i++)
            {
                float u = (float)i / (n - 1);
                Vector3 p = Vector3.Lerp(start, end, u);
                if (i != 0 && i != n - 1)
                {
                    // Sin envelope keeps endpoints sharp, mid-section maximally jagged.
                    float envelope = Mathf.Sin(u * Mathf.PI);
                    float jitterA = (Random.value - 0.5f) * 2f * amplitude * envelope;
                    float jitterB = (Random.value - 0.5f) * 2f * amplitude * envelope;
                    p += perpA * jitterA + perpB * jitterB;
                }
                line.SetPosition(i, p);
            }
        }

        void EnsureMaterial()
        {
            if (_materialLoaded) return;
            _materialLoaded = true;
            _sharedMaterial = Resources.Load<Material>(MaterialPath);
            if (_sharedMaterial == null)
                Debug.LogWarning($"[BeamFlashPresenter] Material not found at Resources/{MaterialPath} — beam will use default.");
        }

        public void Dispose()
        {
            foreach (var beam in _active)
                if (beam.Go != null) Object.Destroy(beam.Go);
            _active.Clear();
        }
    }
}
