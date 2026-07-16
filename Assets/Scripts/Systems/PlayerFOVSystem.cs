using Adapters;
using Constants;
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

            var fov = ctx.FOVConfig;

            // FOV disabled or force-show — all bots fully visible
            if (!fov.Enabled || fov.ForceShowAllBots)
            {
                for (int i = 0; i < state.Bots.Count; i++)
                {
                    state.Bots[i].IsVisibleToPlayer = true;
                    state.Bots[i].VisibilityFactor = 1f;
                }
                return;
            }

            float nearR = fov.NearRadius;
            float farR = fov.FarRadius;
            float halfAngle = fov.Angle * 0.5f;
            var facing = player.FacingDirection;
            bool hasFacing = facing.sqrMagnitude > 0.001f;
            bool checkOcclusion = fov.OcclusionEnabled;

            var eyePos = player.Position + Vector3.up * BotConstants.PlayerEyeHeight;

            for (int i = 0; i < state.Bots.Count; i++)
            {
                var bot = state.Bots[i];
                var toBot = bot.Position - player.Position;
                toBot.y = 0f;
                float dist = toBot.magnitude;

                // Inner sphere — 360° close awareness (full visibility, no edge fade)
                if (dist <= nearR)
                {
                    bool occ = checkOcclusion && IsOccluded(ctx.Physics, eyePos, bot.Position, player.Position);
                    bot.IsVisibleToPlayer = !occ;
                    bot.VisibilityFactor = occ ? 0f : 1f;
                    continue;
                }

                // Beyond far radius — invisible
                if (dist > farR)
                {
                    bot.IsVisibleToPlayer = false;
                    bot.VisibilityFactor = 0f;
                    continue;
                }

                // Outer sector — directional cone
                float angle = hasFacing ? Vector3.Angle(facing, toBot) : 0f;
                if (hasFacing && angle > halfAngle)
                {
                    bot.IsVisibleToPlayer = false;
                    bot.VisibilityFactor = 0f;
                    continue;
                }

                // Inside cone+range — hard bool for gameplay, smooth factor for the view fade.
                bool occluded = checkOcclusion && IsOccluded(ctx.Physics, eyePos, bot.Position, player.Position);
                bot.IsVisibleToPlayer = !occluded;
                if (occluded)
                {
                    bot.VisibilityFactor = 0f;
                }
                else
                {
                    // Ramp to 0 across a band at the far-range and angular edges (smooth fade-in/out).
                    float distFactor = Mathf.InverseLerp(farR, farR - EdgeFalloffMeters, dist);
                    float angleFactor = hasFacing
                        ? Mathf.InverseLerp(halfAngle, halfAngle - EdgeFalloffDegrees, angle)
                        : 1f;
                    bot.VisibilityFactor = Mathf.Clamp01(distFactor * angleFactor);
                }
            }

            // Sniper-scope spotting — reveal bots inside the scoped circle around the aim
            // point, even when they're outside the normal cone (the "look far through the
            // scope" mechanic). Only additive: never hides a bot the cone already sees.
            if (player.ScopeReveal >= ScopeSpotThreshold && player.ScopeRadius > 0f)
            {
                float scopeR2 = player.ScopeRadius * player.ScopeRadius;
                for (int i = 0; i < state.Bots.Count; i++)
                {
                    var bot = state.Bots[i];
                    if (bot.IsVisibleToPlayer) continue;

                    var toCenter = bot.Position - player.ScopeCenter;
                    toCenter.y = 0f;
                    if (toCenter.sqrMagnitude > scopeR2) continue;

                    bool spotted = !checkOcclusion
                        || !IsOccluded(ctx.Physics, eyePos, bot.Position, player.Position);
                    bot.IsVisibleToPlayer = spotted;
                    if (spotted) bot.VisibilityFactor = 1f;
                }
            }
        }

        // Min ScopeReveal (ADS blend) before the scope circle starts spotting — avoids
        // pop-in the instant the player taps aim.
        const float ScopeSpotThreshold = 0.5f;

        // Soft-fade bands at the FoV boundary (view-only VisibilityFactor ramp).
        const float EdgeFalloffMeters = 4f;
        const float EdgeFalloffDegrees = 12f;

        // Character-collider ignore radius for FOV sight checks: CapsuleColliders on Default
        // layer would otherwise spuriously block vision near player/bot positions.
        const float CharacterIgnoreRadius = 2f;

        static bool IsOccluded(IPhysicsAdapter physics, Vector3 eyePos, Vector3 botPos, Vector3 playerPos)
        {
            if (physics == null) return false;

            var targetPos = botPos + Vector3.up * BotConstants.PlayerEyeHeight;
            return physics.IsLineOfSightBlocked(
                eyePos, targetPos, BotConstants.VisionBlockingMask,
                playerPos, botPos, CharacterIgnoreRadius);
        }
    }
}
