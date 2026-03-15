using System;
using Constants;
using Session;
using State;

namespace Systems.Bot.BT
{
    public class BTCondition : IBTNode
    {
        public string Name { get; }
        readonly Func<BotEntityState, RaidState, BotTypeConfig, bool> _predicate;

        public BTCondition(string name, Func<BotEntityState, RaidState, BotTypeConfig, bool> predicate)
        {
            Name = name;
            _predicate = predicate;
        }

        public BTCondition(Func<BotEntityState, RaidState, BotTypeConfig, bool> predicate)
            : this("Condition", predicate) { }

        public BTStatus Tick(BotEntityState bot, RaidState state, in RaidContext ctx, in BotTypeConfig config)
        {
            var result = _predicate(bot, state, config) ? BTStatus.Success : BTStatus.Failure;
            return this.Traced(bot, result);
        }
    }
}
