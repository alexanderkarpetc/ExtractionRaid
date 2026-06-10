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
                    float phase = (bb.PatrolWaitDuration - bb.PatrolWaitTimer)
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
                AdvanceWaypoint(bot, bb, waypoints);
                return this.Traced(bot, BTStatus.Running);
            }

            bb.DebugStatus = "Patrol";

            // Path-follow along NavMesh corners; falls back to straight-line steering
            // when no navmesh is available or pathing fails.
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
            var desiredDir = toSteer.normalized;

            // Gentle Perlin wander — drifts the walk line a few degrees side to side.
            float wanderYaw = (Mathf.PerlinNoise(bb.AimSwaySeed,
                                  state.ElapsedTime * BotConstants.PatrolWanderFrequency) - 0.5f)
                              * 2f * BotConstants.PatrolWanderAmplitudeDeg;
            desiredDir = Quaternion.Euler(0f, wanderYaw, 0f) * desiredDir;

            // Steer through an arc instead of snapping heading: rotate the previous
            // velocity direction toward the desired one at a bounded rate.
            var prevDir = bot.Velocity;
            prevDir.y = 0f;
            if (prevDir.sqrMagnitude > 0.0001f)
            {
                float maxStep = BotConstants.PatrolSteerTurnRateDeg * Mathf.Deg2Rad * ctx.DeltaTime;
                desiredDir = Vector3.RotateTowards(prevDir.normalized, desiredDir, maxStep, 0f);
            }

            // Ease into the waypoint instead of stopping dead.
            float speed = config.PatrolSpeed * bb.PatrolSpeedScale;
            if (dist < BotConstants.PatrolArrivalSlowRadius)
            {
                float t = dist / BotConstants.PatrolArrivalSlowRadius;
                speed *= Mathf.Lerp(BotConstants.PatrolArrivalMinSpeedFraction, 1f, t);
            }

            bot.DesiredVelocity = desiredDir * speed;

            // Stuck watchdog: commanded to move but barely displacing → repath, then
            // skip the waypoint entirely if a fresh path doesn't free us.
            float expectedStep = speed * ctx.DeltaTime;
            float movedSqr = (bot.Position - bb.PatrolLastPosition).sqrMagnitude;
            bb.PatrolLastPosition = bot.Position;
            if (movedSqr < expectedStep * expectedStep * 0.04f)
            {
                bb.PatrolStuckTimer += ctx.DeltaTime;
                if (bb.PatrolStuckTimer >= BotConstants.PatrolStuckSkipTime)
                {
                    AdvanceWaypoint(bot, bb, waypoints);
                }
                else if (bb.PatrolStuckTimer >= BotConstants.PatrolStuckRepathTime)
                {
                    bb.PatrolPathWaypointIndex = -1; // forces EnsurePath to recalc next tick
                }
            }
            else
            {
                bb.PatrolStuckTimer = 0f;
            }

            return this.Traced(bot, BTStatus.Running);
        }

        static void AdvanceWaypoint(BotEntityState bot, BotBlackboard bb, Vector3[] waypoints)
        {
            bb.PatrolWaypointIndex = (bb.PatrolWaypointIndex + 1) % waypoints.Length;
            bb.PatrolWaitDuration = Random.Range(BotConstants.PatrolWaitTimeMin, BotConstants.PatrolWaitTimeMax);
            bb.PatrolWaitTimer = bb.PatrolWaitDuration;
            bb.PatrolSpeedScale = Random.Range(BotConstants.PatrolSpeedScaleMin, BotConstants.PatrolSpeedScaleMax);
            bb.PatrolPathWaypointIndex = -1;
            bb.PatrolStuckTimer = 0f;
            bot.DesiredVelocity = Vector3.zero;

            // Anchor the head-scan to the direction of the next waypoint, so the bot
            // glances around its outgoing line rather than its arrival heading.
            var toNext = waypoints[bb.PatrolWaypointIndex] - bot.Position;
            toNext.y = 0f;
            bb.PatrolScanBaseDir = toNext.sqrMagnitude > 0.0001f
                ? toNext.normalized
                : bot.FacingDirection;
        }

        static void EnsurePath(BotEntityState bot, BotBlackboard bb, in RaidContext ctx, Vector3 target)
        {
            bb.PatrolRepathTimer -= ctx.DeltaTime;
            bool pathValid = bb.PatrolPathWaypointIndex == bb.PatrolWaypointIndex
                             && bb.PatrolPathCornerIndex < bb.PatrolPathCornerCount;
            if (pathValid && bb.PatrolRepathTimer > 0f)
                return;

            bb.PatrolPathCorners ??= new Vector3[BotConstants.PatrolMaxPathCorners];
            bb.PatrolPathCornerCount = ctx.NavMesh.CalculatePath(bot.Position, target, bb.PatrolPathCorners);
            // Corner 0 is the bot's own position — start steering at the next one.
            bb.PatrolPathCornerIndex = bb.PatrolPathCornerCount > 1 ? 1 : 0;
            bb.PatrolPathWaypointIndex = bb.PatrolPathCornerCount > 0 ? bb.PatrolWaypointIndex : -1;
            bb.PatrolRepathTimer = BotConstants.PatrolRepathInterval;
            bb.PatrolLastPosition = bot.Position;
        }

        static Vector3 CurrentSteerTarget(BotEntityState bot, BotBlackboard bb, Vector3 fallback)
        {
            if (bb.PatrolPathWaypointIndex != bb.PatrolWaypointIndex || bb.PatrolPathCornerCount == 0)
                return fallback;

            // Advance past corners we've reached; the last corner is the waypoint itself
            // and is handled by the WaypointArrivalDistance check in Tick.
            while (bb.PatrolPathCornerIndex < bb.PatrolPathCornerCount - 1)
            {
                var toCorner = bb.PatrolPathCorners[bb.PatrolPathCornerIndex] - bot.Position;
                toCorner.y = 0f;
                if (toCorner.sqrMagnitude >
                    BotConstants.PatrolCornerArrivalDistance * BotConstants.PatrolCornerArrivalDistance)
                    break;
                bb.PatrolPathCornerIndex++;
            }

            return bb.PatrolPathCorners[Mathf.Min(bb.PatrolPathCornerIndex, bb.PatrolPathCornerCount - 1)];
        }
    }
}
