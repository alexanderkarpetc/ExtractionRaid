using System.Collections.Generic;
using Adapters;
using Session;
using State;
using UnityEngine;

namespace View
{
    public class DestructiblePresenter
    {
        readonly Dictionary<EId, DestructibleView> _views = new();
        bool _initialized;

        public void LateTick(RaidSession session)
        {
            if (session == null) return;

            if (!_initialized)
            {
                RegisterSceneDestructibles(session);
                _initialized = true;
            }

            var events = session.ConsumeEvents();
            foreach (var e in events.All)
            {
                switch (e.Type)
                {
                    case RaidEventType.DestructibleDestroyed:
                        HandleDestroyed(e.Id, session);
                        break;
                }
            }
        }

        void RegisterSceneDestructibles(RaidSession session)
        {
            var sceneViews = Object.FindObjectsByType<DestructibleView>(FindObjectsSortMode.None);

            foreach (var view in sceneViews)
            {
                var id = session.RaidState.AllocateEId();
                view.Initialize(id);

                var health = HealthState.Create(view.MaxHp);
                session.RaidState.HealthMap[id] = health;
                _views[id] = view;

                Debug.Log($"[DestructiblePresenter] Registered {view.name} as {id} with {view.MaxHp} HP");
            }
        }

        void HandleDestroyed(EId id, RaidSession session)
        {
            if (_views.TryGetValue(id, out var view))
            {
                Object.Destroy(view.gameObject);
                _views.Remove(id);
            }

            session.RaidState.HealthMap.Remove(id);
        }

        public void Dispose()
        {
            _views.Clear();
        }
    }
}
