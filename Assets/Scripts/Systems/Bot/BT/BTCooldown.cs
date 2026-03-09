using System;
using Constants;
using Session;
using State;

namespace Systems.Bot.BT
{
    public class BTCooldown : IBTNode
    {
        readonly IBTNode _child;
        readonly Func<BotBlackboard, float> _getTimer;
        readonly Action<BotBlackboard, float> _setTimer;
        readonly float _duration;

        public BTCooldown(IBTNode child, float duration,
            Func<BotBlackboard, float> getTimer, Action<BotBlackboard, float> setTimer)
        {
            _child = child;
            _duration = duration;
            _getTimer = getTimer;
            _setTimer = setTimer;
        }

        public BTStatus Tick(BotEntityState bot, RaidState state, in RaidContext ctx, in BotTypeConfig config)
        {
            var timer = _getTimer(bot.Blackboard);
            if (timer > 0f)
            {
                _setTimer(bot.Blackboard, timer - ctx.DeltaTime);
                return BTStatus.Failure;
            }

            var status = _child.Tick(bot, state, in ctx, in config);
            if (status == BTStatus.Success)
                _setTimer(bot.Blackboard, _duration);

            return status;
        }
    }
}
