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

        // Cover
        public Vector3 CoverPosition;
        public bool HasCover;

        // Timers
        public float ReactionTimer;
        public float DodgeCooldownTimer;
        public float HealCooldownTimer;
        public float PerceptionTimer;

        // Dodge state
        public bool IsDodging;
        public Vector3 DodgeDirection;
        public float DodgeTimer;

        // Grenade
        public int   GrenadesRemaining;
        public float GrenadeCooldownTimer;
        public float GrenadeThrowDelayTimer = -1;   // -1 = idle; counts down to 0 then fires

        // Alerts
        public bool WasDamaged;

        // BT re-entry
        public int RunningNodeId;

        // Debug
        public string DebugStatus;

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
            CoverPosition = Vector3.zero;
            HasCover = false;
            ReactionTimer = 0f;
            DodgeCooldownTimer = 0f;
            HealCooldownTimer = 0f;
            PerceptionTimer = 0f;
            IsDodging = false;
            DodgeDirection = Vector3.zero;
            DodgeTimer = 0f;
            GrenadesRemaining = 0;
            GrenadeCooldownTimer = 0f;
            GrenadeThrowDelayTimer = -1f;
            WasDamaged = false;
            RunningNodeId = -1;
            DebugStatus = "Idle";
        }
    }
}
