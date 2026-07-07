using Constants;
using Session;
using State;
using UnityEngine;

namespace Systems.Bot
{
    public static class BotMovementSystem
    {
        public static void Tick(RaidState state, in RaidContext ctx)
        {
            for (int i = 0; i < state.Bots.Count; i++)
            {
                var bot = state.Bots[i];

                if (!state.HealthMap.TryGetValue(bot.Id, out var hp) || !hp.IsAlive)
                    continue;

                if (!BotConstants.TryGetConfig(bot.TypeId, out var config))
                    continue;

                Vector3 velocity;
                if (bot.IsRolling)
                {
                    velocity = bot.RollDirection * DodgeConstants.Speed;
                }
                else
                {
                    velocity = bot.DesiredVelocity;
                    if (velocity.sqrMagnitude > config.ChaseSpeed * config.ChaseSpeed)
                        velocity = velocity.normalized * config.ChaseSpeed;
                }

                bot.Velocity = velocity;
                var candidatePos = bot.Position + velocity * ctx.DeltaTime;

                if (ctx.NavMesh != null &&
                    ctx.NavMesh.SamplePosition(candidatePos, 1f, out var clampedPos))
                {
                    bot.Position = clampedPos;
                }
                else
                {
                    bot.Position = candidatePos;
                }

                // Pick the desired facing direction. Target takes priority over velocity
                // so the bot stays oriented on its enemy while strafing/repositioning —
                // a human-feeling combat stance, instead of the legacy snap-to-velocity.
                // FireForward bots (FeedbackRange turrets) opt out completely.
                Vector3 desiredFacing = Vector3.zero;
                if (!config.Has(BotBehaviorFlags.FireForward))
                {
                    var bb = bot.Blackboard;
                    // Face the target only once the reaction gate has opened (IsAlert) —
                    // whipping around before "noticing" was the biggest robot tell.
                    // SearchNode drives facing directly while it scans; don't fight it.
                    if (bb.HasTarget && bb.IsAlert && bb.SearchEndTime < 0f)
                    {
                        var toTarget = bot.Blackboard.LastKnownTargetPos - bot.Position;
                        toTarget.y = 0f;
                        if (toTarget.sqrMagnitude > 0.001f)
                            desiredFacing = toTarget.normalized;
                    }
                    else if (velocity.sqrMagnitude > 0.001f)
                    {
                        desiredFacing = velocity.normalized;
                    }
                }

                if (desiredFacing.sqrMagnitude > 0.0001f && bot.FacingDirection.sqrMagnitude > 0.0001f)
                {
                    float maxStepDeg = BotConstants.FacingTurnRateDeg * ctx.DeltaTime;
                    bot.FacingDirection = Vector3.RotateTowards(
                        bot.FacingDirection, desiredFacing,
                        maxStepDeg * Mathf.Deg2Rad, 0f);
                }
                else if (desiredFacing.sqrMagnitude > 0.0001f)
                {
                    bot.FacingDirection = desiredFacing;
                }
            }
        }
    }
}
