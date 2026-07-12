using Constants;
using Session;
using State;
using Systems.Bot.BT;
using UnityEngine;

namespace Systems.Bot.Nodes
{
    /// <summary>
    /// SAIN-inspired fight-from-cover behaviour: find a navmesh point the enemy has no
    /// line of sight to, run there (firing on the move when the target is visible),
    /// then cycle hide → peek out to a pre-computed shooting spot → expose → duck back.
    ///
    /// Selector contract (sits before ShootNode in the Engage selector):
    /// - Running while it owns movement — seeking, hiding, peeking, returning.
    /// - Failure when the rest of the branch should act: either "exposed at the peek
    ///   spot, let ShootNode fire" or "no usable cover, fall back to shoot/chase".
    ///
    /// Cover validity is a raycast question: the ray from the enemy's eye to the point
    /// at torso height must be blocked (head height too = full cover, preferred). A
    /// point only qualifies if one of its lateral peek spots has a CLEAR line back to
    /// the enemy — cover you can't shoot from is a turtle spot, not a fighting position.
    /// </summary>
    public class TakeCoverNode : IBTNode
    {
        public string Name => "TakeCover";

        public BTStatus Tick(BotEntityState bot, RaidState state, in RaidContext ctx, in BotTypeConfig config)
        {
            var bb = bot.Blackboard;

            // Cover fighting is for live ranged contact within engagement distance.
            // Stale memory → Chase/Search investigate; out of range → Chase closes in.
            // The LoS/nav probes are meaningless without physics + navmesh ports.
            if (!bb.HasTarget
                || bb.TimeSinceTargetSeen > BotConstants.CoverEngageMemoryTime
                || bb.DistanceToTarget > config.EngageRange
                || ctx.Physics == null || ctx.NavMesh == null)
            {
                if (bb.CoverPhase != CoverPhase.None)
                    bb.ResetCover();
                return this.Traced(bot, BTStatus.Failure);
            }

            // Periodic "is this still cover" check — the enemy moves, angles change.
            if (bb.CoverPhase != CoverPhase.None && !RevalidateCover(bb, in ctx))
            {
                bb.ResetCover(); // search again immediately next block
            }

            switch (bb.CoverPhase)
            {
                case CoverPhase.None:    return TickSearch(bot, bb, state, in ctx, in config);
                case CoverPhase.MoveTo:  return TickMoveTo(bot, bb, state, in ctx, in config);
                case CoverPhase.Hold:    return TickHold(bot, bb, state);
                case CoverPhase.Peek:    return TickPeek(bot, bb, state, in ctx, in config);
                case CoverPhase.Exposed: return TickExposed(bot, bb, state);
                case CoverPhase.Return:  return TickReturn(bot, bb, state, in ctx, in config);
                default:                 return this.Traced(bot, BTStatus.Failure);
            }
        }

        // ── Phase: search ────────────────────────────────────────────────────

        BTStatus TickSearch(BotEntityState bot, BotBlackboard bb, RaidState state,
            in RaidContext ctx, in BotTypeConfig config)
        {
            if (state.ElapsedTime < bb.CoverNextSearchTime)
                return this.Traced(bot, BTStatus.Failure);

            if (!TryFindCover(bot, bb, state, in ctx, in config, out var cover, out var peek))
            {
                bb.CoverNextSearchTime = state.ElapsedTime + BotConstants.CoverSearchCooldown;
                return this.Traced(bot, BTStatus.Failure);
            }

            bb.CoverPoint = cover;
            bb.CoverPeekPos = peek;
            bb.CoverEnemyAnchor = bb.LastKnownTargetPos;
            bb.CoverRevalidateTimer = BotConstants.CoverRevalidateInterval;
            EnterPhase(bb, state, CoverPhase.MoveTo);
            bb.CoverPathCornerCount = 0; // force a fresh path
            bb.CoverStuckTimer = 0f;
            bb.CoverLastPosition = bot.Position;
            return TickMoveTo(bot, bb, state, in ctx, in config);
        }

        // ── Phase: run to cover ──────────────────────────────────────────────

