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

            var cfg = context.StaminaConfig;

            var moveInput = input.MoveInput;
            bool isMoving = moveInput.sqrMagnitude > 0.01f;
            bool wantsToSprint = input.SprintPressed && isMoving;

            // Exhaustion hysteresis: once empty, lock sprint until stamina recovers past
            // the threshold. !IsExhausted alone replaces the old `Stamina > 0` so a single
            // regen tick can't re-enable sprint at empty (no stutter-sprint).
            bool canSprint = !player.IsExhausted
                             && player.Stamina > 0f
                             && !player.IsRolling
                             && !player.AreHandsBusy
                             && !player.IsADS;

            player.IsSprinting = wantsToSprint && canSprint;

            if (player.IsSprinting)
            {
                player.Stamina -= cfg.SprintDrainRate * context.DeltaTime;
                player.Stamina = Mathf.Max(player.Stamina, 0f);
                player.LastSprintStopTime = context.Time.Time;
            }
            else
            {
                float timeSinceStop = context.Time.Time - player.LastSprintStopTime;
                if (timeSinceStop >= cfg.RegenDelay)
                {
                    player.Stamina += cfg.RegenRate * context.DeltaTime;
                    player.Stamina = Mathf.Min(player.Stamina, player.MaxStamina);
                }
            }

            // Latch exhaustion at empty; release once recovered past the threshold.
            if (player.Stamina <= 0f)
                player.IsExhausted = true;
            else if (player.IsExhausted
                     && player.Stamina >= player.MaxStamina * cfg.ExhaustionRecoveryRatio)
                player.IsExhausted = false;
        }
    }
}
