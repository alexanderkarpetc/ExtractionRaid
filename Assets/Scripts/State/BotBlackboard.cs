using UnityEngine;

namespace State
{
    /// <summary>
    /// Fight-from-cover state machine phases (TakeCoverNode). None = no cover picked;
    /// the node searches for one on its next eligible tick.
    /// </summary>
    public enum CoverPhase : byte
    {
        None,        // no valid cover point — search when gates allow
        MoveTo,      // running to the picked cover point (may fire on the move)
        Hold,        // hidden behind cover, waiting out the hold timer
        Peek,        // stepping sideways to the peek position to regain line of sight
        Exposed,     // at the peek spot — TakeCoverNode yields so ShootNode fires
        Return,      // ducking back behind cover after the exposure window
    }

    /// <summary>
    /// Open-field fighting footwork (ShootNode). Rolled every few seconds so a bot
    /// doesn't slide sideways for the whole engagement. Not used while fighting from
    /// cover — TakeCoverNode owns the feet there.
    /// </summary>
    public enum CombatStance : byte
    {
        Strafe = 0, // lateral movement perpendicular to the target (the legacy behaviour)
        Hold,       // planted, shooting from where it stands
        Advance,    // walking in on the target, stopping short of melee range
    }

    public class BotBlackboard
    {
        // Target tracking
        public EId TargetEId;
        public Vector3 LastKnownTargetPos;
        public bool HasTarget;
        public bool CanSeeTarget;
        public float DistanceToTarget;
        public float TimeSinceTargetSeen;

        // Graduated perception — humans don't detect targets instantly at range.
        // VisionAwareness01 accumulates while the player is in cone+LoS (rate scales with
        // distance/peripheral angle) and decays when not. Target counts as "seen" at 1.
        public float VisionAwareness01;
        // Reaction gate: true once ReactionTimer passed the per-bot threshold. Gates the
        // whole response chain (facing, chase, fire) — not just the first shot.
        public bool IsAlert;
        // ElapsedTime when CanSeeTarget was last true — drives aim-settle reset after
        // the target re-appears from behind cover.
        public float LastCanSeeTime = -999f;

        // Patrol
        public Vector3[] PatrolWaypoints;
        public int PatrolWaypointIndex;
        public float PatrolWaitTimer;
        public float PatrolWaitDuration;    // rolled per stop; full length of the current wait
        public Vector3 PatrolScanBaseDir;   // facing captured when wait started; head-scan oscillates around this

        // Patrol path-following: NavMesh corners toward the current waypoint
        public Vector3[] PatrolPathCorners;     // lazily allocated corner buffer
        public int PatrolPathCornerCount;
        public int PatrolPathCornerIndex;
        public int PatrolPathWaypointIndex = -1; // waypoint the cached path targets; -1 = no valid path
        public float PatrolRepathTimer;
        public float PatrolStuckTimer;          // seconds of commanded-move with no real displacement
        public Vector3 PatrolLastPosition;

        // Patrol humanization — rolled per leg so each stretch reads differently
        public float PatrolSpeedScale = 1f;

        // Timers
        public float ReactionTimer;
        public float NextDodgeTime;         // ElapsedTime gate for the next dodge roll (config.DodgeCooldown)
        public float HealCooldownTimer;
        public float PerceptionTimer;

        // Humanization — rolled per target-acquisition so each engagement feels distinct
        public float ReactionJitter;        // added to config.ReactionTime in ShootNode
        public int   StrafeDirection;       // -1 or +1 along perp-to-target axis
        public float StrafeChangeTime;      // ElapsedTime at which to flip strafe direction
        public float AimSwaySeed;           // per-bot phase offset for aim-sway noise

        // Combat footwork — re-rolled every ShootStance*Duration seconds in ShootNode
        public CombatStance CombatStance;
        public float CombatStanceEndTime;   // ElapsedTime at which to roll the next stance

