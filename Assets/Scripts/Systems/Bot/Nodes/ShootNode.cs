using Constants;
using Session;
using State;
using Systems.Bot.BT;
using UnityEngine;

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

            // Player-centric off-screen gate: even якщо EngageRange дозволяє, bots мусять бути
            // в межах screen-aligned radius до гравця, інакше player ловить damage без telegraph.
            // Орthogonal to EngageRange (per-bot identity) — це camera-driven UX rule.
            var maxR = ctx.BotEngagementConfig.MaxEngagementRadius;
            if (ctx.BotEngagementConfig.Enabled && maxR > 0f && bb.DistanceToTarget > maxR)
                return this.Traced(bot, BTStatus.Failure);

            float reactionThreshold = config.ReactionTime + bb.ReactionJitter;
            if (bb.ReactionTimer < reactionThreshold)
            {
                bb.DebugStatus = "Reacting...";
                bb.ReactionTimer += ctx.DeltaTime;
                return this.Traced(bot, BTStatus.Running);
            }

            // Lateral axis relative to the target — both strafe and aim sway use it.
            var toTarget = bb.LastKnownTargetPos - bot.Position;
            toTarget.y = 0f;
            Vector3 perp;
            if (toTarget.sqrMagnitude > 0.0001f)
            {
                var fwd = toTarget.normalized;
                perp = new Vector3(-fwd.z, 0f, fwd.x); // 90° CCW around Y
            }
            else
            {
                perp = Vector3.right;
            }

            // Strafe: flip direction every Min..Max seconds so the bot doesn't drift away.
            if (bb.StrafeDirection == 0)
                bb.StrafeDirection = 1;
            if (state.ElapsedTime >= bb.StrafeChangeTime)
            {
                bb.StrafeDirection = -bb.StrafeDirection;
                bb.StrafeChangeTime = state.ElapsedTime
                    + Random.Range(BotConstants.ShootStrafeMinDuration, BotConstants.ShootStrafeMaxDuration);
            }
            bot.DesiredVelocity = perp * (bb.StrafeDirection * config.ChaseSpeed * BotConstants.ShootStrafeSpeedFraction);

            // Aim sway: slow Perlin-noise lateral wobble around LastKnownTargetPos. Combined
            // with existing per-pellet accuracy spread it gives a "tracking, not snapping"
            // feel without changing hit rate at engage range meaningfully.
            float swayT = state.ElapsedTime * BotConstants.AimSwayFrequency + bb.AimSwaySeed;
            float swayX = (Mathf.PerlinNoise(swayT,        0f)   - 0.5f) * 2f;
            float swayZ = (Mathf.PerlinNoise(0f,    swayT + 17f) - 0.5f) * 2f;
            var swayOffset = perp * (swayX * BotConstants.AimSwayAmplitude)
                           + Vector3.up * (swayZ * BotConstants.AimSwayAmplitude * 0.5f);

            bb.DebugStatus = "Shoot";
            bot.DesiredAimPoint = bb.LastKnownTargetPos + swayOffset;
            bot.WantsToFire = true;
            return this.Traced(bot, BTStatus.Success);
        }
    }
}
