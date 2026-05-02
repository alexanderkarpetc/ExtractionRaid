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

                // Stagger lockout (B.4): bot cannot fire while staggered. Headshots are
                // longest stagger — gives player meaningful counter-play. AI keeps WantsToFire
                // intent for next tick after lockout expires.
                bool staggered = ctx.StaggerConfig.Enabled
                                 && ctx.StaggerConfig.AIShootingLockout
                                 && bot.StaggerEndTime > ctx.Time.Time;
                if (bot.WantsToFire && !staggered)
                    ProcessFire(bot, state, in ctx, in config);

                if (bot.WantsToThrowGrenade && !staggered)
                    ProcessThrowGrenade(bot, state, in ctx);
            }
        }

        static void ProcessHeal(BotEntityState bot, HealthState hp, in BotTypeConfig config)
        {
            hp.CurrentHp = hp.MaxHp;
            bot.Blackboard.MedkitsRemaining--;
            bot.Blackboard.TimeSinceTargetSeen = 0f;
        }

        static void ProcessFire(BotEntityState bot, RaidState state, in RaidContext ctx, in BotTypeConfig config)
        {
            var weapon = bot.Weapon;
            if (weapon == null) return;

            if (state.ElapsedTime - weapon.LastFireTime < weapon.Stats.FireInterval) return;

            var aimDir = (bot.DesiredAimPoint - bot.Position).normalized;
            if (aimDir.sqrMagnitude < 0.001f) return;

            bot.AimDirection = aimDir;

            var spawnPos = bot.Position + aimDir * 0.5f + Vector3.up * 1.2f;
            var count = Mathf.Max(1, weapon.Stats.ProjectilesPerShot);
            var halfSpread = weapon.Stats.SpreadAngle * 0.5f;

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
                    weapon.Stats.ProjectileSpeed * ctx.ShootingConfig.ProjectileSpeedMultiplier,
                    state.ElapsedTime,
                    weapon.Stats.ProjectileLifetime,
                    weapon.Stats.Damage * ctx.ShootingConfig.DamageMultiplier);

                state.Projectiles.Add(projectile);
                ctx.Events.ProjectileSpawned(projectileId, spawnPos, pelletDir.normalized, weapon.Stats.Damage);
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
