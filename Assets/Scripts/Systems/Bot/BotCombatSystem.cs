using Constants;
using Session;
using State;
using UnityEngine;

namespace Systems.Bot
{
    public static class BotCombatSystem
    {
        public static void Tick(RaidState state, in RaidContext ctx)
        {
            for (int i = 0; i < state.Bots.Count; i++)
            {
                var bot = state.Bots[i];

                if (!state.HealthMap.TryGetValue(bot.Id, out var hp) || !hp.IsAlive)
                    continue;

                if (!BotConstants.TryGetConfig(bot.TypeId, out var config))
                    continue;

                if (bot.WantsToHeal)
                    ProcessHeal(bot, hp, config);

                if (bot.WantsToFire)
                    ProcessFire(bot, state, in ctx, in config);

                if (bot.WantsToThrowGrenade)
                    ProcessThrowGrenade(bot, state, in ctx);
            }
        }

        static void ProcessHeal(BotEntityState bot, HealthState hp, in BotTypeConfig config)
        {
            hp.CurrentHp = Mathf.Min(hp.CurrentHp + config.HealAmount, hp.MaxHp);
            bot.Blackboard.MedkitsRemaining--;
        }

        static void ProcessFire(BotEntityState bot, RaidState state, in RaidContext ctx, in BotTypeConfig config)
        {
            var weapon = bot.Weapon;
            if (weapon == null) return;

            if (state.ElapsedTime - weapon.LastFireTime < weapon.FireInterval) return;

            var aimDir = (bot.DesiredAimPoint - bot.Position).normalized;
            if (aimDir.sqrMagnitude < 0.001f) return;

            bot.AimDirection = aimDir;

            var spawnPos = bot.Position + aimDir * 0.5f + Vector3.up * 1.2f;
            var count = Mathf.Max(1, weapon.ProjectilesPerShot);
            var halfSpread = weapon.SpreadAngle * 0.5f;

            float accuracySpread = (1f - config.Accuracy) * 10f;

            for (int i = 0; i < count; i++)
            {
                var pelletDir = aimDir;

                if (halfSpread > 0f)
                    pelletDir = Quaternion.Euler(0f, Random.Range(-halfSpread, halfSpread), 0f) * pelletDir;

                if (accuracySpread > 0f)
                    pelletDir = Quaternion.Euler(
                        Random.Range(-accuracySpread, accuracySpread),
                        Random.Range(-accuracySpread, accuracySpread), 0f) * pelletDir;

                var projectileId = state.AllocateEId();
                var projectile = ProjectileEntityState.Create(
                    projectileId, bot.Id, spawnPos, pelletDir.normalized,
                    weapon.ProjectileSpeed, state.ElapsedTime,
                    weapon.ProjectileLifetime, weapon.ProjectileDamage);

                state.Projectiles.Add(projectile);
                ctx.Events.ProjectileSpawned(projectileId, spawnPos, pelletDir.normalized, weapon.ProjectileDamage);
            }

            weapon.LastFireTime = state.ElapsedTime;
        }

        static void ProcessThrowGrenade(BotEntityState bot, RaidState state, in RaidContext ctx)
        {
            var target = bot.GrenadeThrowTarget;
            var toTarget = target - bot.Position;
            toTarget.y = 0f;
            float dist = toTarget.magnitude;
            dist = Mathf.Clamp(dist, GrenadeConstants.MinThrowRange, GrenadeConstants.MaxThrowRange);

            var horizontalDir = dist > 0.001f ? toTarget / dist : bot.FacingDirection;
            horizontalDir.y = 0f;
            if (horizontalDir.sqrMagnitude < 0.001f)
                horizontalDir = Vector3.forward;
            horizontalDir.Normalize();

            var velocity = GrenadeSystem.ComputeThrowVelocity(horizontalDir, dist);
            var spawnPos = bot.Position + Vector3.up * GrenadeConstants.LaunchHeight + horizontalDir * 0.5f;

            var id = state.AllocateEId();
            var grenade = GrenadeEntityState.Create(
                id, bot.Id, state.ElapsedTime,
                GrenadeConstants.FuseTime, GrenadeConstants.Damage, GrenadeConstants.ExplosionRadius);

            state.Grenades.Add(grenade);
            ctx.Events.GrenadeSpawned(id, spawnPos, velocity);

            bot.Blackboard.GrenadesRemaining--;
        }
    }
}
