using System.Collections.Generic;
using Adapters;
using Constants;
using Session;
using State;
using UnityEngine;

namespace View
{
    public class BotPresenter
    {
        readonly Dictionary<EId, BotView> _views = new();
        readonly Dictionary<string, GameObject> _prefabCache = new();

        public void LateTick(RaidSession session)
        {
            if (session == null) return;

            var events = session.ConsumeEvents();

            foreach (var e in events.All)
            {
                switch (e.Type)
                {
                    case RaidEventType.BotSpawned:
                        SpawnView(e.Id, e.Position, e.StringPayload);
                        break;
                    case RaidEventType.BotDespawned:
                        DespawnView(e.Id);
                        break;
                    case RaidEventType.EntityDamaged:
                        if (_views.TryGetValue(e.Id, out var damagedView))
                            damagedView.OnDamaged(e.CurrentHp, e.MaxHp);
                        break;
                }
            }

            foreach (var bot in session.RaidState.Bots)
            {
                if (_views.TryGetValue(bot.Id, out var view))
                {
                    float hp = 0f, maxHp = 0f;
                    if (session.RaidState.HealthMap.TryGetValue(bot.Id, out var health))
                    {
                        hp = health.CurrentHp;
                        maxHp = health.MaxHp;
                    }
                    view.SyncFromState(bot, hp, maxHp);
                }
            }
        }

        void SpawnView(EId id, Vector3 position, string typeId)
        {
            if (!BotConstants.TryGetConfig(typeId, out var config))
                return;

            var prefab = GetPrefab(config.PrefabId);
            if (prefab == null) return;

            var go = Object.Instantiate(prefab, position, Quaternion.identity);
            var view = go.GetComponent<BotView>();
            if (view == null)
                view = go.AddComponent<BotView>();

            view.Initialize(id, typeId, config.WeaponPrefabId);
            view.GizmoVisionRange = config.VisionRange;
            view.GizmoVisionAngle = config.VisionAngle;
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

        GameObject GetPrefab(string prefabId)
        {
            if (_prefabCache.TryGetValue(prefabId, out var cached))
                return cached;

            var prefab = Resources.Load<GameObject>("Prefabs/" + prefabId);
            if (prefab == null)
                Debug.LogWarning($"[BotPresenter] Prefab not found: Prefabs/{prefabId}");

            _prefabCache[prefabId] = prefab;
            return prefab;
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