        BTStatus TickMoveTo(BotEntityState bot, BotBlackboard bb, RaidState state,
            in RaidContext ctx, in BotTypeConfig config)
        {
            bb.DebugStatus = "Cover (move)";

            var toCover = bb.CoverPoint - bot.Position;
            toCover.y = 0f;
            if (toCover.magnitude < BotConstants.CoverArriveDistance)
            {
                bot.DesiredVelocity = Vector3.zero;
                EnterHold(bb, state);
                return this.Traced(bot, BTStatus.Running);
            }

            MoveAlongPath(bot, bb, in ctx, bb.CoverPoint, config.ChaseSpeed);

            // Suppressive fire on the move — same burst gates ShootNode uses, but with
            // a heavy moving penalty and no strafe/sway (the feet are busy).
            if (bb.CanSeeTarget)
            {
                bot.DesiredAimPoint = bb.LastKnownTargetPos;
                bb.EffectiveAccuracy = Mathf.Clamp01(
                    config.Accuracy * bb.AccuracyMult * BotConstants.CoverMoveFireAccuracyMult);
                if (bb.BurstShotsLeft > 0 || state.ElapsedTime >= bb.NextBurstTime)
                {
                    if (bb.BurstShotsLeft <= 0)
                    {
                        float burst = Random.Range(BotConstants.BurstShotsMin, BotConstants.BurstShotsMax + 1)
                                      * bb.Aggression;
                        bb.BurstShotsLeft = Mathf.Max(1, Mathf.RoundToInt(burst));
                    }
                    bot.WantsToFire = true;
                }
            }

            // Stuck watchdog: commanded to move but barely displacing → repath, then
            // abandon this cover point entirely (unreachable — scoring never pathed it).
            float expectedStep = config.ChaseSpeed * ctx.DeltaTime;
            float movedSqr = (bot.Position - bb.CoverLastPosition).sqrMagnitude;
            bb.CoverLastPosition = bot.Position;
            if (movedSqr < expectedStep * expectedStep * 0.04f)
            {
                bb.CoverStuckTimer += ctx.DeltaTime;
                if (bb.CoverStuckTimer >= BotConstants.CoverStuckAbandonTime)
                {
                    bb.ResetCover();
                    bb.CoverNextSearchTime = state.ElapsedTime + BotConstants.CoverSearchCooldown;
                    return this.Traced(bot, BTStatus.Failure);
                }
                if (bb.CoverStuckTimer >= BotConstants.PatrolStuckRepathTime)
                    bb.CoverPathCornerCount = 0; // forces repath next tick
            }
            else
            {
                bb.CoverStuckTimer = 0f;
            }

            return this.Traced(bot, BTStatus.Running);
        }

        // ── Phase: hold (hidden) ─────────────────────────────────────────────

        BTStatus TickHold(BotEntityState bot, BotBlackboard bb, RaidState state)
        {
            bb.DebugStatus = "Cover (hold)";
            bot.DesiredVelocity = Vector3.zero;

            // Shot while "hidden" — the enemy has an angle the raycast model missed.
            // This cover is lying to us; blacklist the spot (or the re-search would
            // just pick it again — the rays still call it cover) and search anew.
            if (bb.LastDamageTime > bb.CoverPhaseStartTime)
            {
                bb.CoverBlacklistPos = bb.CoverPoint;
                bb.CoverBlacklistUntil = state.ElapsedTime + BotConstants.CoverSpotBlacklistDuration;
                bb.ResetCover();
                return this.Traced(bot, BTStatus.Running);
            }

            // Mid-reload → keep hiding until the mag is back; peeking empty is suicide.
            if (bot.Weapon != null && bot.Weapon.Phase == WeaponPhase.Reloading)
                bb.CoverPhaseEndTime = Mathf.Max(bb.CoverPhaseEndTime,
                    state.ElapsedTime + BotConstants.CoverReloadHoldExtension);

            if (state.ElapsedTime >= bb.CoverPhaseEndTime)
                EnterPhase(bb, state, CoverPhase.Peek);

            return this.Traced(bot, BTStatus.Running);
        }

        // ── Phase: peek (step out) ───────────────────────────────────────────