        // Personality — rolled once per spawn so each bot is an individual, not a clone
        public float ReactionTimeMult = 1f; // scales config.ReactionTime
        public float AccuracyMult    = 1f;  // scales config.Accuracy
        public float Aggression      = 1f;  // scales burst length up / burst pause down / strafe speed

        // Trigger discipline — bots fire in bursts with pauses, not a continuous stream
        public int   BurstShotsLeft;        // shots remaining in current burst (0 = between bursts)
        public float NextBurstTime;         // ElapsedTime at which the next burst may start

        // Aim settle — accuracy ramps up over AimSettleTime after target (re)appears
        public float AimSettle01;
        // Effective accuracy computed by ShootNode (settle/movement/pressure applied);
        // 0 = unset → BotCombatSystem falls back to raw config.Accuracy.
        public float EffectiveAccuracy;
        public bool IsInsideEngagementView;

        // Heal cast — medkit takes time; bot is vulnerable (retreats, can't fire) while it runs
        public float HealCastEndTime = -1f; // -1 = idle; ElapsedTime at which heal completes

        // Chase path-following: NavMesh corners toward LastKnownTargetPos
        public Vector3[] ChasePathCorners;
        public int ChasePathCornerCount;
        public int ChasePathCornerIndex;
        public float ChaseRepathTimer;
        public Vector3 ChasePathTarget;     // LKP snapshot the cached path was computed for

        // Search at last-known-position after losing the target
        public float SearchEndTime = -1f;   // -1 = idle
        public Vector3 SearchScanBaseDir;   // facing captured at search start; scan oscillates around it

        // Fight-from-cover (TakeCoverNode) — SAIN-inspired pick/peek/duck cycle
        public CoverPhase CoverPhase;
        public Vector3 CoverPoint;          // hidden spot the enemy has no line of sight to
        public Vector3 CoverPeekPos;        // lateral step-out spot with clear line of sight
        public Vector3 CoverEnemyAnchor;    // LKP snapshot the cover was validated against
        public float CoverPhaseStartTime;   // ElapsedTime when the current phase began
        public float CoverPhaseEndTime;     // hold/expose deadline (ElapsedTime)
        public float CoverNextSearchTime;   // gate after a failed search — no per-tick raycast storms
        public float CoverRevalidateTimer;  // countdown to the next is-this-still-cover check

        // Spot blacklist — a point the bot was shot at while "hidden" is compromised
        // even though the raycast model still calls it cover. Survives ResetCover so
        // the immediate re-search can't just pick the same spot again.
        public Vector3 CoverBlacklistPos;
        public float CoverBlacklistUntil;   // ElapsedTime; 0 = no blacklist

        // Cover path-following: NavMesh corners toward CoverPoint
        public Vector3[] CoverPathCorners;
        public int CoverPathCornerCount;
        public int CoverPathCornerIndex;
        public float CoverRepathTimer;
        public float CoverStuckTimer;       // commanded-move with no displacement → abandon this cover
        public Vector3 CoverLastPosition;

        // Dodge state
        public bool IsDodging;
        public Vector3 DodgeDirection;
        public float DodgeTimer;

        // Consumables
        public int   MedkitsRemaining;
        public int   GrenadesRemaining;
        public int   BandagesRemaining;
        public float GrenadeCooldownTimer;
        public float GrenadeThrowDelayTimer = -1;   // -1 = idle; counts down to 0 then fires
        public float MeleeAttackCooldownTimer;

        // Alerts
        public bool WasDamaged;
        public float LastDamageTime;

        // BT re-entry
        public int RunningNodeId;

        // Debug
        public string DebugStatus;
        public BTTrace Trace;

