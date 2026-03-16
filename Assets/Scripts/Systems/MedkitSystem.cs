using Constants;
using Session;
using State;
using UnityEngine;

namespace Systems
{
    public static class MedkitSystem
    {
        public static void Tick(RaidState state, in RaidContext context)
        {
            var player = state.PlayerEntity;
            if (player == null) return;

            if (!state.HealthMap.TryGetValue(player.Id, out var health)) return;

            var input = context.Input;
            if (input == null) return;

            if (player.IsUsingMedkit)
            {
                var medkit = GetActiveMedkit(state, player);
                if (!input.HealPressed || !health.IsAlive || medkit == null)
                {
                    StopMedkit(state, player, context);
                    return;
                }

                if (!player.MedkitHealingActive)
                {
                    if (state.ElapsedTime - player.MedkitUseStartTime >= MedConstants.UseDelay)
                        player.MedkitHealingActive = true;

                    return;
                }

                float rawHeal = MedConstants.HealPerSecond * context.DeltaTime;
                player.MedkitHealFraction += rawHeal;
                int drain = (int)player.MedkitHealFraction;
                if (drain < 1) return;

                drain = Mathf.Min(drain, medkit.StackCount);
                float actualHeal = Mathf.Min(drain, health.MaxHp - health.CurrentHp);

                health.CurrentHp = Mathf.Min(health.CurrentHp + actualHeal, health.MaxHp);
                medkit.StackCount -= drain;
                player.MedkitHealFraction -= drain;
                context.Events.EntityDamaged(player.Id, health.CurrentHp, health.MaxHp);

                if (medkit.StackCount <= 0)
                {
                    state.Inventory.Backpack[player.ActiveMedkitSlot] = null;
                    player.ActiveMedkitSlot = -1;
                    StopMedkit(state, player, context);
                    return;
                }

                if (health.CurrentHp >= health.MaxHp)
                    StopMedkit(state, player, context);

                return;
            }

            if (!input.HealPressed) return;
            if (player.IsRolling || player.IsInGrenadeMode) return;
            if (health.CurrentHp >= health.MaxHp) return;

            int slot = InventorySystem.FindFirstMedkitSlot(state.Inventory);
            if (slot < 0) return;

            player.IsUsingMedkit = true;
            player.ActiveMedkitSlot = slot;
            player.MedkitUseStartTime = state.ElapsedTime;
            player.MedkitHealingActive = false;
            player.MedkitHealFraction = 0f;
            context.Events.MedkitUseStarted();
        }

        static ItemState GetActiveMedkit(RaidState state, PlayerEntityState player)
        {
            if (player.ActiveMedkitSlot < 0) return null;
            return state.Inventory.Backpack[player.ActiveMedkitSlot];
        }

        static void StopMedkit(RaidState state, PlayerEntityState player, in RaidContext context)
        {
            player.IsUsingMedkit = false;
            player.MedkitHealingActive = false;
            player.MedkitHealFraction = 0f;
            context.Events.MedkitUseStopped();
        }
    }
}
