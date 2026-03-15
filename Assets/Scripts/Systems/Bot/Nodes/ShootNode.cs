using Constants;
using Session;
using State;
using Systems.Bot.BT;

namespace Systems.Bot.Nodes
{
    public class ShootNode : IBTNode
    {
        public string Name => "Shoot";

        public BTStatus Tick(BotEntityState bot, RaidState state, in RaidContext ctx, in BotTypeConfig config)
        {
            var bb = bot.Blackboard;
            if (!bb.HasTarget || !bb.CanSeeTarget)
                return this.Traced(bot, BTStatus.Failure);

            if (bb.DistanceToTarget > config.EngageRange)
                return this.Traced(bot, BTStatus.Failure);

            if (bb.ReactionTimer < config.ReactionTime)
            {
                bb.DebugStatus = "Reacting...";
                bb.ReactionTimer += ctx.DeltaTime;
                return this.Traced(bot, BTStatus.Running);
            }

            bb.DebugStatus = "Shoot";
            bot.DesiredAimPoint = bb.LastKnownTargetPos;
            bot.WantsToFire = true;
            return this.Traced(bot, BTStatus.Success);
        }
    }
}
