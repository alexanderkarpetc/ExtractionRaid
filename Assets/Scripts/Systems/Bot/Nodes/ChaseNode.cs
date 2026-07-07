using Constants;
using Session;
using State;
using Systems.Bot.BT;
using UnityEngine;

namespace Systems.Bot.Nodes
{
    /// <summary>
    /// Move toward the last known target position along NavMesh path corners
    /// (straight-line steering fallback when no navmesh is available). The old
    /// straight-line-only chase ground bots along wall faces like Roombas.
    ///
    /// On arrival: Success if the target is visible (hold position, ShootNode owns
    /// the engagement), Failure if not — the parent selector falls through to
    /// <see cref="SearchNode"/> instead of freezing at an empty spot.
    /// </summary>
    public class ChaseNode : IBTNode
    {
        public string Name => "Chase";

        public BTStatus Tick(BotEntityState bot, RaidState state, in RaidContext ctx, in BotTypeConfig config)
        {
            var bb = bot.Blackboard;
            if (!bb.HasTarget)
                return this.Traced(bot, BTStatus.Failure);

            var target = bb.LastKnownTargetPos;
            var toTarget = target - bot.Position;
            toTarget.y = 0f;
            var dist = toTarget.magnitude;

            if (dist < BotConstants.ChaseArriveDistance)
            {
                bot.DesiredVelocity = Vector3.zero;
                return this.Traced(bot, bb.CanSeeTarget ? BTStatus.Success : BTStatus.Failure);
            }

            bb.DebugStatus = "Chase";

            var steerTarget = target;
            if (ctx.NavMesh != null)
            {
                EnsurePath(bot, bb, in ctx, target);
                steerTarget = CurrentSteerTarget(bot, bb, target);
            }

            var toSteer = steerTarget - bot.Position;
            toSteer.y = 0f;
            if (toSteer.sqrMagnitude < 0.0001f)
                toSteer = toTarget;

            bot.DesiredVelocity = toSteer.normalized * config.ChaseSpeed;
            return this.Traced(bot, BTStatus.Running);
        }

        static void EnsurePath(BotEntityState bot, BotBlackboard bb, in RaidContext ctx, Vector3 target)
        {
            bb.ChaseRepathTimer -= ctx.DeltaTime;

            bool pathValid = bb.ChasePathCornerCount > 0
                             && bb.ChasePathCornerIndex < bb.ChasePathCornerCount;
            bool targetMoved = (target - bb.ChasePathTarget).sqrMagnitude
                               > BotConstants.ChaseRepathMoveThreshold * BotConstants.ChaseRepathMoveThreshold;

            if (pathValid && !targetMoved && bb.ChaseRepathTimer > 0f)
                return;

            bb.ChasePathCorners ??= new Vector3[BotConstants.ChaseMaxPathCorners];
            bb.ChasePathCornerCount = ctx.NavMesh.CalculatePath(bot.Position, target, bb.ChasePathCorners);
            // Corner 0 is the bot's own position — start steering at the next one.
            bb.ChasePathCornerIndex = bb.ChasePathCornerCount > 1 ? 1 : 0;
            bb.ChasePathTarget = target;
            bb.ChaseRepathTimer = BotConstants.ChaseRepathInterval;
        }

        static Vector3 CurrentSteerTarget(BotEntityState bot, BotBlackboard bb, Vector3 fallback)
        {
            if (bb.ChasePathCornerCount == 0)
                return fallback;

            while (bb.ChasePathCornerIndex < bb.ChasePathCornerCount - 1)
            {
                var toCorner = bb.ChasePathCorners[bb.ChasePathCornerIndex] - bot.Position;
                toCorner.y = 0f;
                if (toCorner.sqrMagnitude >
                    BotConstants.ChaseCornerArrivalDistance * BotConstants.ChaseCornerArrivalDistance)
                    break;
                bb.ChasePathCornerIndex++;
            }

            return bb.ChasePathCorners[Mathf.Min(bb.ChasePathCornerIndex, bb.ChasePathCornerCount - 1)];
        }
    }
}
