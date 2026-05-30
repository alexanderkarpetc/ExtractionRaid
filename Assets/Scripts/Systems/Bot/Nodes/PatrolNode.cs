using Constants;
using Session;
using State;
using Systems.Bot.BT;
using UnityEngine;

namespace Systems.Bot.Nodes
{
    public class PatrolNode : IBTNode
    {
        public string Name => "Patrol";

        public BTStatus Tick(BotEntityState bot, RaidState state, in RaidContext ctx, in BotTypeConfig config)
        {
            var bb = bot.Blackboard;
            var waypoints = bb.PatrolWaypoints;
            if (waypoints == null || waypoints.Length == 0)
                return this.Traced(bot, BTStatus.Failure);

            if (bb.PatrolWaitTimer > 0f)
            {
                bb.DebugStatus = "Patrol (wait)";
                bb.PatrolWaitTimer -= ctx.DeltaTime;
                bot.DesiredVelocity = Vector3.zero;

                // Head-scan: oscillate facing ±amplitude around base direction. Cheap
                // anti-statue effect — keeps the silhouette alive during wait windows.
                if (bb.PatrolScanBaseDir.sqrMagnitude > 0.0001f)
                {
                    float phase = (BotConstants.PatrolWaitTime - bb.PatrolWaitTimer)
                                  * (2f * Mathf.PI / BotConstants.PatrolScanPeriod);
                    float yaw = Mathf.Sin(phase) * BotConstants.PatrolScanAmplitudeDeg;
                    bot.FacingDirection = Quaternion.Euler(0f, yaw, 0f) * bb.PatrolScanBaseDir;
                }
                return this.Traced(bot, BTStatus.Running);
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

                // Anchor the head-scan to the direction of the next waypoint, so the bot
                // glances around its outgoing line rather than its arrival heading.
                var nextTarget = waypoints[bb.PatrolWaypointIndex];
                var toNext = nextTarget - bot.Position;
                toNext.y = 0f;
                bb.PatrolScanBaseDir = toNext.sqrMagnitude > 0.0001f
                    ? toNext.normalized
                    : bot.FacingDirection;
                return this.Traced(bot, BTStatus.Running);
            }

            bb.DebugStatus = "Patrol";
            bot.DesiredVelocity = (toTarget / dist) * config.PatrolSpeed;
            return this.Traced(bot, BTStatus.Running);
        }
    }
}
