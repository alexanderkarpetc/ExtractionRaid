using Constants;
using Session;
using State;
using UnityEngine;

namespace Systems.Bot
{
    public static class BotPerceptionSystem
    {
        public static void Tick(RaidState state, in RaidContext ctx)
        {
            var player = state.PlayerEntity;

            for (int i = 0; i < state.Bots.Count; i++)
            {
                var bot = state.Bots[i];
                var bb = bot.Blackboard;

                bb.PerceptionTimer -= ctx.DeltaTime;
                if (bb.PerceptionTimer > 0f)
                    continue;
                bb.PerceptionTimer = BotConstants.PerceptionTickInterval;

                if (!BotConstants.TryGetConfig(bot.TypeId, out var config))
                    continue;

                if (player == null || !state.HealthMap.TryGetValue(bot.Id, out var botHp) || !botHp.IsAlive)
                {
                    bb.CanSeeTarget = false;
                    if (bb.HasTarget)
                        bb.TimeSinceTargetSeen += BotConstants.PerceptionTickInterval;
                    if (bb.TimeSinceTargetSeen > config.TargetMemoryDuration)
                        ClearTarget(bb);
                    continue;
                }

                bool alerted = bb.WasDamaged;
                bb.WasDamaged = false;

                var toPlayer = player.Position - bot.Position;
                toPlayer.y = 0f;
                var dist = toPlayer.magnitude;

                bool inVisionRange = dist <= config.VisionRange;
                bool inVisionAngle = false;

                if (inVisionRange && bot.FacingDirection.sqrMagnitude > 0.001f)
                {
                    var angle = Vector3.Angle(bot.FacingDirection, toPlayer);
                    inVisionAngle = angle <= config.VisionAngle * 0.5f;
                }

                bool hasLineOfSight = false;
                if (inVisionRange && inVisionAngle)
                {
                    var eyePos = bot.Position + Vector3.up * 1.5f;
                    var targetPos = player.Position + Vector3.up * 1f;
                    hasLineOfSight = ctx.Physics == null || !ctx.Physics.Linecast(eyePos, targetPos);
                }

                bool heard = dist <= config.HearingRange && player.Velocity.sqrMagnitude > 0.1f;
                bool detected = (inVisionRange && inVisionAngle && hasLineOfSight) || heard || alerted;

                if (detected)
                {
                    bb.TargetEId = player.Id;
                    bb.LastKnownTargetPos = player.Position;
                    bb.HasTarget = true;
                    bb.CanSeeTarget = inVisionRange && inVisionAngle && hasLineOfSight;
                    bb.DistanceToTarget = dist;
                    bb.TimeSinceTargetSeen = 0f;
                }
                else
                {
                    bb.CanSeeTarget = false;
                    if (bb.HasTarget)
                    {
                        bb.TimeSinceTargetSeen += BotConstants.PerceptionTickInterval;
                        if (bb.TimeSinceTargetSeen > config.TargetMemoryDuration)
                            ClearTarget(bb);
                    }
                }
            }
        }

        static void ClearTarget(BotBlackboard bb)
        {
            bb.HasTarget = false;
            bb.TargetEId = EId.None;
            bb.CanSeeTarget = false;
            bb.DistanceToTarget = float.MaxValue;
            bb.TimeSinceTargetSeen = float.MaxValue;
            bb.ReactionTimer = 0f;
            bb.HasCover = false;
        }
    }
}
