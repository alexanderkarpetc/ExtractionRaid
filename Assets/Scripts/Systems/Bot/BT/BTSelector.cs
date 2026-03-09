using Constants;
using Session;
using State;

namespace Systems.Bot.BT
{
    public class BTSelector : IBTNode
    {
        readonly IBTNode[] _children;

        public BTSelector(params IBTNode[] children)
        {
            _children = children;
        }

        public BTStatus Tick(BotEntityState bot, RaidState state, in RaidContext ctx, in BotTypeConfig config)
        {
            for (int i = 0; i < _children.Length; i++)
            {
                var status = _children[i].Tick(bot, state, in ctx, in config);
                if (status != BTStatus.Failure)
                    return status;
            }
            return BTStatus.Failure;
        }
    }
}
