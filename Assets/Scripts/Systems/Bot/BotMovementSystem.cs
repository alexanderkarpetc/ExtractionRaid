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

                var velocity = bot.DesiredVelocity;
                if (velocity.sqrMagnitude > config.ChaseSpeed * config.ChaseSpeed)
                    velocity = velocity.normalized * config.ChaseSpeed;

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

                if (velocity.sqrMagnitude > 0.001f)
                {
                    bot.FacingDirection = velocity.normalized;
                }
            }
        }
    }
}
