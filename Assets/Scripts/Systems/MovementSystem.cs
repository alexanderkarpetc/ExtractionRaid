using Constants;
using Dev;
using Session;
using State;
using UnityEngine;

namespace Systems
{
    public static class MovementSystem
    {
        public const float MoveSpeed = 5f;

        public static void Tick(RaidState state, in RaidContext context)
        {
            var player = state.PlayerEntity;
            if (player == null) return;

            if (player.IsRolling)
            {
                player.Velocity = player.RollDirection * DodgeConstants.Speed;
            }
            else
            {
                var input = context.Input;
                if (input == null) return;

                var moveInput = input.MoveInput;
                var moveDirection = new Vector3(moveInput.x, 0f, moveInput.y);

                if (moveDirection.sqrMagnitude > 1f)
                    moveDirection.Normalize();

                player.Velocity = moveDirection * (MoveSpeed * DevCheats.MoveSpeedMultiplier);
            }

            var candidatePos = player.Position + player.Velocity * context.DeltaTime;

            if (context.NavMesh != null &&
                context.NavMesh.SamplePosition(candidatePos, 1f, out var clampedPos))
            {
                player.Position = clampedPos;
            }
            else
            {
                player.Position = candidatePos;
            }
        }
    }
}