        /// <summary>
        /// Forget the current target and all engagement-scoped state. Used by
        /// BotPerceptionSystem when target memory expires and by SearchNode when
        /// the bot gives up searching.
        /// </summary>
        public void ClearTarget()
        {
            HasTarget = false;
            TargetEId = EId.None;
            CanSeeTarget = false;
            DistanceToTarget = float.MaxValue;
            TimeSinceTargetSeen = float.MaxValue;
            ReactionTimer = 0f;
            VisionAwareness01 = 0f;
            IsAlert = false;
            CombatStance = CombatStance.Strafe;
            CombatStanceEndTime = 0f;
            AimSettle01 = 0f;
            EffectiveAccuracy = 0f;
            IsInsideEngagementView = false;
            BurstShotsLeft = 0;
            NextBurstTime = 0f;
            ChasePathCornerCount = 0;
            ChasePathCornerIndex = 0;
            ChaseRepathTimer = 0f;
            SearchEndTime = -1f;
            ResetCover();
        }

        /// <summary>Drop the current cover point and all cover-cycle progress.</summary>
        public void ResetCover()
        {
            CoverPhase = CoverPhase.None;
            CoverPoint = Vector3.zero;
            CoverPeekPos = Vector3.zero;
            CoverEnemyAnchor = Vector3.zero;
            CoverPhaseStartTime = 0f;
            CoverPhaseEndTime = 0f;
            CoverNextSearchTime = 0f;
            CoverRevalidateTimer = 0f;
            CoverPathCornerCount = 0;
            CoverPathCornerIndex = 0;
            CoverRepathTimer = 0f;
            CoverStuckTimer = 0f;
            CoverLastPosition = Vector3.zero;
        }

        public void Reset()
        {
            TargetEId = EId.None;
            LastKnownTargetPos = Vector3.zero;
            HasTarget = false;
            CanSeeTarget = false;
            DistanceToTarget = float.MaxValue;
            TimeSinceTargetSeen = float.MaxValue;
            VisionAwareness01 = 0f;
            IsAlert = false;
            LastCanSeeTime = -999f;
            PatrolWaypointIndex = 0;
            PatrolWaitTimer = 0f;
            PatrolWaitDuration = 0f;
            PatrolScanBaseDir = Vector3.zero;
            PatrolPathCornerCount = 0;
            PatrolPathCornerIndex = 0;
            PatrolPathWaypointIndex = -1;
            PatrolRepathTimer = 0f;
            PatrolStuckTimer = 0f;
            PatrolLastPosition = Vector3.zero;
            PatrolSpeedScale = 1f;
            ReactionTimer = 0f;
            NextDodgeTime = 0f;
            HealCooldownTimer = 0f;
            PerceptionTimer = 0f;
            ReactionJitter = 0f;
            StrafeDirection = 0;
            StrafeChangeTime = 0f;
            AimSwaySeed = 0f;
            CombatStance = CombatStance.Strafe;
            CombatStanceEndTime = 0f;
            ReactionTimeMult = 1f;
            AccuracyMult = 1f;
            Aggression = 1f;
            BurstShotsLeft = 0;
            NextBurstTime = 0f;
            AimSettle01 = 0f;
            EffectiveAccuracy = 0f;
            IsInsideEngagementView = false;
            HealCastEndTime = -1f;
            ChasePathCornerCount = 0;
            ChasePathCornerIndex = 0;
            ChaseRepathTimer = 0f;
            ChasePathTarget = Vector3.zero;
            SearchEndTime = -1f;
            SearchScanBaseDir = Vector3.zero;
            ResetCover();
            CoverBlacklistPos = Vector3.zero;
            CoverBlacklistUntil = 0f;
            IsDodging = false;
            DodgeDirection = Vector3.zero;
            DodgeTimer = 0f;
            MedkitsRemaining = 0;
            GrenadesRemaining = 0;
            BandagesRemaining = 0;
            GrenadeCooldownTimer = 0f;
            GrenadeThrowDelayTimer = -1f;
            MeleeAttackCooldownTimer = 0f;
            WasDamaged = false;
            LastDamageTime = -999f;
            RunningNodeId = -1;
            DebugStatus = "Idle";
        }
    }
}
