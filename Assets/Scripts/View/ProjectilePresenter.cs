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
        readonly GameObject _surfaceImpactPrefab;
        readonly GameObject _bodyImpactPrefab;
        readonly GameObject _headImpactPrefab;
        readonly GameObject _armorImpactPrefab;
        readonly GameObject _ricochetSparkPrefab;
        readonly Dictionary<EId, ProjectileView> _views = new();

        public ProjectilePresenter()
        {
            _projectilePrefab = Resources.Load<GameObject>("Prefabs/Projectile");
            _surfaceImpactPrefab = Resources.Load<GameObject>("Vfx/Prefabs/Impacts/BulletImpact");
            _bodyImpactPrefab = Resources.Load<GameObject>("Vfx/Prefabs/Impacts/BodyImpact");
            _headImpactPrefab = Resources.Load<GameObject>("Vfx/Prefabs/Impacts/HeadImpact");
            _armorImpactPrefab = Resources.Load<GameObject>("Vfx/Prefabs/Impacts/ArmorImpact");
            _ricochetSparkPrefab = Resources.Load<GameObject>("Vfx/Prefabs/Impacts/RicochetSpark");

            // Fallback: if body/head not found, reuse surface
            if (_bodyImpactPrefab == null) _bodyImpactPrefab = _surfaceImpactPrefab;
            if (_headImpactPrefab == null) _headImpactPrefab = _surfaceImpactPrefab;
            if (_armorImpactPrefab == null) _armorImpactPrefab = _surfaceImpactPrefab;
            if (_ricochetSparkPrefab == null) _ricochetSparkPrefab = _surfaceImpactPrefab;

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
                    {
                        // Find targeted entity and combat stats from state
                        EId targeted = default;
                        float penetration = 0f;
                        float armorDamage = 0f;
                        float bleedChance = 0f;
                        foreach (var p in session.RaidState.Projectiles)
                        {
                            if (p.Id == e.Id)
                            {
                                targeted = p.TargetedEntityId;
                                penetration = p.Penetration;
                                armorDamage = p.ArmorDamage;
                                bleedChance = p.BleedChance;
                                break;
                            }
                        }
                        SpawnView(e.Id, e.Position, e.Direction, e.Damage, targeted, penetration, armorDamage, bleedChance);
                        break;
                    }
                    case RaidEventType.ProjectileHit:
                        SpawnImpactVfx(e.Position, e.StringPayload);
                        break;
                    case RaidEventType.ProjectileRicochet:
                        SpawnRicochetVfx(e.Position);
                        DespawnView(e.Id);
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
                    // Pass targetedEntityId on first sync (Initialize only sets basics)
                    view.SyncFromState(proj);
                }
                else
                {
                    // View not yet created — will be created on next ProjectileSpawned event
                }
            }
        }

        void SpawnView(EId id, Vector3 position, Vector3 direction, float damage,
            EId targetedEntityId = default, float penetration = 0f, float armorDamage = 0f,
            float bleedChance = 0f)
        {
            if (_projectilePrefab == null) return;

            var rotation = direction.sqrMagnitude > 0.001f
                ? Quaternion.LookRotation(direction, Vector3.up)
                : Quaternion.identity;

            var go = Object.Instantiate(_projectilePrefab, position, rotation);
            var view = go.GetComponent<ProjectileView>();
            view.Initialize(id, damage, targetedEntityId, penetration, armorDamage, bleedChance);
            _views[id] = view;
        }

        void SpawnImpactVfx(Vector3 position, string hitType)
        {
            // Parse hitType format: "body:0.45" or "head:0.00" or legacy "body"/"head"/"surface"
            string baseType = hitType;
            float absorption = 0f;
            int colonIdx = hitType.IndexOf(':');
            if (colonIdx >= 0)
            {
                baseType = hitType.Substring(0, colonIdx);
                float.TryParse(hitType.Substring(colonIdx + 1),
                    System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out absorption);
            }

            // Flesh VFX (blood) — spawned when absorption < 1
            if (absorption < 0.95f)
            {
                var fleshPrefab = baseType switch
                {
                    "head" => _headImpactPrefab,
                    "body" => _bodyImpactPrefab,
                    _ => _surfaceImpactPrefab,
                };
                if (fleshPrefab != null)
                {
                    var go = Object.Instantiate(fleshPrefab, position, Quaternion.identity);
                    // Scale down blood when armor absorbs most damage
                    if (absorption > 0.1f)
                        go.transform.localScale *= (1f - absorption * 0.7f);
                    Object.Destroy(go, 2f);
                }
            }

            // Armor VFX (sparks) — spawned when absorption > 0.1
            if (absorption > 0.1f && _armorImpactPrefab != null)
            {
                var go = Object.Instantiate(_armorImpactPrefab, position, Quaternion.identity);
                // Scale up sparks with more absorption
                go.transform.localScale *= (0.3f + absorption * 0.7f);
                Object.Destroy(go, 2f);
            }
        }

        void SpawnRicochetVfx(Vector3 position)
        {
            if (_ricochetSparkPrefab == null) return;
            var go = Object.Instantiate(_ricochetSparkPrefab, position, Quaternion.identity);
            Object.Destroy(go, 1.5f);
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
