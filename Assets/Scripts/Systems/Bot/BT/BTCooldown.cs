using System;
using Constants;
using Session;
using State;

namespace Systems.Bot.BT
{
    public class BTCooldown : IBTNode
    {
        public string Name { get; }
        readonly IBTNode _child;
        public IBTNode Child => _child;
        readonly Func<BotBlackboard, float> _getTimer;
        readonly Action<BotBlackboard, float> _setTimer;
        readonly float _duration;

        public BTCooldown(string name, IBTNode child, float duration,
            Func<BotBlackboard, float> getTimer, Action<BotBlackboard, float> setTimer)
        {
            Name = name;
            _child = child;
            _duration = duration;
            _getTimer = getTimer;
            _setTimer = setTimer;
        }

        public BTCooldown(IBTNode child, float duration,
            Func<BotBlackboard, float> getTimer, Action<BotBlackboard, float> setTimer)
            : this($"CD ({duration:F0}s)", child, duration, getTimer, setTimer) { }

        public BTStatus Tick(BotEntityState bot, RaidState state, in RaidContext ctx, in BotTypeConfig config)
        {
            var timer = _getTimer(bot.Blackboard);
            if (timer > 0f)
            {
                _setTimer(bot.Blackboard, timer - ctx.DeltaTime);
                return this.Traced(bot, BTStatus.Failure);
            }

            var status = _child.Tick(bot, state, in ctx, in config);
            if (status == BTStatus.Success)
                _setTimer(bot.Blackboard, _duration);

            return this.Traced(bot, status);
        }
    }
}
