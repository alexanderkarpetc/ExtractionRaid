using System.Collections.Generic;
using Adapters;
using Session;
using State;
using UnityEngine;

namespace View
{
    public class GrenadePresenter
    {
        readonly GameObject _grenadePrefab;
        readonly GrenadePositionAdapter _positionAdapter;
        readonly Dictionary<EId, GrenadeView> _views = new();

        public GrenadePresenter(GrenadePositionAdapter positionAdapter)
        {
            _positionAdapter = positionAdapter;
            _grenadePrefab = Resources.Load<GameObject>("Prefabs/Grenade");

            if (_grenadePrefab == null)
                Debug.LogWarning("[GrenadePresenter] Prefab not found at Resources/Prefabs/Grenade");
        }

        public void LateTick(RaidSession session)
        {
            if (session == null) return;

            var events = session.ConsumeEvents();

            foreach (var e in events.All)
            {
                switch (e.Type)
                {
                    case RaidEventType.GrenadeSpawned:
                        SpawnView(e.Id, e.Position, e.Direction);
                        break;
                    case RaidEventType.GrenadeExploded:
                        OnExploded(e.Id, e.Position);
                        break;
                    case RaidEventType.GrenadeDespawned:
                        DespawnView(e.Id);
                        break;
                }
            }
        }

        void SpawnView(EId id, Vector3 position, Vector3 velocity)
        {
            if (_grenadePrefab == null) return;

            var go = Object.Instantiate(_grenadePrefab, position, Quaternion.identity);
            var view = go.GetComponent<GrenadeView>();
            if (view == null)
                view = go.AddComponent<GrenadeView>();

            view.Initialize(id, velocity);
            _views[id] = view;
            _positionAdapter.Register(id, go.transform);

            var playerView = Object.FindAnyObjectByType<PlayerView>();
            if (playerView != null)
                view.IgnoreCollisionWith(playerView.gameObject);
        }

        void OnExploded(EId id, Vector3 position)
        {
            // TODO: spawn explosion VFX/SFX at position
        }

        void DespawnView(EId id)
        {
            _positionAdapter.Unregister(id);

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
                {
                    _positionAdapter.Unregister(kvp.Key);
                    Object.Destroy(kvp.Value.gameObject);
                }
            }

            _views.Clear();
        }
    }
}
