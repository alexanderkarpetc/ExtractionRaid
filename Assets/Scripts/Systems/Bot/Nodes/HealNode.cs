using Constants;
using Session;
using State;
using Systems.Bot.BT;

namespace Systems.Bot.Nodes
{
    public class HealNode : IBTNode
    {
        public string Name => "Heal";

        public BTStatus Tick(BotEntityState bot, RaidState state, in RaidContext ctx, in BotTypeConfig config)
        {
            if (!state.HealthMap.TryGetValue(bot.Id, out var health))
                return this.Traced(bot, BTStatus.Failure);

            if (!health.IsAlive)
                return this.Traced(bot, BTStatus.Failure);

            var bb = bot.Blackboard;

            if (bb.MedkitsRemaining <= 0)
                return this.Traced(bot, BTStatus.Failure);

            if (bb.HealCooldownTimer > 0f)
            {
                bb.HealCooldownTimer -= ctx.DeltaTime;
                return this.Traced(bot, BTStatus.Failure);
            }

            float hpRatio = health.CurrentHp / health.MaxHp;
            float timeSinceDamage = state.ElapsedTime - bb.LastDamageTime;

            if (hpRatio < config.EmergencyHealThreshold
                && timeSinceDamage > config.EmergencyHealDelay)
            {
                bot.WantsToHeal = true;
                bb.HealCooldownTimer = config.EmergencyHealCooldown;
                bb.DebugStatus = "Emergency Heal";
                return this.Traced(bot, BTStatus.Success);
            }

            if (hpRatio < config.HealThreshold
                && timeSinceDamage > config.HealSafeDelay
                && !bb.CanSeeTarget
                && bb.DistanceToTarget > config.HealSafeEnemyDistance
                && !IsReloading(bot))
            {
                bot.WantsToHeal = true;
                bb.HealCooldownTimer = config.HealCooldown;
                bb.DebugStatus = "Heal";
                return this.Traced(bot, BTStatus.Success);
            }

            return this.Traced(bot, BTStatus.Failure);
        }

        static bool IsReloading(BotEntityState bot)
        {
            return bot.Weapon != null && bot.Weapon.Phase == WeaponPhase.Reloading;
        }
    }
}
