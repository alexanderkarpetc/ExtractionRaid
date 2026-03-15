using Dev;
using State;
using UnityEngine;

namespace Systems
{
    public static class PlayerFOVSystem
    {
        public static void Tick(RaidState state)
        {
            var player = state.PlayerEntity;
            if (player == null) return;

            // FOV disabled or force-show — all bots visible
            if (!DevCheats.FOVEnabled || DevCheats.ForceShowAllBots)
            {
                for (int i = 0; i < state.Bots.Count; i++)
                    state.Bots[i].IsVisibleToPlayer = true;
                return;
            }

            float nearR = DevCheats.FOVNearRadius;
            float farR = DevCheats.FOVFarRadius;
            float halfAngle = DevCheats.FOVAngle * 0.5f;
            var facing = player.FacingDirection;
            bool hasFacing = facing.sqrMagnitude > 0.001f;

            for (int i = 0; i < state.Bots.Count; i++)
            {
                var bot = state.Bots[i];
                var toBot = bot.Position - player.Position;
                toBot.y = 0f;
                float dist = toBot.magnitude;

                // Inner sphere — 360° close awareness
                if (dist <= nearR)
                {
                    bot.IsVisibleToPlayer = true;
                    continue;
                }

                // Beyond far radius — invisible
                if (dist > farR)
                {
                    bot.IsVisibleToPlayer = false;
                    continue;
                }

                // Outer sector — directional cone
                if (hasFacing)
                {
                    float angle = Vector3.Angle(facing, toBot);
                    bot.IsVisibleToPlayer = angle <= halfAngle;
                }
                else
                {
                    bot.IsVisibleToPlayer = true;
                }
            }
        }
    }
}
