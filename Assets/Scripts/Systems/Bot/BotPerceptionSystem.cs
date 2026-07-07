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
                        bb.ClearTarget();
                    continue;
                }

                bool alerted = bb.WasDamaged;
                bb.WasDamaged = false;

                var toPlayer = player.Position - bot.Position;
                toPlayer.y = 0f;
                var dist = toPlayer.magnitude;

                // ── Vision geometry ──────────────────────────────────────────
                bool inVisionRange = dist <= config.VisionRange;
                bool inVisionAngle = false;
                float angleToPlayer = 180f;

                if (inVisionRange && bot.FacingDirection.sqrMagnitude > 0.001f)
                {
                    angleToPlayer = Vector3.Angle(bot.FacingDirection, toPlayer);
                    inVisionAngle = angleToPlayer <= config.VisionAngle * 0.5f;
                }

                // 360° close-presence sense — someone standing right next to you is
                // noticed regardless of facing (still requires line of sight).
                bool closeSense = config.VisionRange > 0f && dist <= BotConstants.CloseSenseRadius;
                bool inCone = (inVisionRange && inVisionAngle) || closeSense;

                bool hasLineOfSight = false;
                if (inCone)
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

                bool visible = inCone && hasLineOfSight;

                // ── Graduated awareness ─────────────────────────────────────
                // Distant/peripheral targets take time to register; close targets are
                // instant. Awareness decays when sight breaks instead of vanishing.
                if (visible)
                {
                    float instantRadius = config.VisionRange * BotConstants.VisionInstantFraction;
                    if (dist <= instantRadius || closeSense)
                    {
                        bb.VisionAwareness01 = 1f;
                    }
                    else
                    {
                        float distT = Mathf.InverseLerp(instantRadius, config.VisionRange, dist);
                        float detectTime = Mathf.Lerp(
                            BotConstants.VisionDetectTimeMin, BotConstants.VisionDetectTimeMax, distT);
                        if (angleToPlayer > config.VisionAngle * 0.5f * BotConstants.PeripheralAngleFraction)
                            detectTime *= BotConstants.PeripheralDetectTimeMult;
                        if (bb.HasTarget)
                            detectTime *= BotConstants.CombatDetectTimeMult;

                        bb.VisionAwareness01 += BotConstants.PerceptionTickInterval / Mathf.Max(0.01f, detectTime);
                    }
                }
                else
                {
                    bb.VisionAwareness01 -= BotConstants.VisionAwarenessDecayPerSec * BotConstants.PerceptionTickInterval;
                }
                bb.VisionAwareness01 = Mathf.Clamp01(bb.VisionAwareness01);

                bool seen = visible && bb.VisionAwareness01 >= 1f;

                // ── Hearing: noise tiers + gunshots ─────────────────────────
                // Movement noise scales with how the player moves; gunshots are loud
                // map-scale events heard far beyond footstep range.
                float noiseRadius = 0f;
                float playerSpeed = player.Velocity.magnitude;
                if (playerSpeed > 0.3f)
                {
                    noiseRadius = config.HearingRange;
                    if (player.IsSprinting)
                        noiseRadius *= BotConstants.SprintNoiseMult;
                    else if (playerSpeed < BotConstants.SneakSpeedThreshold)
                        noiseRadius *= BotConstants.SneakNoiseMult;
                }

                bool gunshot = false;
                var playerWeapon = player.EquippedWeapon;
                if (config.HearingRange > 0f && playerWeapon != null && playerWeapon.LastFireTime > 0f
                    && state.ElapsedTime - playerWeapon.LastFireTime <= BotConstants.GunshotRecencyWindow)
                {
                    gunshot = true;
                    noiseRadius = Mathf.Max(noiseRadius, BotConstants.GunshotHearingRange);
                }

                bool heard = noiseRadius > 0f && dist <= noiseRadius;
                bool detected = seen || heard || alerted;

                if (detected)
                {
                    bool freshAcquire = !bb.HasTarget;

                    bb.TargetEId = player.Id;
                    bb.HasTarget = true;
                    bb.CanSeeTarget = seen;
                    bb.DistanceToTarget = dist;
                    bb.TimeSinceTargetSeen = 0f;

                    if (seen)
                    {
                        // Exact fix only comes from eyes-on. Re-appearing after a break
                        // resets aim settle — the bot has to re-acquire its aim.
                        if (state.ElapsedTime - bb.LastCanSeeTime >= BotConstants.AimSettleResetUnseenTime)
                            bb.AimSettle01 = 0f;
                        bb.LastCanSeeTime = state.ElapsedTime;
                        bb.LastKnownTargetPos = player.Position;
                    }
                    else
                    {
                        // Heard/damage contact = fuzzy localization, not a GPS pin.
                        float err = alerted
                            ? BotConstants.DamagePosError
                            : dist * (gunshot ? BotConstants.GunshotPosErrorFraction
                                              : BotConstants.HeardPosErrorFraction);
                        var offset2 = Random.insideUnitCircle * err;
                        bb.LastKnownTargetPos = player.Position + new Vector3(offset2.x, 0f, offset2.y);
                    }

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
                            bb.ClearTarget();
                    }
                }
            }
        }
    }
}
