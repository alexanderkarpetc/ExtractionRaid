using Constants;
using Session;
using State;

namespace Systems.Bot.BT
{
    public class BTSequence : IBTNode
    {
        readonly IBTNode[] _children;

        public BTSequence(params IBTNode[] children)
        {
            _children = children;
        }

        public BTStatus Tick(BotEntityState bot, RaidState state, in RaidContext ctx, in BotTypeConfig config)
        {
            for (int i = 0; i < _children.Length; i++)
            {
                var status = _children[i].Tick(bot, state, in ctx, in config);
                if (status != BTStatus.Success)
                    return status;
            }
            return BTStatus.Success;
        }
    }
}
