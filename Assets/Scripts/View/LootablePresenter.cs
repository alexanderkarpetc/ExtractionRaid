using System.Collections.Generic;
using Adapters;
using Constants;
using Session;
using State;
using Systems;
using UnityEngine;
using View.SpawnPoints;

namespace View
{
    /// <summary>
    /// Spawns view GameObjects for lootables — bot corpses AND scene containers.
    /// Both flow through the same <see cref="Adapters.RaidEventType.LootableSpawned"/>
    /// event from <see cref="Systems.LootSystem"/>; this presenter dispatches on
    /// <c>LootableState.IsContainer</c> to pick the corpse vs container visual.
    /// </summary>
    public class LootablePresenter
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
                        // Only scene containers get a spawned view. Bot corpses are now
                        // represented visually by the ragdoll body (Gunplay A.9), so we no
                        // longer spawn the old brown capsule loot marker for them. Looting
                        // still works — interaction is state-based (LootSystem.FindNearest-
                        // Interactable reads LootableState positions, not this GameObject).
                        var lootable = LootSystem.GetLootable(session.RaidState, e.Id);
                        if (lootable != null && lootable.IsContainer)
                            SpawnContainerView(e.Id, e.Position, e.StringPayload);
                        break;
                    }
                    case RaidEventType.LootableDespawned:
                        DespawnView(e.Id);
                        break;
                }
            }

        }

        void SpawnContainerView(EId id, Vector3 position, string typeId)
        {
            if (_views.ContainsKey(id)) return;

            string displayName = typeId;
            if (ContainerConstants.TryGetConfig(typeId, out var cfg))
                displayName = cfg.DisplayName;

            // Per-spawn-point override registered by RaidSession. If present, just
            // instantiate that prefab and keep the procedural cube path off.
            var overridePrefab = ContainerVisualRegistry.Consume(position);
            if (overridePrefab != null)
            {
                var go = Object.Instantiate(overridePrefab, position, Quaternion.identity);
                go.name = $"Container_{typeId}_{id}";
                go.AddComponent<InteractableOutlineTarget>();
                // Container prefabs (ER_MCrate01) sit on the NonXRay layer. The scene-wide
                // queue pass only covers objects present at load, so freshly spawned
                // containers have to opt in explicitly.
                NonXRayRenderQueue.Apply(go);
                _views[id] = go;
                return;
            }

            var fallback = GameObject.CreatePrimitive(PrimitiveType.Cube);
            fallback.name = $"Container_{typeId}_{id}";
            fallback.transform.position = position + new Vector3(0f, 0.3f, 0f);
            fallback.transform.localScale = new Vector3(0.6f, 0.5f, 0.4f);

            var renderer = fallback.GetComponent<Renderer>();
            if (renderer != null)
                renderer.material.color = GetContainerColor(typeId);

            AttachLabel(fallback, displayName, new Color(0.7f, 0.9f, 1f));
            fallback.AddComponent<InteractableOutlineTarget>();
            _views[id] = fallback;
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
