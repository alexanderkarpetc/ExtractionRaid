using Constants;
using Session;
using State;
using Systems.Bot.BT;

namespace Systems.Bot.Nodes
{
    public class ShootNode : IBTNode
    {
        public BTStatus Tick(BotEntityState bot, RaidState state, in RaidContext ctx, in BotTypeConfig config)
        {
            var bb = bot.Blackboard;
            if (!bb.HasTarget || !bb.CanSeeTarget)
                return BTStatus.Failure;

            if (bb.DistanceToTarget > config.EngageRange)
                return BTStatus.Failure;

            if (bb.ReactionTimer < config.ReactionTime)
            {
                bb.DebugStatus = "Reacting...";
                bb.ReactionTimer += ctx.DeltaTime;
                return BTStatus.Running;
            }

            bb.DebugStatus = "Shoot";
            bot.DesiredAimPoint = bb.LastKnownTargetPos;
            bot.WantsToFire = true;
            return BTStatus.Success;
        }
    }
}
