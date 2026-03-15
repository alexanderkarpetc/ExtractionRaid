using Constants;
using Session;
using State;
using Systems.Bot.BT;
using UnityEngine;

namespace Systems.Bot.Nodes
{
    public class ChaseNode : IBTNode
    {
        public string Name => "Chase";

        public BTStatus Tick(BotEntityState bot, RaidState state, in RaidContext ctx, in BotTypeConfig config)
        {
            var bb = bot.Blackboard;
            if (!bb.HasTarget)
                return this.Traced(bot, BTStatus.Failure);

            var toTarget = bb.LastKnownTargetPos - bot.Position;
            toTarget.y = 0f;
            var dist = toTarget.magnitude;

            if (dist < 1f)
            {
                bot.DesiredVelocity = Vector3.zero;
                return this.Traced(bot, BTStatus.Success);
            }

            bb.DebugStatus = "Chase";
            bot.DesiredVelocity = (toTarget / dist) * config.ChaseSpeed;
            return this.Traced(bot, BTStatus.Running);
        }
    }
}