        BTStatus TickPeek(BotEntityState bot, BotBlackboard bb, RaidState state,
            in RaidContext ctx, in BotTypeConfig config)
        {
            bb.DebugStatus = "Cover (peek)";

            var toPeek = bb.CoverPeekPos - bot.Position;
            toPeek.y = 0f;
            bool arrived = toPeek.magnitude < BotConstants.CoverArriveDistance;

            if (arrived || bb.CanSeeTarget)
            {
                bot.DesiredVelocity = Vector3.zero;
                EnterPhase(bb, state, CoverPhase.Exposed);
                bb.CoverPhaseEndTime = state.ElapsedTime
                    + Random.Range(BotConstants.CoverExposeTimeMin, BotConstants.CoverExposeTimeMax);
                return this.Traced(bot, BTStatus.Running);
            }

            if (state.ElapsedTime - bb.CoverPhaseStartTime > BotConstants.CoverPeekTimeout)
            {
                // Couldn't reach the peek spot — duck back and try a fresh cycle.
                EnterPhase(bb, state, CoverPhase.Return);
                return this.Traced(bot, BTStatus.Running);
            }

            // Short hop — straight steering is enough (peek pos is navmesh-snapped
            // and 1-2 m away); MovementSystem faces the target while we sidestep.
            bot.DesiredVelocity = toPeek.normalized
                                  * (config.ChaseSpeed * BotConstants.CoverPeekSpeedFraction);
            return this.Traced(bot, BTStatus.Running);
        }

        // ── Phase: exposed (ShootNode owns the trigger) ──────────────────────

        BTStatus TickExposed(BotEntityState bot, BotBlackboard bb, RaidState state)
        {
            bool duck = state.ElapsedTime >= bb.CoverPhaseEndTime
                        // hit while exposed → cut the window short
                        || bb.LastDamageTime > bb.CoverPhaseStartTime
                        // mag ran dry → reload behind cover, not in the open
                        || (bot.Weapon != null && bot.Weapon.Phase == WeaponPhase.Reloading)
                        // target slipped away — nothing to shoot; without this, yielding
                        // Failure would fall through past ShootNode into Chase and the
                        // bot would abandon its cover to push
                        || !bb.CanSeeTarget;

            if (duck)
            {
                EnterPhase(bb, state, CoverPhase.Return);
                bot.DesiredVelocity = Vector3.zero;
                return this.Traced(bot, BTStatus.Running);
            }

            // Yield to ShootNode: it fires, strafes and sways from the peek spot.
            return this.Traced(bot, BTStatus.Failure);
        }

        // ── Phase: return (duck back) ────────────────────────────────────────

        BTStatus TickReturn(BotEntityState bot, BotBlackboard bb, RaidState state,
            in RaidContext ctx, in BotTypeConfig config)
        {
            bb.DebugStatus = "Cover (duck)";

            var toCover = bb.CoverPoint - bot.Position;
            toCover.y = 0f;
            bool timedOut = state.ElapsedTime - bb.CoverPhaseStartTime > BotConstants.CoverPeekTimeout;

            if (toCover.magnitude < BotConstants.CoverArriveDistance || timedOut)
            {
                bot.DesiredVelocity = Vector3.zero;
                EnterHold(bb, state);
                return this.Traced(bot, BTStatus.Running);
            }

            bot.DesiredVelocity = toCover.normalized
                                  * (config.ChaseSpeed * BotConstants.CoverPeekSpeedFraction);
            return this.Traced(bot, BTStatus.Running);
        }

        // ── Phase helpers ────────────────────────────────────────────────────

        static void EnterPhase(BotBlackboard bb, RaidState state, CoverPhase phase)
        {
            bb.CoverPhase = phase;
            bb.CoverPhaseStartTime = state.ElapsedTime;
        }

        static void EnterHold(BotBlackboard bb, RaidState state)
        {
            EnterPhase(bb, state, CoverPhase.Hold);
            // Aggressive bots spend less time hiding between peeks.
            float hold = Random.Range(BotConstants.CoverHoldTimeMin, BotConstants.CoverHoldTimeMax)
                         / Mathf.Max(0.5f, bb.Aggression);
            bb.CoverPhaseEndTime = state.ElapsedTime + hold;
        }

        // ── Cover finding / validation ───────────────────────────────────────

