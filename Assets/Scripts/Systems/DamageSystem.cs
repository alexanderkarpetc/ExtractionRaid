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
                if (!state.HealthMap.TryGetValue(hit.TargetId, out var health))
                    continue;

                if (!health.IsAlive) continue;

                ApplyDamage(health, hit.Damage);

                if (health.IsAlive)
                {
                    context.Events.DestructibleDamaged(hit.TargetId, health.CurrentHp, health.MaxHp);
                }
                else
                {
                    context.Events.DestructibleDestroyed(hit.TargetId);
                }

                // Consume the projectile
                for (int i = state.Projectiles.Count - 1; i >= 0; i--)
                {
                    if (state.Projectiles[i].Id == hit.ProjectileId)
                    {
                        context.Events.ProjectileDespawned(hit.ProjectileId);
                        state.Projectiles.RemoveAt(i);
                        break;
                    }
                }
            }
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
