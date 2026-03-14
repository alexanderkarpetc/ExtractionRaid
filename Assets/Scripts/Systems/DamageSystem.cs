using System.Collections.Generic;
using Session;
using State;
using UnityEngine;

namespace Systems
{
    public static class DamageSystem
    {
        public static void Tick(RaidState state, List<HitSignal> hits, in RaidContext context)
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

                ApplyDamage(health, hit.Damage);

                if (health.IsAlive)
                    context.Events.EntityDamaged(hit.TargetId, health.CurrentHp, health.MaxHp);
                else
                    context.Events.EntityDied(hit.TargetId);

                if (projectile != null && state.PlayerEntity != null
                    && projectile.OwnerId == state.PlayerEntity.Id)
                {
                    context.Events.HitConfirmed(isKill: !health.IsAlive);
                }

                for (int i = state.Projectiles.Count - 1; i >= 0; i--)
                {
                    if (state.Projectiles[i].Id == hit.ProjectileId)
                    {
                        context.Events.ProjectileHit(hit.ProjectileId, state.Projectiles[i].Position);
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
