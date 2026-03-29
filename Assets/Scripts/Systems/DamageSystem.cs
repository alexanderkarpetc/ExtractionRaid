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
                    if (ArmorSystem.ShouldRicochet(helmet, hit.Penetration, ricochetRoll))
                    {
                        // Ricochet: 0 HP damage, full durability damage (absorptionRatio = 1)
                        float armorDurDmg = ArmorSystem.CalcArmorDurabilityDamage(hit.ArmorDamage, 1f);
                        ArmorSystem.ApplyDurabilityDamage(helmet, armorDurDmg);

                        if (helmet.IsBroken)
                            context.Events.ArmorBroken(hit.TargetId, isHelmet: true);

                        context.Events.ProjectileRicochet(hit.ProjectileId, hit.HitPoint,
                            projectile != null ? projectile.Direction : Vector3.forward);

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
                if (state.ArmorMap.TryGetValue(hit.TargetId, out var armorSlots))
                {
                    var result = ArmorSystem.Calculate(damage, hit.Penetration, hit.ArmorDamage,
                        armorSlots, isHeadshot);
                    finalDamage = result.HpDamage;

                    var armor = ArmorSystem.GetArmorForHit(armorSlots, isHeadshot);
                    if (armor != null && !armor.IsBroken)
                    {
                        ArmorSystem.ApplyDurabilityDamage(armor, result.ArmorDurDamage);

                        if (armor.IsBroken)
                            context.Events.ArmorBroken(hit.TargetId, isHelmet: isHeadshot);
                    }
                }

                ApplyDamage(health, finalDamage);

                if (health.IsAlive)
                    context.Events.EntityDamaged(hit.TargetId, health.CurrentHp, health.MaxHp);
                else
                    context.Events.EntityDied(hit.TargetId);

                if (projectile != null && state.PlayerEntity != null
                    && projectile.OwnerId == state.PlayerEntity.Id)
                {
                    context.Events.HitConfirmed(isKill: !health.IsAlive, isHeadshot: isHeadshot);
                    context.Events.DamageNumberSpawned(hit.HitPoint, finalDamage, isHeadshot, !health.IsAlive,
                        projectile != null ? projectile.Direction : Vector3.forward);
                }

                for (int i = state.Projectiles.Count - 1; i >= 0; i--)
                {
                    var proj = state.Projectiles[i];
                    if (proj.Id == hit.ProjectileId)
                    {
                        string hitType = hit.TargetedEntityId == hit.TargetId
                            && hit.TargetedEntityId.Value != 0
                                ? "head" : "body";
                        context.Events.ProjectileHit(hit.ProjectileId, hit.HitPoint, hitType);
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
