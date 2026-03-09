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

        // BT re-entry
        public int RunningNodeId;

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
            RunningNodeId = -1;
        }
    }
}
