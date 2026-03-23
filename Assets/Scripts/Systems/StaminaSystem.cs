using Constants;
using Session;
using State;
using UnityEngine;

namespace Systems
{
    public static class StaminaSystem
    {
        public static void Tick(RaidState state, in RaidContext context)
        {
            var player = state.PlayerEntity;
            if (player == null) return;

            var input = context.Input;
            if (input == null) return;

            var moveInput = input.MoveInput;
            bool isMoving = moveInput.sqrMagnitude > 0.01f;
            bool wantsToSprint = input.SprintPressed && isMoving;
            bool canSprint = player.Stamina > 0f
                             && !player.IsRolling
                             && !player.AreHandsBusy
                             && !player.IsADS;

            player.IsSprinting = wantsToSprint && canSprint;

            if (player.IsSprinting)
            {
                player.Stamina -= StaminaConstants.SprintDrainRate * context.DeltaTime;
                player.Stamina = Mathf.Max(player.Stamina, 0f);
                player.LastSprintStopTime = context.Time.Time;
            }
            else
            {
                float timeSinceStop = context.Time.Time - player.LastSprintStopTime;
                if (timeSinceStop >= StaminaConstants.RegenDelay)
                {
                    player.Stamina += StaminaConstants.RegenRate * context.DeltaTime;
                    player.Stamina = Mathf.Min(player.Stamina, player.MaxStamina);
                }
            }
        }
    }
}
