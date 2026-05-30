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

                    if (ctx.Physics == null)
                    {
                        hasLineOfSight = true;
                    }
                    else
                    {
                        // Use RaycastAll and ignore hits on bot/player colliders (by proximity to known positions).
                        // This prevents character CapsuleColliders on Default layer from blocking vision.
                        var dir = targetPos - eyePos;
                        float maxDist = dir.magnitude;
                        if (maxDist > 0.001f)
                        {
                            hasLineOfSight = true;
                            var hits = Physics.RaycastAll(eyePos, dir / maxDist, maxDist, BotConstants.VisionBlockingMask);
                            for (int h = 0; h < hits.Length; h++)
                            {
                                var hitPos = hits[h].collider.transform.position;
                                // Skip colliders near bot or player position (character colliders)
                                if ((hitPos - bot.Position).sqrMagnitude < 4f) continue;
                                if ((hitPos - player.Position).sqrMagnitude < 4f) continue;
                                hasLineOfSight = false;
                                break;
                            }
                        }
                    }
                }

                bool heard = dist <= config.HearingRange && player.Velocity.sqrMagnitude > 0.1f;
                bool detected = (inVisionRange && inVisionAngle && hasLineOfSight) || heard || alerted;

                if (detected)
                {
                    bool freshAcquire = !bb.HasTarget;

                    bb.TargetEId = player.Id;
                    bb.LastKnownTargetPos = player.Position;
                    bb.HasTarget = true;
                    bb.CanSeeTarget = inVisionRange && inVisionAngle && hasLineOfSight;
                    bb.DistanceToTarget = dist;
                    bb.TimeSinceTargetSeen = 0f;

                    if (bb.CanSeeTarget)
                        bb.GrenadeThrowDelayTimer = -1f;

                    if (freshAcquire)
                    {
                        bb.ReactionJitter   = Random.Range(BotConstants.ReactionJitterMin, BotConstants.ReactionJitterMax);
                        bb.StrafeDirection  = Random.value < 0.5f ? -1 : 1;
                        bb.StrafeChangeTime = state.ElapsedTime + Random.Range(BotConstants.ShootStrafeMinDuration, BotConstants.ShootStrafeMaxDuration);
                        bb.AimSwaySeed      = Random.Range(0f, 1000f);
                    }
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
        }
    }
}
