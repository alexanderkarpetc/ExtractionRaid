using System.Collections.Generic;
using Dev;
using Session;
using State;
using UnityEngine;

namespace Systems
{
    public static class DamageSystem
    {
        public static void Tick(RaidState state, List<HitSignal> hits, in RaidContext context)
        {
            Tick(state, hits, in context, null);
        }

        public static void Tick(RaidState state, List<HitSignal> hits, in RaidContext context,
            System.Func<float> randomProvider)
        {
            foreach (var hit in hits)
            {
                ProjectileEntityState projectile = null;
                foreach (var p in state.Projectiles)
                {
                    if (p.Id == hit.ProjectileId) { projectile = p; break; }
                }

                if (projectile != null && projectile.OwnerId == hit.TargetId)
                    continue;

                if (!state.HealthMap.TryGetValue(hit.TargetId, out var health))
                    continue;

                if (!health.IsAlive) continue;

                if (IsRolling(state, hit.TargetId))
                    continue;

                if (DevCheats.GodMode && state.PlayerEntity != null
                    && hit.TargetId == state.PlayerEntity.Id)
                    continue;

                bool isHeadshot = hit.TargetedEntityId == hit.TargetId
                                 && hit.TargetedEntityId.Value != 0;

                float damage = hit.Damage;
                if (isHeadshot && projectile != null)
                    damage *= projectile.HeadshotDamageMultiplier;

                // Helmet ricochet check (before armor damage reduction)
                if (isHeadshot && state.ArmorMap.TryGetValue(hit.TargetId, out var ricoSlots))
                {
                    var helmet = ricoSlots.Helmet;
                    float ricochetRoll = randomProvider != null ? randomProvider() : Random.value;
                    if (ArmorSystem.ShouldRicochet(helmet, hit.Penetration, ricochetRoll, DevCheats.ArmorRicochetChance))
                    {
                        // Ricochet: 0 HP damage, full durability damage (absorptionRatio = 1)
                        float armorDurDmg = ArmorSystem.CalcArmorDurabilityDamage(hit.ArmorDamage, 1f);
                        ArmorSystem.ApplyDurabilityDamage(helmet, armorDurDmg);

                        if (helmet.IsBroken)
                            context.Events.ArmorBroken(hit.TargetId, isHelmet: true);

                        var ricochetDir = projectile != null ? projectile.Direction : Vector3.forward;
                        context.Events.ProjectileRicochet(hit.ProjectileId, hit.HitPoint, ricochetDir);

                        // Per-target view feedback (flash, future blood/decal). Fires regardless of owner.
                        context.Events.EntityHit(
                            targetEid:           hit.TargetId,
                            hitPoint:            hit.HitPoint,
                            projectileDirection: ricochetDir,
                            isHeadshot:          true,
                            isRicochet:          true,
                            isKill:              false,
                            absorptionRatio:     1f);

                        // Ricochet crosshair feedback (player shots only)
                        if (projectile != null && state.PlayerEntity != null
                            && projectile.OwnerId == state.PlayerEntity.Id)
                        {
                            context.Events.HitConfirmed(isKill: false, isHeadshot: true,
                                absorptionRatio: 1f, isRicochet: true);
                        }

                        // Remove projectile
                        for (int i = state.Projectiles.Count - 1; i >= 0; i--)
                        {
                            if (state.Projectiles[i].Id == hit.ProjectileId)
                            {
                                context.Events.ProjectileDespawned(hit.ProjectileId);
                                state.Projectiles.RemoveAt(i);
                                break;
                            }
                        }
                        continue; // Skip HP damage entirely
                    }
                }

                // Armor damage reduction
                float finalDamage = damage;
                float absorptionRatio = 0f;
                if (state.ArmorMap.TryGetValue(hit.TargetId, out var armorSlots))
                {
                    var result = ArmorSystem.Calculate(damage, hit.Penetration, hit.ArmorDamage,
                        armorSlots, isHeadshot);
                    finalDamage = result.HpDamage;
                    absorptionRatio = result.AbsorptionRatio;

                    var armor = ArmorSystem.GetArmorForHit(armorSlots, isHeadshot);
                    if (armor != null && !armor.IsBroken)
                    {
                        ArmorSystem.ApplyDurabilityDamage(armor, result.ArmorDurDamage);

                        if (armor.IsBroken)
                            context.Events.ArmorBroken(hit.TargetId, isHelmet: isHeadshot);
                    }
                }

                ApplyDamage(health, finalDamage);

                // Bleed roll (ignores armor, per hit signal = per pellet for shotgun)
                if (health.IsAlive && hit.BleedChance > 0f)
                {
                    float bleedRoll = randomProvider != null ? randomProvider() : Random.value;
                    if (bleedRoll < hit.BleedChance)
                    {
                        StatusEffectSystem.ApplyEffect(state, hit.TargetId, StatusEffectType.Bleeding);
                        context.Events.StatusEffectApplied(hit.TargetId, "Bleeding");
                    }
                }

                if (health.IsAlive)
                    context.Events.EntityDamaged(hit.TargetId, health.CurrentHp, health.MaxHp);
                else
                    context.Events.EntityDied(
                        hit.TargetId,
                        projectile != null ? projectile.OwnerId  : default,
                        hit.HitPoint,
                        projectile != null ? projectile.Direction : Vector3.forward,
                        finalDamage);

                // Per-target view feedback (flash, future blood/decal). Fires regardless of owner.
                var hitDir = projectile != null ? projectile.Direction : Vector3.forward;
                context.Events.EntityHit(
                    targetEid:           hit.TargetId,
                    hitPoint:            hit.HitPoint,
                    projectileDirection: hitDir,
                    isHeadshot:          isHeadshot,
                    isRicochet:          false,
                    isKill:              !health.IsAlive,
                    absorptionRatio:     absorptionRatio);

                if (projectile != null && state.PlayerEntity != null
                    && projectile.OwnerId == state.PlayerEntity.Id)
                {
                    context.Events.HitConfirmed(isKill: !health.IsAlive, isHeadshot: isHeadshot,
                        absorptionRatio: absorptionRatio);
                    context.Events.DamageNumberSpawned(hit.HitPoint, finalDamage, isHeadshot, !health.IsAlive,
                        hitDir,
                        absorptionRatio: absorptionRatio);
                }

                for (int i = state.Projectiles.Count - 1; i >= 0; i--)
                {
                    var proj = state.Projectiles[i];
                    if (proj.Id == hit.ProjectileId)
                    {
                        string hitBase = hit.TargetedEntityId == hit.TargetId
                            && hit.TargetedEntityId.Value != 0
                                ? "head" : "body";
                        string hitType = $"{hitBase}:{absorptionRatio:F2}";
                        // Normal not relevant for character hits (presenters skip them — blood
                        // particles cover via BodyImpact prefab). Pass zero — bullet hole presenter
                        // filters out non-surface hitTypes.
                        context.Events.ProjectileHit(hit.ProjectileId, hit.HitPoint, Vector3.zero, hitType);
                        context.Events.ProjectileDespawned(hit.ProjectileId);
                        state.Projectiles.RemoveAt(i);
                        break;
                    }
                }
            }
        }

        static bool IsRolling(RaidState state, EId targetId)
        {
            if (state.PlayerEntity != null && targetId == state.PlayerEntity.Id)
                return state.PlayerEntity.IsRolling;

            for (int i = 0; i < state.Bots.Count; i++)
            {
                if (state.Bots[i].Id == targetId)
                    return state.Bots[i].IsRolling;
            }

            return false;
        }

        public static void ApplyDamage(HealthState health, float damage)
        {
            if (!health.IsAlive) return;

            health.CurrentHp = Mathf.Max(0f, health.CurrentHp - damage);

            if (health.CurrentHp <= 0f)
                health.IsAlive = false;
        }
    }
}
