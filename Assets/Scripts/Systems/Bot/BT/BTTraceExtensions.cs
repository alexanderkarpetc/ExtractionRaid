using State;

namespace Systems.Bot.BT
{
    public static class BTTraceExtensions
    {
        public static BTStatus Traced(this IBTNode node, BotEntityState bot, BTStatus status)
        {
            bot.Blackboard.Trace?.Record(node, (int)status);
            return status;
        }
    }
}
