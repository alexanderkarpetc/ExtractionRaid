using Constants;
using Session;
using State;

namespace Systems
{
    public static class BandageSystem
    {
        public static void Tick(RaidState state, in RaidContext context)
        {
            var player = state.PlayerEntity;
            if (player == null) return;

            if (!state.HealthMap.TryGetValue(player.Id, out var health)) return;

            var input = context.Input;
            if (input == null) return;

            if (player.IsUsingBandage)
            {
                if (!input.BandagePressed
                    || !health.IsAlive
                    || player.IsRolling
                    || !StatusEffectSystem.HasEffect(state, player.Id, StatusEffectType.Bleeding))
                {
                    StopBandage(player, context);
                    return;
                }

                float elapsed = state.ElapsedTime - player.BandageUseStartTime;
                if (elapsed >= StatusEffectConstants.BandageUseTime)
                {
                    StatusEffectSystem.RemoveEffect(state, player.Id, StatusEffectType.Bleeding);
                    context.Events.StatusEffectRemoved(player.Id, "Bleeding");

                    if (player.ActiveBandageSlot >= 0)
                        state.Inventory.Backpack[player.ActiveBandageSlot] = null;

                    StopBandage(player, context);
                }

                return;
            }

            if (!input.BandagePressed) return;
            if (player.IsRolling || player.AreHandsBusy) return;
            if (!StatusEffectSystem.HasEffect(state, player.Id, StatusEffectType.Bleeding)) return;

            int slot = InventorySystem.FindFirstBandageSlot(state.Inventory);
            if (slot < 0) return;

            player.IsUsingBandage = true;
            player.BandageUseStartTime = state.ElapsedTime;
            player.ActiveBandageSlot = slot;
            context.Events.StatusEffectApplied(player.Id, "BandageUse");
        }

        static void StopBandage(PlayerEntityState player, in RaidContext context)
        {
            player.IsUsingBandage = false;
            player.ActiveBandageSlot = -1;
            context.Events.StatusEffectRemoved(player.Id, "BandageUse");
        }
    }
}