        /// <summary>
        /// Ring-sample candidate points around the bot, keep those the enemy eye can't
        /// see at torso height AND that have a workable peek side, score by run
        /// distance + too-close-to-enemy penalty − full-cover bonus. Straight-line
        /// reachability is assumed; the MoveTo stuck watchdog culls the liars.
        /// </summary>
        static bool TryFindCover(BotEntityState bot, BotBlackboard bb, RaidState state,
            in RaidContext ctx, in BotTypeConfig config, out Vector3 coverPoint, out Vector3 peekPos)
        {
            coverPoint = default;
            peekPos = default;

            var enemy = bb.LastKnownTargetPos;
            var enemyEye = enemy + Vector3.up * BotConstants.PlayerEyeHeight;
            float maxEnemyDist = config.EngageRange * BotConstants.CoverMaxEngageFraction;
            float bestScore = float.MaxValue;
            bool found = false;

            var enemyToBot = bot.Position - enemy;
            enemyToBot.y = 0f;
            bool blacklistActive = state.ElapsedTime < bb.CoverBlacklistUntil;

            // Random rotation per search so repeated searches don't probe identical rays.
            float baseAngle = Random.value * 360f;
            float angleStep = 360f / BotConstants.CoverSearchDirections;

            for (int r = 0; r < BotConstants.CoverSearchRadii.Length; r++)
            {
                float radius = BotConstants.CoverSearchRadii[r];
                for (int d = 0; d < BotConstants.CoverSearchDirections; d++)
                {
                    float ang = (baseAngle + d * angleStep) * Mathf.Deg2Rad;
                    var raw = bot.Position + new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * radius;
                    if (!ctx.NavMesh.SamplePosition(raw, BotConstants.CoverNavSampleDistance, out var pos))
                        continue;

                    var toEnemy = enemy - pos;
                    toEnemy.y = 0f;
                    float distEnemy = toEnemy.magnitude;
                    if (distEnemy < BotConstants.CoverMinEnemyDistance || distEnemy > maxEnemyDist)
                        continue;

                    // SAIN's "not behind the enemy" gate — a spot on the far side of
                    // the target means running through their line of fire to reach it.
                    if (Vector3.Dot(-toEnemy, enemyToBot) < 0f)
                        continue;

                    if (blacklistActive
                        && (pos - bb.CoverBlacklistPos).sqrMagnitude
                           < BotConstants.CoverSpotBlacklistRadius * BotConstants.CoverSpotBlacklistRadius)
                        continue;

                    // Blocked ray at torso height = the point hides the body.
                    if (!ctx.Physics.Linecast(enemyEye,
                            pos + Vector3.up * BotConstants.CoverBodyCheckHeight,
                            BotConstants.VisionBlockingMask))
                        continue;

                    bool fullCover = ctx.Physics.Linecast(enemyEye,
                        pos + Vector3.up * BotConstants.CoverHeadCheckHeight,
                        BotConstants.VisionBlockingMask);

                    if (!TryFindPeek(pos, enemy, enemyEye, in ctx, out var peek))
                        continue;

                    float score = (pos - bot.Position).magnitude
                                  + Mathf.Max(0f, BotConstants.CoverPreferredEnemyDistance - distEnemy)
                                    * BotConstants.CoverEnemyClosePenaltyWeight
                                  - (fullCover ? BotConstants.CoverFullCoverBonus : 0f);

                    if (score < bestScore)
                    {
                        bestScore = score;
                        coverPoint = pos;
                        peekPos = peek;
                        found = true;
                    }
                }
            }

            return found;
        }

