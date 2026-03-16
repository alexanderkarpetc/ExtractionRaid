using Adapters;
using Constants;
using Dev;
using Session;
using State;
using UnityEngine;

namespace Systems
{
    public static class PlayerFOVSystem
    {
        public static void Tick(RaidState state, in RaidContext ctx)
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
            bool checkOcclusion = DevCheats.FOVOcclusionEnabled;

            var eyePos = player.Position + Vector3.up * BotConstants.PlayerEyeHeight;

            for (int i = 0; i < state.Bots.Count; i++)
            {
                var bot = state.Bots[i];
                var toBot = bot.Position - player.Position;
                toBot.y = 0f;
                float dist = toBot.magnitude;

                // Inner sphere — 360° close awareness
                if (dist <= nearR)
                {
                    bot.IsVisibleToPlayer = !checkOcclusion
                        || !IsOccluded(ctx.Physics, eyePos, bot.Position);
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
                    if (angle > halfAngle)
                    {
                        bot.IsVisibleToPlayer = false;
                        continue;
                    }
                }

                // Passed distance+angle — check occlusion
                bot.IsVisibleToPlayer = !checkOcclusion
                    || !IsOccluded(ctx.Physics, eyePos, bot.Position);
            }
        }

        static bool IsOccluded(IPhysicsAdapter physics, Vector3 eyePos, Vector3 botPos)
        {
            if (physics == null) return false;

            var targetPos = botPos + Vector3.up * BotConstants.PlayerEyeHeight;
            return physics.Linecast(eyePos, targetPos, BotConstants.VisionBlockingMask);
        }
    }
}
