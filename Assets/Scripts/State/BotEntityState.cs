using UnityEngine;

namespace State
{
    public class BotEntityState
    {
        public EId Id;
        public string TypeId;

        // Spatial
        public Vector3 Position;
        public Vector3 Velocity;
        public Vector3 FacingDirection;
        public Vector3 AimDirection;

        // Weapon
        public WeaponEntityState Weapon;

        // AI working memory
        public BotBlackboard Blackboard;

        // Roll
        public bool IsRolling;
        public Vector3 RollDirection;
        public float RollStartTime;
        public float RollCooldownEndTime;

        // Intents (written by BT, consumed by movement/combat systems)
        public Vector3 DesiredVelocity;
        public Vector3 DesiredAimPoint;
        public bool WantsToFire;
        public bool WantsToDodge;
        public bool WantsToHeal;

        public static BotEntityState Create(EId id, string typeId, Vector3 position, Vector3[] patrolWaypoints)
        {
            return new BotEntityState
            {
                Id = id,
                TypeId = typeId,
                Position = position,
                Velocity = Vector3.zero,
                FacingDirection = Vector3.forward,
                AimDirection = Vector3.forward,
                Blackboard = new BotBlackboard { PatrolWaypoints = patrolWaypoints },
                DesiredVelocity = Vector3.zero,
                DesiredAimPoint = Vector3.zero,
                WantsToFire = false,
                WantsToDodge = false,
                WantsToHeal = false,
            };
        }

        public void ClearIntents()
        {
            DesiredVelocity = Vector3.zero;
            DesiredAimPoint = Vector3.zero;
            WantsToFire = false;
            WantsToDodge = false;
            WantsToHeal = false;
        }
    }
}
