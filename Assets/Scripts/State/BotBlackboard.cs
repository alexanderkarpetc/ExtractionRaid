using UnityEngine;

namespace State
{
    public class BotBlackboard
    {
        // Target tracking
        public EId TargetEId;
        public Vector3 LastKnownTargetPos;
        public bool HasTarget;
        public bool CanSeeTarget;
        public float DistanceToTarget;
        public float TimeSinceTargetSeen;

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
        public float DodgeCooldownTimer;
        public float HealCooldownTimer;
        public float PerceptionTimer;

        // Humanization — rolled per target-acquisition so each engagement feels distinct
        public float ReactionJitter;        // added to config.ReactionTime in ShootNode
        public int   StrafeDirection;       // -1 or +1 along perp-to-target axis
        public float StrafeChangeTime;      // ElapsedTime at which to flip strafe direction
        public float AimSwaySeed;           // per-bot phase offset for aim-sway noise

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

        public void Reset()
        {
            TargetEId = EId.None;
            LastKnownTargetPos = Vector3.zero;
            HasTarget = false;
            CanSeeTarget = false;
            DistanceToTarget = float.MaxValue;
            TimeSinceTargetSeen = float.MaxValue;
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
            DodgeCooldownTimer = 0f;
            HealCooldownTimer = 0f;
            PerceptionTimer = 0f;
            ReactionJitter = 0f;
            StrafeDirection = 0;
            StrafeChangeTime = 0f;
            AimSwaySeed = 0f;
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
