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
        public Vector3 PatrolScanBaseDir;   // facing captured when wait started; head-scan oscillates around this

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
