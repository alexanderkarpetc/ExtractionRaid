using System.Collections.Generic;
using Constants;
using Session;
using State;

namespace Systems.Bot.BT
{
    public class BTSequence : IBTNode
    {
        public string Name { get; }
        readonly IBTNode[] _children;
        public IReadOnlyList<IBTNode> Children => _children;

        public BTSequence(string name, params IBTNode[] children)
        {
            Name = name;
            _children = children;
        }

        public BTSequence(params IBTNode[] children) : this("Sequence", children) { }

        public BTStatus Tick(BotEntityState bot, RaidState state, in RaidContext ctx, in BotTypeConfig config)
        {
            for (int i = 0; i < _children.Length; i++)
            {
                var status = _children[i].Tick(bot, state, in ctx, in config);
                if (status != BTStatus.Success)
                    return this.Traced(bot, status);
            }
            return this.Traced(bot, BTStatus.Success);
        }
    }
}
