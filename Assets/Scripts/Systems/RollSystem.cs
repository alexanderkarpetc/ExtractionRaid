using Constants;
using Session;
using State;
using UnityEngine;

namespace Systems
{
    public static class RollSystem
    {
        public static void Tick(RaidState state, in RaidContext context)
        {
            float elapsed = state.ElapsedTime;

            TickPlayer(state, in context, elapsed);
            TickBots(state, elapsed);
        }

        static void TickPlayer(RaidState state, in RaidContext context, float elapsed)
        {
            var player = state.PlayerEntity;
            if (player == null) return;

            if (player.IsRolling)
            {
                if (elapsed - player.RollStartTime >= DodgeConstants.Duration)
                {
                    player.IsRolling = false;
                    player.RollCooldownEndTime = elapsed + DodgeConstants.Cooldown;
                }
                return;
            }

            var input = context.Input;
            if (input == null || !input.DodgePressed) return;
            if (elapsed < player.RollCooldownEndTime) return;

            var moveInput = input.MoveInput;
            var dir = new Vector3(moveInput.x, 0f, moveInput.y);

            if (dir.sqrMagnitude < 0.01f)
                dir = player.FacingDirection;

            player.IsRolling = true;
            player.RollDirection = dir.normalized;
            player.RollStartTime = elapsed;
        }

        static void TickBots(RaidState state, float elapsed)
        {
            for (int i = 0; i < state.Bots.Count; i++)
            {
                var bot = state.Bots[i];
                if (!bot.IsRolling) continue;

                if (elapsed - bot.RollStartTime >= DodgeConstants.Duration)
                {
                    bot.IsRolling = false;
                    bot.RollCooldownEndTime = elapsed + DodgeConstants.Cooldown;
                }
            }
        }

        public static void StartBotRoll(BotEntityState bot, Vector3 direction, float elapsed)
        {
            if (bot.IsRolling) return;
            if (elapsed < bot.RollCooldownEndTime) return;

            bot.IsRolling = true;
            bot.RollDirection = direction.normalized;
            bot.RollStartTime = elapsed;
        }
    }
}
