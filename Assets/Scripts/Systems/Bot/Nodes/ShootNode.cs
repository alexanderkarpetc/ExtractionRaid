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

            // Reaction is gated tree-wide by the "Alert?" condition (see BotTreeBuilder);
            // by the time this node runs the bot has already noticed the target.

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

            // Footwork. Bots fighting from cover keep the legacy pure-strafe: their
            // peek spot is only valid where they stand, so TakeCoverNode owns the feet
            // and this node must not push them off it.
            bool fromCover = bb.CoverPhase != CoverPhase.None;
            if (fromCover)
            {
                bot.DesiredVelocity = StrafeVelocity(bb, state, perp, in config);
            }
            else
            {
                // Open field: roll a stance every few seconds — plant, push, or strafe.
                // Endless strafing was the tell that read as "weird" to the player.
                if (state.ElapsedTime >= bb.CombatStanceEndTime)
                    RollStance(bb, state);

                switch (bb.CombatStance)
                {
                    case CombatStance.Strafe:
                        bot.DesiredVelocity = StrafeVelocity(bb, state, perp, in config);
                        break;

                    case CombatStance.Advance:
                        float stopDist = Mathf.Max(BotConstants.ShootAdvanceStopMin,
                            config.EngageRange * BotConstants.ShootAdvanceStopFraction);
                        // Own-position distance, not the perception-tick snapshot in
                        // bb.DistanceToTarget — that one lags and would overshoot.
                        if (toTarget.magnitude > stopDist)
                        {
                            bot.DesiredVelocity = toTarget.normalized * (config.ChaseSpeed
                                * BotConstants.ShootAdvanceSpeedFraction * bb.Aggression);
                            break;
                        }
                        // Already inside the push distance — go lateral instead of
                        // oscillating at the stop line or walking into melee. Standing
                        // still is Hold's job, not a side effect of a blocked advance.
                        bb.CombatStance = CombatStance.Strafe;
                        bot.DesiredVelocity = StrafeVelocity(bb, state, perp, in config);
                        break;

                    default: // Hold
                        bot.DesiredVelocity = Vector3.zero;
                        break;
                }
            }

            // Aim sway: slow Perlin-noise lateral wobble around LastKnownTargetPos. Combined
            // with existing per-pellet accuracy spread it gives a "tracking, not snapping"
            // feel without changing hit rate at engage range meaningfully.
            float swayT = state.ElapsedTime * BotConstants.AimSwayFrequency + bb.AimSwaySeed;
            float swayX = (Mathf.PerlinNoise(swayT,        0f)   - 0.5f) * 2f;
            float swayZ = (Mathf.PerlinNoise(0f,    swayT + 17f) - 0.5f) * 2f;
            var swayOffset = perp * (swayX * BotConstants.AimSwayAmplitude)
                           + Vector3.up * (swayZ * BotConstants.AimSwayAmplitude * 0.5f);

            // Aim settle: accuracy ramps up while the target stays visible. Reset on
            // re-acquisition happens in BotPerceptionSystem (LastCanSeeTime gap check).
            bb.AimSettle01 = Mathf.Min(1f, bb.AimSettle01 + ctx.DeltaTime / BotConstants.AimSettleTime);

            // Effective accuracy = base * personality * settle * movement * pressure.
            // BotCombatSystem reads this instead of raw config.Accuracy (0 = unset).
            float acc = config.Accuracy * bb.AccuracyMult;
            acc *= Mathf.Lerp(BotConstants.AimSettleStartAccuracyMult, 1f, bb.AimSettle01);
            if (bot.Velocity.magnitude > BotConstants.MovingAccuracySpeedThreshold)
                acc *= BotConstants.MovingAccuracyMult;
            if (state.ElapsedTime - bb.LastDamageTime < BotConstants.RecentDamageAccuracyWindow)
                acc *= BotConstants.RecentDamageAccuracyMult;
            bb.EffectiveAccuracy = Mathf.Clamp01(acc);

            bot.DesiredAimPoint = bb.LastKnownTargetPos + swayOffset;

            // Trigger discipline: fire in bursts with pauses instead of a continuous
            // stream. Aggressive bots run longer bursts and shorter pauses. The shot
            // countdown/pause rolls live in BotCombatSystem where shots actually fire.
            if (bb.BurstShotsLeft <= 0 && state.ElapsedTime < bb.NextBurstTime)
            {
                bb.DebugStatus = "Shoot (pause)";
                return this.Traced(bot, BTStatus.Running);
            }

            if (bb.BurstShotsLeft <= 0)
            {
                float burst = Random.Range(BotConstants.BurstShotsMin, BotConstants.BurstShotsMax + 1)
                              * bb.Aggression;
                bb.BurstShotsLeft = Mathf.Max(1, Mathf.RoundToInt(burst));
            }

            bb.DebugStatus = fromCover ? "Shoot" : StanceLabel(bb.CombatStance);
            bot.WantsToFire = true;
            return this.Traced(bot, BTStatus.Success);
        }

        /// <summary>
        /// Lateral movement along the perp-to-target axis, flipping direction every
        /// Min..Max seconds so the bot doesn't drift off across the map.
        /// </summary>
        static Vector3 StrafeVelocity(BotBlackboard bb, RaidState state, Vector3 perp,
            in BotTypeConfig config)
        {
            if (bb.StrafeDirection == 0)
                bb.StrafeDirection = 1;
            if (state.ElapsedTime >= bb.StrafeChangeTime)
            {
                bb.StrafeDirection = -bb.StrafeDirection;
                bb.StrafeChangeTime = state.ElapsedTime
                    + Random.Range(BotConstants.ShootStrafeMinDuration, BotConstants.ShootStrafeMaxDuration);
            }
            return perp * (bb.StrafeDirection * config.ChaseSpeed
                           * BotConstants.ShootStrafeSpeedFraction * bb.Aggression);
        }

        /// <summary>
        /// Weighted stance roll. Aggression pushes toward Advance and away from Hold, so
        /// a jumpy bot presses in while a cautious one plants and shoots. Repeating the
        /// current stance is possible but down-weighted.
        /// </summary>
        static void RollStance(BotBlackboard bb, RaidState state)
        {
            float aggr = Mathf.Max(0.1f, bb.Aggression);
            float hold    = BotConstants.ShootStanceHoldWeight / aggr;
            float advance = BotConstants.ShootStanceAdvanceWeight * aggr;
            float strafe  = BotConstants.ShootStanceStrafeWeight;

            switch (bb.CombatStance)
            {
                case CombatStance.Hold:    hold    *= BotConstants.ShootStanceRepeatWeightMult; break;
                case CombatStance.Advance: advance *= BotConstants.ShootStanceRepeatWeightMult; break;
                default:                   strafe  *= BotConstants.ShootStanceRepeatWeightMult; break;
            }

            float roll = Random.value * (hold + advance + strafe);
            var next = roll < hold                ? CombatStance.Hold
                     : roll < hold + advance      ? CombatStance.Advance
                                                  : CombatStance.Strafe;

            // Entering a fresh strafe leg: pick a side outright instead of inheriting
            // whichever way the previous leg happened to end.
            if (next == CombatStance.Strafe && bb.CombatStance != CombatStance.Strafe)
            {
                bb.StrafeDirection  = Random.value < 0.5f ? -1 : 1;
                bb.StrafeChangeTime = state.ElapsedTime
                    + Random.Range(BotConstants.ShootStrafeMinDuration, BotConstants.ShootStrafeMaxDuration);
            }

            bb.CombatStance = next;
            bb.CombatStanceEndTime = state.ElapsedTime
                + Random.Range(BotConstants.ShootStanceMinDuration, BotConstants.ShootStanceMaxDuration);
        }

        static string StanceLabel(CombatStance stance) => stance switch
        {
            CombatStance.Hold    => "Shoot (hold)",
            CombatStance.Advance => "Shoot (push)",
            _                    => "Shoot (strafe)",
        };
    }
}
