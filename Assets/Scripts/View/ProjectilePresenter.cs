using System.Collections.Generic;
using Adapters;
using Session;
using State;
using UnityEngine;

namespace View
{
    public class ProjectilePresenter
    {
        readonly GameObject _projectilePrefab;
        readonly GameObject _impactVfxPrefab;
        readonly Dictionary<EId, ProjectileView> _views = new();

        public ProjectilePresenter()
        {
            _projectilePrefab = Resources.Load<GameObject>("Prefabs/Projectile");
            _impactVfxPrefab = Resources.Load<GameObject>("Vfx/Prefabs/Impacts/BulletImpact");

            if (_projectilePrefab == null)
                Debug.LogWarning("[ProjectilePresenter] Prefab not found at Resources/Prefabs/Projectile");
        }

        public void LateTick(RaidSession session)
        {
            if (session == null) return;

            var events = session.ConsumeEvents();

            foreach (var e in events.All)
            {
                switch (e.Type)
                {
                    case RaidEventType.ProjectileSpawned:
                        SpawnView(e.Id, e.Position, e.Direction, e.Damage);
                        break;
                    case RaidEventType.ProjectileHit:
                        SpawnImpactVfx(e.Position);
                        break;
                    case RaidEventType.ProjectileDespawned:
                        DespawnView(e.Id);
                        break;
                }
            }

            foreach (var proj in session.RaidState.Projectiles)
            {
                if (_views.TryGetValue(proj.Id, out var view))
                {
                    view.SyncFromState(proj);
                }
            }
        }

        void SpawnView(EId id, Vector3 position, Vector3 direction, float damage)
        {
            if (_projectilePrefab == null) return;

            var rotation = direction.sqrMagnitude > 0.001f
                ? Quaternion.LookRotation(direction, Vector3.up)
                : Quaternion.identity;

            var go = Object.Instantiate(_projectilePrefab, position, rotation);
            var view = go.GetComponent<ProjectileView>();
            view.Initialize(id, damage);
            _views[id] = view;
        }

        void SpawnImpactVfx(Vector3 position)
        {
            if (_impactVfxPrefab == null) return;
            var go = Object.Instantiate(_impactVfxPrefab, position, Quaternion.identity);
            Object.Destroy(go, 2f);
        }

        void DespawnView(EId id)
        {
            if (_views.TryGetValue(id, out var view))
            {
                Object.Destroy(view.gameObject);
                _views.Remove(id);
            }
        }

        public void Dispose()
        {
            foreach (var kvp in _views)
            {
                if (kvp.Value != null)
                    Object.Destroy(kvp.Value.gameObject);
            }

            _views.Clear();
        }
    }
}