        /// <summary>
        /// A peek spot is a lateral step (perpendicular to the enemy direction) from
        /// the cover point with a CLEAR torso-height line back to the enemy — the spot
        /// the bot fires from. Random first side so bots don't all peek the same shoulder.
        /// </summary>
        static bool TryFindPeek(Vector3 cover, Vector3 enemy, Vector3 enemyEye,
            in RaidContext ctx, out Vector3 peek)
        {
            peek = default;
            var fwd = enemy - cover;
            fwd.y = 0f;
            if (fwd.sqrMagnitude < 0.0001f)
                return false;
            fwd.Normalize();
            var perp = new Vector3(-fwd.z, 0f, fwd.x);

            float side = Random.value < 0.5f ? 1f : -1f;
            for (int s = 0; s < 2; s++, side = -side)
            {
                var raw = cover + perp * (side * BotConstants.CoverPeekOffset);
                if (!ctx.NavMesh.SamplePosition(raw, 1f, out var pos))
                    continue;
                if (ctx.Physics.Linecast(enemyEye,
                        pos + Vector3.up * BotConstants.CoverBodyCheckHeight,
                        BotConstants.VisionBlockingMask))
                    continue; // still blocked — can't shoot from here
                peek = pos;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Cheap periodic re-check of the picked point. On a large enemy-position drift
        /// the peek side is recomputed too (the open shoulder may have flipped).
        /// Returns false when the point no longer works as cover.
        /// </summary>
        static bool RevalidateCover(BotBlackboard bb, in RaidContext ctx)
        {
            bb.CoverRevalidateTimer -= ctx.DeltaTime;
            bool enemyMoved = (bb.LastKnownTargetPos - bb.CoverEnemyAnchor).sqrMagnitude
                              > BotConstants.CoverEnemyMoveInvalidate * BotConstants.CoverEnemyMoveInvalidate;
            if (bb.CoverRevalidateTimer > 0f && !enemyMoved)
                return true;

            bb.CoverRevalidateTimer = BotConstants.CoverRevalidateInterval;

            var enemy = bb.LastKnownTargetPos;
            var enemyEye = enemy + Vector3.up * BotConstants.PlayerEyeHeight;

            // Enemy pushed onto the cover itself → it's no longer a fighting position.
            var toEnemy = enemy - bb.CoverPoint;
            toEnemy.y = 0f;
            if (toEnemy.magnitude < BotConstants.CoverMinEnemyDistance)
                return false;

            if (!ctx.Physics.Linecast(enemyEye,
                    bb.CoverPoint + Vector3.up * BotConstants.CoverBodyCheckHeight,
                    BotConstants.VisionBlockingMask))
                return false; // the enemy can see the "cover" now

            if (enemyMoved)
            {
                if (!TryFindPeek(bb.CoverPoint, enemy, enemyEye, in ctx, out var peek))
                    return false;
                bb.CoverPeekPos = peek;
                bb.CoverEnemyAnchor = enemy;
            }

            return true;
        }

        // ── Path-following (same corner-buffer pattern as Chase/Patrol) ─────────

        static void MoveAlongPath(BotEntityState bot, BotBlackboard bb, in RaidContext ctx,
            Vector3 target, float speed)
        {
            bb.CoverRepathTimer -= ctx.DeltaTime;
            bool pathValid = bb.CoverPathCornerCount > 0
                             && bb.CoverPathCornerIndex < bb.CoverPathCornerCount;
            if (!pathValid || bb.CoverRepathTimer <= 0f)
            {
                bb.CoverPathCorners ??= new Vector3[BotConstants.CoverMaxPathCorners];
                bb.CoverPathCornerCount = ctx.NavMesh.CalculatePath(bot.Position, target, bb.CoverPathCorners);
                // Corner 0 is the bot's own position — start steering at the next one.
                bb.CoverPathCornerIndex = bb.CoverPathCornerCount > 1 ? 1 : 0;
                bb.CoverRepathTimer = BotConstants.CoverRepathInterval;
            }

            var steerTarget = target;
            if (bb.CoverPathCornerCount > 0)
            {
                while (bb.CoverPathCornerIndex < bb.CoverPathCornerCount - 1)
                {
                    var toCorner = bb.CoverPathCorners[bb.CoverPathCornerIndex] - bot.Position;
                    toCorner.y = 0f;
                    if (toCorner.sqrMagnitude >
                        BotConstants.CoverCornerArrivalDistance * BotConstants.CoverCornerArrivalDistance)
                        break;
                    bb.CoverPathCornerIndex++;
                }
                steerTarget = bb.CoverPathCorners[
                    Mathf.Min(bb.CoverPathCornerIndex, bb.CoverPathCornerCount - 1)];
            }

            var toSteer = steerTarget - bot.Position;
            toSteer.y = 0f;
            if (toSteer.sqrMagnitude < 0.0001f)
            {
                toSteer = target - bot.Position;
                toSteer.y = 0f;
            }
            bot.DesiredVelocity = toSteer.sqrMagnitude > 0.0001f
                ? toSteer.normalized * speed
                : Vector3.zero;
        }
    }
}
