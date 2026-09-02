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

                TickReload(bot, state, in config);
                TickHealCast(bot, hp, state, in config);

                if (bot.WantsToHeal)
                    ProcessHeal(bot, state, in config);

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

                if (bot.WantsToMeleeAttack && !staggered)
                    ProcessMeleeAttack(bot, state, in ctx, in config);
            }
        }

        static void ProcessMeleeAttack(BotEntityState bot, RaidState state, in RaidContext ctx, in BotTypeConfig config)
        {
            // BTCooldown around MeleeAttackNode owns rate-limiting; here we just
            // resolve the swing into damage on whatever the bot's current target is.
            var targetId = bot.Blackboard.TargetEId;
            if (targetId == default) return;
            if (!state.HealthMap.TryGetValue(targetId, out var hp) || !hp.IsAlive) return;

            var hitPoint = Vector3.Lerp(bot.Position, bot.Blackboard.LastKnownTargetPos, 0.5f);
            var hitDir   = (bot.Blackboard.LastKnownTargetPos - bot.Position);
            hitDir.y = 0f;
            if (hitDir.sqrMagnitude < 0.0001f) hitDir = bot.FacingDirection;
            hitDir.Normalize();

            DamageSystem.ApplyMeleeDamage(state, targetId, config.MeleeAttackDamage,
                attackerId: bot.Id, hitPoint: hitPoint, hitDirection: hitDir, in ctx);
        }

        /// <summary>
        /// Magazine + reload state for bot weapons (infinite reserves). Runs every tick
        /// so a reload completes even when the bot isn't trying to fire. Also starts a
        /// tactical reload when the mag runs low and the target is out of sight — like
        /// a player topping up between peeks. FireForward test turrets opt out.
        /// </summary>
        static void TickReload(BotEntityState bot, RaidState state, in BotTypeConfig config)
        {
            var weapon = bot.Weapon;
            if (weapon == null || config.Has(BotBehaviorFlags.FireForward)) return;
            if (weapon.Stats.MagazineSize <= 0) return; // legacy/degenerate stats → infinite mag

            if (weapon.Phase == WeaponPhase.Reloading)
            {
                if (state.ElapsedTime - weapon.PhaseStartTime >= weapon.Stats.ReloadTime)
                {
                    weapon.AmmoInMagazine = weapon.Stats.MagazineSize;
                    weapon.Phase = WeaponPhase.Ready;
                    weapon.PhaseStartTime = state.ElapsedTime;
                }
                return;
            }

            var bb = bot.Blackboard;
            bool magEmpty = weapon.AmmoInMagazine <= 0;
            bool lowAndSafe = weapon.AmmoInMagazine < weapon.Stats.MagazineSize * BotConstants.TacticalReloadFraction
                              && !bb.CanSeeTarget;
            if (magEmpty || lowAndSafe)
            {
                weapon.Phase = WeaponPhase.Reloading;
                weapon.PhaseStartTime = state.ElapsedTime;
            }
        }

        /// <summary>
        /// Completes a running heal cast (started by ProcessHeal). Heals by
        /// config.HealAmount — not to full — so a mag-dumped bot stays hurt.
        /// </summary>
        static void TickHealCast(BotEntityState bot, HealthState hp, RaidState state, in BotTypeConfig config)
        {
            var bb = bot.Blackboard;
            if (bb.HealCastEndTime < 0f || state.ElapsedTime < bb.HealCastEndTime) return;

            float amount = config.HealAmount > 0f ? config.HealAmount : hp.MaxHp * 0.5f;
            hp.CurrentHp = Mathf.Min(hp.MaxHp, hp.CurrentHp + amount);
            bb.HealCastEndTime = -1f;
            bb.TimeSinceTargetSeen = 0f;
        }

        static void ProcessHeal(BotEntityState bot, RaidState state, in BotTypeConfig config)
        {
            var bb = bot.Blackboard;
            if (bb.HealCastEndTime >= 0f) return; // already casting

            // Commit the medkit up front and start the cast; HealNode holds the bot in
            // a retreating, non-firing state until TickHealCast applies the HP.
            bb.HealCastEndTime = state.ElapsedTime + BotConstants.HealCastTime;
            bb.MedkitsRemaining--;
        }

        static void ProcessFire(BotEntityState bot, RaidState state, in RaidContext ctx, in BotTypeConfig config)
        {
            var weapon = bot.Weapon;
            if (weapon == null) return;

            if (weapon.Phase == WeaponPhase.Reloading) return;
            bool tracksAmmo = !config.Has(BotBehaviorFlags.FireForward) && weapon.Stats.MagazineSize > 0;
            if (tracksAmmo && weapon.AmmoInMagazine <= 0) return; // TickReload starts the reload

            if (state.ElapsedTime - weapon.LastFireTime < weapon.Stats.FireInterval) return;

            var aimDir = (bot.DesiredAimPoint - bot.Position).normalized;
            if (aimDir.sqrMagnitude < 0.001f) return;

            bot.AimDirection = aimDir;

            var spawnPos = bot.Position + aimDir * 0.5f + Vector3.up * 1.2f;
            var count = Mathf.Max(1, weapon.Stats.ProjectilesPerShot);
            var halfSpread = weapon.Stats.SpreadAngle * 0.5f;

            // ShootNode publishes settle/movement/pressure-adjusted accuracy; nodes that
            // bypass it (FireForward, tests setting WantsToFire directly) fall back to raw config.
            float accuracy = bot.Blackboard.EffectiveAccuracy > 0f
                ? bot.Blackboard.EffectiveAccuracy
                : config.Accuracy;
            float accuracySpread = (1f - accuracy) * 10f;

            // Bleed parity з player: bots не consume ammo, але читаємо BleedChance з compatible
            // AmmoType (Ammo_Rifle / Ammo_EnergyCell) щоб baseline 5% з ItemDefinition застосовувався
            // і на bot shots. Інші ammo модифікатори (Penetration / Damage / ArmorDamage) свідомо
            // не додаємо — design: bot стати композуються тільки з payload core (див. Tier 4a comment нижче).
            float ammoBleedChance = 0f;
            if (!string.IsNullOrEmpty(weapon.AmmoType))
            {
                var ammoDef = ItemDefinition.Get(weapon.AmmoType);
                if (ammoDef != null) ammoBleedChance = ammoDef.BleedChance;
            }
            float totalBleedChance = weapon.Stats.BaseBleedChance + ammoBleedChance;

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
                // Tier 4a — pass full Builder-derived stats: HeadshotMultiplier, Penetration,
                // ArmorDamage, BleedChance — так bot shots interact з player armor / headshot
                // pipeline ідентично як player shots interact з bot armor. Pre-migration bots
                // had Penetration=0 → завжди absorbed by armor. (No ammo modifiers — bots
                // не manage AmmoState; стат composer на base values з Payload core.)
                var projectile = ProjectileEntityState.Create(
                    projectileId, bot.Id, spawnPos, pelletDir.normalized,
                    weapon.Stats.ProjectileSpeed * ctx.ShootingConfig.ProjectileSpeedMultiplier,
                    state.ElapsedTime,
                    weapon.Stats.ProjectileLifetime,
                    weapon.Stats.Damage * ctx.ShootingConfig.DamageMultiplier,
                    headshotDamageMultiplier: weapon.Stats.HeadshotDamageMultiplier,
                    targetedEntityId: default,
                    penetration:      weapon.Stats.BasePenetration,
                    armorDamage:      weapon.Stats.BaseArmorDamage,
                    bleedChance:      totalBleedChance,
                    archetype:        PayloadArchetypeKeyExt.FromArchetypeString(weapon.PayloadDefinition?.Archetype));

                state.Projectiles.Add(projectile);
                ctx.Events.ProjectileSpawned(projectileId, spawnPos, pelletDir.normalized,
                    weapon.Stats.Damage, weapon.PayloadDefinition?.Archetype);
            }

            weapon.LastFireTime = state.ElapsedTime;
            ctx.Events.WeaponFired(bot.Id, spawnPos, aimDir, weapon.PayloadDefinition?.Archetype,
                deliveryPattern: weapon.DeliveryDefinition?.Pattern ?? FiringPattern.Single);

            if (tracksAmmo)
                weapon.AmmoInMagazine--;

            // Burst bookkeeping: count the shot; when the burst is spent, roll the pause
            // before the next one (aggressive personalities pause less).
            var bb = bot.Blackboard;
            if (bb.BurstShotsLeft > 0)
            {
                bb.BurstShotsLeft--;
                if (bb.BurstShotsLeft <= 0)
                    bb.NextBurstTime = state.ElapsedTime
                        + Random.Range(BotConstants.BurstPauseMin, BotConstants.BurstPauseMax)
                          / Mathf.Max(0.1f, bb.Aggression);
            }
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
