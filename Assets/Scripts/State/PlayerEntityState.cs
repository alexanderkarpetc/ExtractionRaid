using UnityEngine;

namespace State
{
    public class PlayerEntityState
    {
        public EId Id;
        public Vector3 Position;
        public Vector3 Velocity;
        public Vector3 FacingDirection;
        public Vector3 AimDirection;
        public PlayerCombatState Combat;

        public static PlayerEntityState Create(EId id, Vector3 spawnPosition)
        {
            return new PlayerEntityState
            {
                Id = id,
                Position = spawnPosition,
                Velocity = Vector3.zero,
                FacingDirection = Vector3.forward,
                AimDirection = Vector3.forward,
                Combat = PlayerCombatState.Create(),
            };
        }
    }
}
