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

            var hpRatio = health.CurrentHp / health.MaxHp;
            if (hpRatio > config.HealThreshold)
                return this.Traced(bot, BTStatus.Failure);

            bot.Blackboard.DebugStatus = "Heal";
            bot.WantsToHeal = true;
            return this.Traced(bot, BTStatus.Success);
        }
    }
}
