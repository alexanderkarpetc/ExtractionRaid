using System;
using Constants;
using Session;
using State;

namespace Systems.Bot.BT
{
    public class BTCondition : IBTNode
    {
        readonly Func<BotEntityState, RaidState, BotTypeConfig, bool> _predicate;

        public BTCondition(Func<BotEntityState, RaidState, BotTypeConfig, bool> predicate)
        {
            _predicate = predicate;
        }

        public BTStatus Tick(BotEntityState bot, RaidState state, in RaidContext ctx, in BotTypeConfig config)
        {
            return _predicate(bot, state, config) ? BTStatus.Success : BTStatus.Failure;
        }
    }
}
