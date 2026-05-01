using System.Collections.Generic;
using Adapters;
using Dev;
using Session;
using UnityEngine;

namespace View
{
    /// <summary>
    /// Gunplay A.6 — listens до <see cref="RaidEventType.WeaponFired"/> + spawns physics
    /// brass shells from the ejection port. Pool-bounded (oldest replaced); each casing has
    /// physics velocity + spin tumble before falling. Last 30% of lifetime — scale shrink.
    ///
    /// Casing prefab — `Resources/Prefabs/Casings/Casing.prefab` (primitive cylinder + brass
    /// material + Rigidbody). Replaced з real authored asset за need.
    /// </summary>
    public class CasingEjectorPresenter
    {
        struct ActiveCasing
        {
            public GameObject Go;
            public Transform  Transform;
            public Vector3    InitialScale;
            public float      SpawnedUnscaled;
            public float      Lifetime;
            public Rigidbody  Rb;
            public Collider   Collider;
            public bool       Frozen;
        }

        readonly Queue<ActiveCasing> _active = new();
        Transform _root;
        GameObject _prefab;
        bool _prefabLoaded;

        public void LateTick(RaidSession session)
        {
            if (session == null) return;
            var cfg = ViewCheats.Config?.Casings;
            if (cfg == null || !cfg.Enabled) return;

            EnsurePrefabLoaded();
            EnsureRoot();

            foreach (var e in session.ConsumeEvents().All)
            {
                if (e.Type != RaidEventType.WeaponFired) continue;
                // Casings are physical brass shells — only ballistic-archetype payloads eject them.
                // Laser / Foam / Rocket / etc. don't have brass shells; future per-archetype
                // ejection effects (energy crackle, capsule drop, тощо) — окремі presenters.
                if (e.StringPayload != "Ballistic") continue;
                SpawnCasing(e.Position, e.Direction, cfg);
            }

            TickActive();
        }

        void SpawnCasing(Vector3 firePosition, Vector3 fireDirection, ViewCheatsCasingsSection cfg)
        {
            if (_prefab == null) return;
            if (fireDirection.sqrMagnitude < 0.0001f) return;

            // Build local axes from fire direction. Right = perpendicular у horizontal plane.
            // Use world up to avoid roll bias — top-down camera, no banking weapons.
            var fwd   = fireDirection.normalized;
            var right = Vector3.Cross(Vector3.up, fwd).normalized;
            var up    = Vector3.up;

            // Eject port = fire origin + local offset transformed via fwd/right/up basis.
            var localOffset = cfg.EjectPortOffset;
            var spawnPos = firePosition
                         + right * localOffset.x
                         + up    * localOffset.y
                         + fwd   * localOffset.z;

            // Evict oldest якщо capacity reached.
            while (_active.Count >= cfg.MaxActive)
            {
                var old = _active.Dequeue();
                if (old.Go != null) Object.Destroy(old.Go);
            }

            var go = Object.Instantiate(_prefab, spawnPos, Random.rotationUniform, _root);

            // Velocity composition.
            float jitter = cfg.VelocityJitter;
            var velocity =
                  right * (cfg.LateralVelocity  + Random.Range(-jitter, jitter))
                + up    * (cfg.UpwardVelocity   + Random.Range(-jitter, jitter))
                - fwd   * (cfg.BackwardVelocity + Random.Range(-jitter, jitter));

            var rb = go.GetComponent<Rigidbody>();
            if (rb != null)
            {
                // Override prefab values з config — runtime tunable from ViewCheats.
                rb.mass           = cfg.Mass;
                rb.linearDamping  = cfg.LinearDamping;
                rb.angularDamping = cfg.AngularDamping;
                rb.linearVelocity = velocity;
                rb.angularVelocity = new Vector3(
                    Random.Range(-cfg.SpinMagnitude, cfg.SpinMagnitude),
                    Random.Range(-cfg.SpinMagnitude, cfg.SpinMagnitude),
                    Random.Range(-cfg.SpinMagnitude, cfg.SpinMagnitude));
            }

            _active.Enqueue(new ActiveCasing
            {
                Go              = go,
                Transform       = go.transform,
                InitialScale    = go.transform.localScale,
                SpawnedUnscaled = Time.unscaledTime,
                Lifetime        = Mathf.Max(0.5f, cfg.Lifetime),
                Rb              = rb,
                Collider        = go.GetComponentInChildren<Collider>(),
                Frozen          = false,
            });
        }

        void TickActive()
        {
            float now = Time.unscaledTime;
            var cfg = ViewCheats.Config?.Casings;
            int count = _active.Count;
            for (int i = 0; i < count; i++)
            {
                var c = _active.Dequeue();
                if (c.Go == null) continue;

                float age = now - c.SpawnedUnscaled;
                if (age >= c.Lifetime)
                {
                    Object.Destroy(c.Go);
                    continue;
                }

                // Settle ramp + freeze (Hybrid + linear timer).
                if (cfg != null && c.Rb != null && !c.Frozen)
                {
                    float settleStart = cfg.SettleDelay;
                    float settleEnd   = cfg.SettleDelay + cfg.SettleTimeout;
                    if (age >= settleEnd)
                    {
                        // Final freeze — kinematic + ignore player.
                        c.Rb.linearVelocity  = Vector3.zero;
                        c.Rb.angularVelocity = Vector3.zero;
                        c.Rb.isKinematic     = true;
                        if (c.Collider != null && cfg.DisableColliderOnSettle)
                            c.Collider.enabled = false;
                        c.Frozen = true;
                    }
                    else if (age >= settleStart)
                    {
                        // Ramp damping linearly base → max during SettleTimeout window.
                        float t = (age - settleStart) / (settleEnd - settleStart);
                        c.Rb.linearDamping  = Mathf.Lerp(cfg.LinearDamping,  cfg.MaxLinearDamping,  t);
                        c.Rb.angularDamping = Mathf.Lerp(cfg.AngularDamping, cfg.MaxAngularDamping, t);
                    }
                }

                // Scale shrink fade у last 30% — same convention як decal pool.
                float fadeStart = c.Lifetime * 0.7f;
                if (age >= fadeStart && c.Transform != null)
                {
                    float fadeT = (age - fadeStart) / (c.Lifetime - fadeStart);
                    float k = 1f - fadeT;
                    float scaleK = k * k * k;
                    c.Transform.localScale = c.InitialScale * scaleK;
                }

                _active.Enqueue(c);
            }
        }

        void EnsurePrefabLoaded()
        {
            if (_prefabLoaded) return;
            _prefabLoaded = true;
            _prefab = Resources.Load<GameObject>("Prefabs/Casings/Casing");
            if (_prefab == null)
                Debug.LogWarning("[CasingEjectorPresenter] Casing prefab not found at Resources/Prefabs/Casings/Casing");
        }

        void EnsureRoot()
        {
            if (_root != null) return;
            var go = new GameObject("[CasingPool]");
            _root = go.transform;
        }

        public void Dispose()
        {
            while (_active.Count > 0)
            {
                var c = _active.Dequeue();
                if (c.Go != null) Object.Destroy(c.Go);
            }
        }
    }
}
