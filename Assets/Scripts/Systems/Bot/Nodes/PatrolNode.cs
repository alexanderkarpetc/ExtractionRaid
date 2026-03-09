using Constants;
using Session;
using State;
using Systems.Bot.BT;
using UnityEngine;

namespace Systems.Bot.Nodes
{
    public class PatrolNode : IBTNode
    {
        public BTStatus Tick(BotEntityState bot, RaidState state, in RaidContext ctx, in BotTypeConfig config)
        {
            var bb = bot.Blackboard;
            var waypoints = bb.PatrolWaypoints;
            if (waypoints == null || waypoints.Length == 0)
                return BTStatus.Failure;

            if (bb.PatrolWaitTimer > 0f)
            {
                bb.PatrolWaitTimer -= ctx.DeltaTime;
                bot.DesiredVelocity = Vector3.zero;
                return BTStatus.Running;
            }

            var target = waypoints[bb.PatrolWaypointIndex];
            var toTarget = target - bot.Position;
            toTarget.y = 0f;
            var dist = toTarget.magnitude;

            if (dist < BotConstants.WaypointArrivalDistance)
            {
                bb.PatrolWaypointIndex = (bb.PatrolWaypointIndex + 1) % waypoints.Length;
                bb.PatrolWaitTimer = BotConstants.PatrolWaitTime;
                bot.DesiredVelocity = Vector3.zero;
                return BTStatus.Running;
            }

            bot.DesiredVelocity = (toTarget / dist) * config.PatrolSpeed;
            return BTStatus.Running;
        }
    }
}
