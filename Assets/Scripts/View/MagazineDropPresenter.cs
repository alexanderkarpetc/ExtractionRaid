using System.Collections.Generic;
using Adapters;
using Dev;
using Session;
using UnityEngine;

namespace View
{
    /// <summary>
    /// Listens до <see cref="RaidEventType.WeaponReloadStarted"/> + drops a physics magazine
    /// GO at the player's feet. Ballistic-only — laser archetype uses a different feedback
    /// (energy cell vent / TBD). Mirrors <see cref="CasingEjectorPresenter"/> pool semantics:
    /// hybrid auto-settle (damping ramp → kinematic freeze), scale-shrink fade у last 30%.
    ///
    /// Реload events fire only for the player (bots reload silently, see WeaponStateMachineSystem
    /// + ShootingSystem), so reading <c>RaidState.PlayerEntity</c> on each event is safe.
    ///
    /// DropDelay matches real-world reload timing — mag ejects mid-anim, not at the press
    /// frame. Pending queue defers spawn до the right beat.
    /// </summary>
    public class MagazineDropPresenter
    {
        struct ActiveMag
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

        struct PendingMag
        {
            public float   FireUnscaledTime; // when to spawn
            public Vector3 OriginPosition;
            public Vector3 OriginForward;
        }

        readonly Queue<ActiveMag>  _active  = new();
        readonly List<PendingMag>  _pending = new();
        Transform  _root;
        GameObject _prefab;
        bool       _prefabLoaded;

        public void LateTick(RaidSession session)
        {
            if (session == null) return;
            var cfg = ViewCheats.Config?.Magazine;
            if (cfg == null || !cfg.Enabled) return;

            EnsurePrefabLoaded();
            EnsureRoot();

            foreach (var e in session.ConsumeEvents().All)
            {
                if (e.Type != RaidEventType.WeaponReloadStarted) continue;

                var player = session.RaidState.PlayerEntity;
                var weapon = player?.EquippedWeapon;
                var archetype = weapon?.PayloadDefinition?.Archetype;
                if (archetype != "Ballistic") continue;

                _pending.Add(new PendingMag
                {
                    FireUnscaledTime = Time.unscaledTime + Mathf.Max(0f, cfg.DropDelay),
                    OriginPosition   = player.Position,
                    OriginForward    = player.FacingDirection.sqrMagnitude > 0.0001f
                        ? player.FacingDirection.normalized
                        : Vector3.forward,
                });
            }

            TickPending(cfg);
            TickActive(cfg);
        }

        void TickPending(ViewCheatsMagazineSection cfg)
        {
            if (_pending.Count == 0) return;
            float now = Time.unscaledTime;
            for (int i = _pending.Count - 1; i >= 0; i--)
            {
                if (now < _pending[i].FireUnscaledTime) continue;
                SpawnMagazine(_pending[i].OriginPosition, _pending[i].OriginForward, cfg);
                _pending.RemoveAt(i);
            }
        }

        void SpawnMagazine(Vector3 originPos, Vector3 forward, ViewCheatsMagazineSection cfg)
        {
            if (_prefab == null) return;

            var fwd   = forward;
            var right = Vector3.Cross(Vector3.up, fwd).normalized;
            var up    = Vector3.up;

            var localOffset = cfg.SpawnOffset;
            var spawnPos = originPos
                         + right * localOffset.x
                         + up    * localOffset.y
                         + fwd   * localOffset.z;

            while (_active.Count >= cfg.MaxActive)
            {
                var old = _active.Dequeue();
                if (old.Go != null) Object.Destroy(old.Go);
            }

            var go = Object.Instantiate(_prefab, spawnPos, Random.rotationUniform, _root);

            // Use Ragdoll layer — InitCollisionMatrix already ignores Ragdoll vs Player/Bot,
            // so the magazine спaвн inside player capsule не штовхатиметься в нікуди. Ground
            // (Default) collision залишається.
            LayerUtils.SetLayerRecursively(go, LayerUtils.Ragdoll);

            float jitter = cfg.VelocityJitter;
            var velocity =
                  fwd  * (cfg.ForwardVelocity  + Random.Range(-jitter, jitter))
                - up   * (cfg.DownwardVelocity + Random.Range(-jitter, jitter));

            var rb = go.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.mass            = cfg.Mass;
                rb.linearDamping   = cfg.LinearDamping;
                rb.angularDamping  = cfg.AngularDamping;
                rb.linearVelocity  = velocity;
                rb.angularVelocity = new Vector3(
                    Random.Range(-cfg.SpinMagnitude, cfg.SpinMagnitude),
                    Random.Range(-cfg.SpinMagnitude, cfg.SpinMagnitude),
                    Random.Range(-cfg.SpinMagnitude, cfg.SpinMagnitude));
            }

            _active.Enqueue(new ActiveMag
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

        void TickActive(ViewCheatsMagazineSection cfg)
        {
            float now = Time.unscaledTime;
            int count = _active.Count;
            for (int i = 0; i < count; i++)
            {
                var m = _active.Dequeue();
                if (m.Go == null) continue;

                float age = now - m.SpawnedUnscaled;
                if (age >= m.Lifetime)
                {
                    Object.Destroy(m.Go);
                    continue;
                }

                if (m.Rb != null && !m.Frozen)
                {
                    float settleStart = cfg.SettleDelay;
                    float settleEnd   = cfg.SettleDelay + cfg.SettleTimeout;
                    if (age >= settleEnd)
                    {
                        m.Rb.linearVelocity  = Vector3.zero;
                        m.Rb.angularVelocity = Vector3.zero;
                        m.Rb.isKinematic     = true;
                        if (m.Collider != null && cfg.DisableColliderOnSettle)
                            m.Collider.enabled = false;
                        m.Frozen = true;
                    }
                    else if (age >= settleStart)
                    {
                        float t = (age - settleStart) / (settleEnd - settleStart);
                        m.Rb.linearDamping  = Mathf.Lerp(cfg.LinearDamping,  cfg.MaxLinearDamping,  t);
                        m.Rb.angularDamping = Mathf.Lerp(cfg.AngularDamping, cfg.MaxAngularDamping, t);
                    }
                }

                float fadeStart = m.Lifetime * 0.7f;
                if (age >= fadeStart && m.Transform != null)
                {
                    float fadeT = (age - fadeStart) / (m.Lifetime - fadeStart);
                    float k = 1f - fadeT;
                    float scaleK = k * k * k;
                    m.Transform.localScale = m.InitialScale * scaleK;
                }

                _active.Enqueue(m);
            }
        }

        void EnsurePrefabLoaded()
        {
            if (_prefabLoaded) return;
            _prefabLoaded = true;
            _prefab = Resources.Load<GameObject>("Prefabs/Magazines/Magazine");
            if (_prefab == null)
                Debug.LogWarning("[MagazineDropPresenter] Prefab not found at Resources/Prefabs/Magazines/Magazine");
        }

        void EnsureRoot()
        {
            if (_root != null) return;
            var go = new GameObject("[MagazinePool]");
            _root = go.transform;
        }

        public void Dispose()
        {
            while (_active.Count > 0)
            {
                var m = _active.Dequeue();
                if (m.Go != null) Object.Destroy(m.Go);
            }
            _pending.Clear();
        }
    }
}
