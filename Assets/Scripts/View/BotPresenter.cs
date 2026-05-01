using System.Collections.Generic;
using Adapters;
using Constants;
using Dev;
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
                    case RaidEventType.ArmorBroken:
                        if (_views.TryGetValue(e.Id, out var armorView))
                        {
                            bool isHelmet = e.Damage > 0.5f;
                            if (isHelmet)
                            {
                                var helmet = armorView.DetachHelmetModel();
                                if (helmet != null)
                                    ArmorBreakHelper.FlyOffHelmet(helmet);
                            }
                            else
                            {
                                armorView.ClearArmorModel();
                            }
                        }
                        break;
                    case RaidEventType.EntityHit:
                        if (_views.TryGetValue(e.Id, out var hitView))
                            ApplyHitFlash(hitView, e);
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

                    // Sync armor bar
                    if (session.RaidState.ArmorMap.TryGetValue(bot.Id, out var armorSlots))
                    {
                        float helmetDur = armorSlots.Helmet?.DurabilityPercent ?? 0f;
                        float vestDur = armorSlots.BodyArmor?.DurabilityPercent ?? 0f;
                        view.UpdateArmor(helmetDur, vestDur);
                    }
                }
            }
        }

        void SpawnView(EId id, Vector3 position, string typeId)
        {
            if (!BotConstants.TryGetConfig(typeId, out var config))
                return;

            // 1. Shell (View + Collider)
            var shellPrefab = GetPrefab(config.PrefabId);
            if (shellPrefab == null) return;

            var shellGo = Object.Instantiate(shellPrefab, position, Quaternion.identity);
            var view = shellGo.GetComponent<BotView>();
            if (view == null)
                view = shellGo.AddComponent<BotView>();

            // 2. Body as child (CharacterBody + visual mesh)
            if (!string.IsNullOrEmpty(config.BodyPrefabId))
            {
                var bodyPrefab = GetPrefab("Bodies/" + config.BodyPrefabId);
                if (bodyPrefab != null)
                {
                    var bodyGo = Object.Instantiate(bodyPrefab, shellGo.transform);
                    bodyGo.transform.localPosition = Vector3.zero;
                    bodyGo.transform.localRotation = Quaternion.identity;

                    var body = bodyGo.GetComponent<CharacterBody>();
                    if (body != null)
                        view.BindBody(body);
                }
            }

            view.Initialize(id, typeId, config.WeaponPrefabId, config.MaxHp);
            view.GizmoVisionRange = config.VisionRange;
            view.GizmoVisionAngle = config.VisionAngle;

            // Equip armor visuals from bot config
            if (!string.IsNullOrEmpty(config.HelmetDefinitionId))
            {
                var helmetDef = ItemDefinition.Get(config.HelmetDefinitionId);
                if (helmetDef != null && !string.IsNullOrEmpty(helmetDef.ArmorPrefabId))
                    view.SwapHelmetModel(helmetDef.ArmorPrefabId);
            }
            if (!string.IsNullOrEmpty(config.BodyArmorDefinitionId))
            {
                var armorDef = ItemDefinition.Get(config.BodyArmorDefinitionId);
                if (armorDef != null && !string.IsNullOrEmpty(armorDef.ArmorPrefabId))
                    view.SwapArmorModel(armorDef.ArmorPrefabId);
            }

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
                Debug.LogError($"[BotPresenter] Prefab not found: Prefabs/{prefabId}");

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

        // Gunplay A.2 — pick flash color per hit kind, route to BotView.
        static void ApplyHitFlash(BotView view, RaidEvent e)
        {
            var cfg = DevCheats.Config?.HitFlash;
            if (cfg == null || !cfg.Enabled) return;

            // RaidEventBuffer.EntityHit packs:
            //   CurrentHp = isHeadshot ? 1 : 0
            //   MaxHp     = isKill     ? 1 : 0
            //   KillerId.Value = isRicochet ? 1 : 0
            bool isHeadshot = e.CurrentHp > 0.5f;
            bool isKill     = e.MaxHp     > 0.5f;
            bool isRicochet = e.KillerId.Value == 1;

            // Priority: Ricochet > Kill > Headshot > Normal — ricochet ловиться першим
            // бо there's no damage taken (different kind of feedback).
            Color color;
            if      (isRicochet) color = cfg.RicochetColor;
            else if (isKill)     color = cfg.KillColor;
            else if (isHeadshot) color = cfg.HeadshotColor;
            else                 color = cfg.NormalColor;

            view.TriggerHitFlash(color, cfg.Intensity, cfg.Duration, cfg.EmissionBoost);
        }
    }
}
