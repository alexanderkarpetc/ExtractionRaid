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
            }
        }

        static void ProcessHeal(BotEntityState bot, HealthState hp, in BotTypeConfig config)
        {
            hp.CurrentHp = Mathf.Min(hp.CurrentHp + config.HealAmount, hp.MaxHp);
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
    }
}
