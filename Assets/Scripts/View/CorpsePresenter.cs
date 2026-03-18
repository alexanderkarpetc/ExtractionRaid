using System.Collections.Generic;
using Adapters;
using Constants;
using Session;
using State;
using Systems;
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
                    {
                        var lootable = LootSystem.GetLootable(session.RaidState, e.Id);
                        if (lootable != null && lootable.IsContainer)
                            SpawnContainerView(e.Id, e.Position, e.StringPayload);
                        else
                            SpawnCorpseView(e.Id, e.Position, e.StringPayload);
                        break;
                    }
                    case RaidEventType.LootableDespawned:
                        DespawnView(e.Id);
                        break;
                }
            }
        }

        void SpawnCorpseView(EId id, Vector3 position, string typeId)
        {
            if (_views.ContainsKey(id)) return;

            var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.name = $"Corpse_{typeId}_{id}";
            go.transform.position = position + new Vector3(0f, 0.15f, 0f);
            go.transform.localScale = new Vector3(0.8f, 0.15f, 0.8f);

            var renderer = go.GetComponent<Renderer>();
            if (renderer != null)
                renderer.material.color = new Color(0.4f, 0.2f, 0.15f);

            AttachLabel(go, typeId, new Color(0.9f, 0.7f, 0.5f));
            _views[id] = go;
        }

        void SpawnContainerView(EId id, Vector3 position, string typeId)
        {
            if (_views.ContainsKey(id)) return;

            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            string displayName = typeId;
            if (ContainerConstants.TryGetConfig(typeId, out var cfg))
                displayName = cfg.DisplayName;

            go.name = $"Container_{typeId}_{id}";
            go.transform.position = position + new Vector3(0f, 0.3f, 0f);
            go.transform.localScale = new Vector3(0.6f, 0.5f, 0.4f);

            var renderer = go.GetComponent<Renderer>();
            if (renderer != null)
                renderer.material.color = GetContainerColor(typeId);

            AttachLabel(go, displayName, new Color(0.7f, 0.9f, 1f));
            _views[id] = go;
        }

        static Color GetContainerColor(string typeId)
        {
            return typeId switch
            {
                "MedContainer" => new Color(0.2f, 0.5f, 0.3f),
                "AmmoBox" => new Color(0.4f, 0.35f, 0.2f),
                "RandomLootBox" => new Color(0.35f, 0.3f, 0.45f),
                _ => new Color(0.3f, 0.3f, 0.3f),
            };
        }

        static void AttachLabel(GameObject parent, string text, Color color)
        {
            var labelGo = new GameObject("Label");
            labelGo.transform.SetParent(parent.transform, false);
            labelGo.transform.localPosition = new Vector3(0f, 5f, 0f);

            var label = labelGo.AddComponent<TextMesh>();
            label.text = text;
            label.characterSize = 0.15f;
            label.fontSize = 48;
            label.anchor = TextAnchor.MiddleCenter;
            label.alignment = TextAlignment.Center;
            label.color = color;
            labelGo.AddComponent<BillboardText>();
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
