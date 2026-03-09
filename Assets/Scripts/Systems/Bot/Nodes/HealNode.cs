using Constants;
using Session;
using State;
using Systems.Bot.BT;

namespace Systems.Bot.Nodes
{
    public class HealNode : IBTNode
    {
        public BTStatus Tick(BotEntityState bot, RaidState state, in RaidContext ctx, in BotTypeConfig config)
        {
            if (!state.HealthMap.TryGetValue(bot.Id, out var health))
                return BTStatus.Failure;

            if (!health.IsAlive)
                return BTStatus.Failure;

            var hpRatio = health.CurrentHp / health.MaxHp;
            if (hpRatio > config.HealThreshold)
                return BTStatus.Failure;

            bot.WantsToHeal = true;
            return BTStatus.Success;
        }
    }
}
