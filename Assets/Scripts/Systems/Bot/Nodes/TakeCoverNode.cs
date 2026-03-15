using Constants;
using Session;
using State;
using Systems.Bot.BT;
using UnityEngine;

namespace Systems.Bot.Nodes
{
    public class TakeCoverNode : IBTNode
    {
        public string Name => "Take Cover";

        public BTStatus Tick(BotEntityState bot, RaidState state, in RaidContext ctx, in BotTypeConfig config)
        {
            var bb = bot.Blackboard;
            if (!bb.HasTarget)
                return this.Traced(bot, BTStatus.Failure);

            if (!bb.HasCover)
            {
                var awayFromTarget = (bot.Position - bb.LastKnownTargetPos).normalized;
                var coverCandidate = bot.Position + awayFromTarget * BotConstants.CoverSearchRadius * 0.5f;

                if (ctx.NavMesh != null &&
                    ctx.NavMesh.SamplePosition(coverCandidate, BotConstants.CoverSearchRadius, out var coverPos))
                {
                    bb.CoverPosition = coverPos;
                    bb.HasCover = true;
                }
                else
                {
                    return this.Traced(bot, BTStatus.Failure);
                }
            }

            bb.DebugStatus = "Cover";
            var toCover = bb.CoverPosition - bot.Position;
            toCover.y = 0f;
            var dist = toCover.magnitude;

            if (dist < BotConstants.MinCoverDistance)
            {
                bot.DesiredVelocity = Vector3.zero;
                return this.Traced(bot, BTStatus.Success);
            }

            bot.DesiredVelocity = (toCover / dist) * config.ChaseSpeed;
            return this.Traced(bot, BTStatus.Running);
        }
    }
}
