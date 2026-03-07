using System.Collections.Generic;
using Adapters;
using Session;
using State;
using UnityEngine;

namespace View
{
    public class GroundItemPresenter
    {
        readonly Dictionary<EId, GroundItemView> _views = new();

        public void LateTick(RaidSession session)
        {
            if (session == null) return;

            var events = session.ConsumeEvents();

            foreach (var e in events.All)
            {
                switch (e.Type)
                {
                    case RaidEventType.GroundItemSpawned:
                    {
                        var def = ItemDefinition.Get(e.StringPayload);
                        var displayName = def?.DisplayName ?? e.StringPayload;
                        SpawnView(e.Id, e.Position, displayName);
                        break;
                    }
                    case RaidEventType.GroundItemDespawned:
                        DespawnView(e.Id);
                        break;
                }
            }
        }

        void SpawnView(EId id, Vector3 position, string displayName)
        {
            if (_views.ContainsKey(id)) return;

            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.transform.position = position + new Vector3(0f, 0.5f, 0f);
            go.transform.localScale = new Vector3(0.5f, 0.5f, 0.5f);

            var renderer = go.GetComponent<Renderer>();
            if (renderer != null)
                renderer.material.color = new Color(0.3f, 0.8f, 0.4f);

            var view = go.AddComponent<GroundItemView>();
            view.Initialize(id, displayName);
            _views[id] = view;
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
