using System.Collections.Generic;
using Adapters;
using Session;
using State;
using UnityEngine;

namespace View
{
    public class CorpsePresenter
    {
        readonly Dictionary<EId, GameObject> _views = new();

        public void LateTick(RaidSession session)
        {
            if (session == null) return;

            var events = session.ConsumeEvents();

            foreach (var e in events.All)
            {
                switch (e.Type)
                {
                    case RaidEventType.LootableSpawned:
                        SpawnView(e.Id, e.Position, e.StringPayload);
                        break;
                    case RaidEventType.LootableDespawned:
                        DespawnView(e.Id);
                        break;
                }
            }
        }

        void SpawnView(EId id, Vector3 position, string typeId)
        {
            if (_views.ContainsKey(id)) return;

            var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.name = $"Corpse_{typeId}_{id}";
            go.transform.position = position + new Vector3(0f, 0.15f, 0f);
            go.transform.localScale = new Vector3(0.8f, 0.15f, 0.8f);

            var renderer = go.GetComponent<Renderer>();
            if (renderer != null)
                renderer.material.color = new Color(0.4f, 0.2f, 0.15f);

            var labelGo = new GameObject("Label");
            labelGo.transform.SetParent(go.transform, false);
            labelGo.transform.localPosition = new Vector3(0f, 5f, 0f);

            var label = labelGo.AddComponent<TextMesh>();
            label.text = typeId;
            label.characterSize = 0.15f;
            label.fontSize = 48;
            label.anchor = TextAnchor.MiddleCenter;
            label.alignment = TextAlignment.Center;
            label.color = new Color(0.9f, 0.7f, 0.5f);
            labelGo.AddComponent<BillboardText>();

            _views[id] = go;
        }

        void DespawnView(EId id)
        {
            if (_views.TryGetValue(id, out var go))
            {
                Object.Destroy(go);
                _views.Remove(id);
            }
        }

        public void Dispose()
        {
            foreach (var kvp in _views)
            {
                if (kvp.Value != null)
                    Object.Destroy(kvp.Value);
            }
            _views.Clear();
        }
    }
}
