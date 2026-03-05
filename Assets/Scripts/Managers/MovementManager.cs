using Session;
using State;
using UnityEngine;

namespace Managers
{
    public static class MovementManager
    {
        public const float MoveSpeed = 5f;

        public static void Tick(RaidState state, in RaidContext context)
        {
            var player = state.PlayerEntity;
            if (player == null) return;

            var input = context.Input;
            if (input == null) return;

            var moveInput = input.MoveInput;
            var moveDirection = new Vector3(moveInput.x, 0f, moveInput.y);

            if (moveDirection.sqrMagnitude > 1f)
                moveDirection.Normalize();

            player.Velocity = moveDirection * MoveSpeed;
            player.Position += player.Velocity * context.DeltaTime;
        }
    }
}
