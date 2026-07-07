using Constants;
using Session;
using State;
using Systems.Bot.BT;
using UnityEngine;

namespace Systems.Bot.Nodes
{
    /// <summary>
    /// Runs after <see cref="ChaseNode"/> arrives at the last known target position
    /// without regaining sight: the bot stands and sweeps its facing left/right for
    /// <see cref="BotConstants.SearchDuration"/> seconds, then gives up and forgets
    /// the target (back to patrol) — replacing the old freeze-until-memory-expires.
    ///
    /// Facing is driven directly here; BotMovementSystem skips its face-the-target
    /// override while a search is active (SearchEndTime >= 0).
    /// </summary>
    public class SearchNode : IBTNode
    {
        public string Name => "Search";

        public BTStatus Tick(BotEntityState bot, RaidState state, in RaidContext ctx, in BotTypeConfig config)
        {
            var bb = bot.Blackboard;

            if (!bb.HasTarget || bb.CanSeeTarget)
            {
                bb.SearchEndTime = -1f;
                return this.Traced(bot, BTStatus.Failure);
            }

            var toTarget = bb.LastKnownTargetPos - bot.Position;
            toTarget.y = 0f;
            if (toTarget.magnitude > BotConstants.SearchArriveDistance)
            {
                bb.SearchEndTime = -1f;
                return this.Traced(bot, BTStatus.Failure);
            }

            if (bb.SearchEndTime < 0f)
            {
                bb.SearchEndTime = state.ElapsedTime + BotConstants.SearchDuration;
                bb.SearchScanBaseDir = bot.FacingDirection.sqrMagnitude > 0.0001f
                    ? bot.FacingDirection
                    : Vector3.forward;
            }

            if (state.ElapsedTime >= bb.SearchEndTime)
            {
                bb.SearchEndTime = -1f;
                bb.ClearTarget(); // give up — patrol branch takes over next tick
                return this.Traced(bot, BTStatus.Failure);
            }

            // Sweep facing around the arrival heading — checking corners, not statue-ing.
            float phase = (state.ElapsedTime + bb.AimSwaySeed)
                          * (2f * Mathf.PI / BotConstants.SearchScanPeriod);
            float yaw = Mathf.Sin(phase) * BotConstants.SearchScanAmplitudeDeg;
            bot.FacingDirection = Quaternion.Euler(0f, yaw, 0f) * bb.SearchScanBaseDir;

            bot.DesiredVelocity = Vector3.zero;
            bb.DebugStatus = "Search";
            return this.Traced(bot, BTStatus.Running);
        }
    }
}
