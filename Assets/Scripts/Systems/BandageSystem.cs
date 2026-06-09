using ApplicationCore;
using Constants;
using Session;
using State;
using UnityEngine;

namespace Systems
{
    public static class BandageSystem
    {
        public static void Tick(RaidState state, in RaidContext context)
        {
            var player = state.PlayerEntity;
            if (player == null) return;

            if (!state.HealthMap.TryGetValue(player.Id, out var health)) return;

            var inventory = App.Instance.Player.Inventory;
            bool wantsBandage = QuickSlotSystem.GetActiveDefinitionId(player, inventory) == "Bandage"
                && player.QuickSlotHeld;

            if (player.IsUsingBandage)
            {
                // Cancel if released, dead, or interrupted by a roll. Note we no longer
                // cancel when bleeding clears — a bandage also heals, so it stays useful.
                if (!wantsBandage || !health.IsAlive || player.IsRolling)
                {
                    StopBandage(player, context);
                    return;
                }

                float elapsed = state.ElapsedTime - player.BandageUseStartTime;
                if (elapsed >= StatusEffectConstants.BandageUseTime)
                {
                    ApplyBandage(state, player, health, inventory, context);
                    StopBandage(player, context);
                }

                return;
            }

            if (!wantsBandage) return;
            if (player.IsRolling || player.AreHandsBusy) return;

            // Usable while bleeding OR while below max HP — a bandage both stems
            // bleeding and restores a little health.
            bool isBleeding = StatusEffectSystem.HasEffect(state, player.Id, StatusEffectType.Bleeding);
            bool needsHeal = health.CurrentHp < health.MaxHp;
            if (!isBleeding && !needsHeal) return;

            int slot = QuickSlotSystem.GetActiveBoundSlot(player, inventory);
            if (slot < 0) return;
            if (inventory.Backpack[slot]?.DefinitionId != "Bandage") return;

            player.IsUsingBandage = true;
            player.BandageUseStartTime = state.ElapsedTime;
            player.ActiveBandageSlot = slot;
            context.Events.StatusEffectApplied(player.Id, "BandageUse");
        }

        static void ApplyBandage(RaidState state, PlayerEntityState player, HealthState health,
            InventoryState inventory, in RaidContext context)
        {
            // 1. Reduce bleeding if present.
            if (StatusEffectSystem.HasEffect(state, player.Id, StatusEffectType.Bleeding))
            {
                StatusEffectSystem.DowngradeBleed(state, player.Id);
                int levelAfter = StatusEffectSystem.GetBleedLevel(state, player.Id);
                if (levelAfter == 0)
                    context.Events.StatusEffectRemoved(player.Id, "Bleeding");
                else
                    context.Events.StatusEffectApplied(player.Id, "BleedingL1");
            }

            // 2. Restore a flat chunk of HP.
            float healAmount = ItemDefinition.Get("Bandage")?.HealAmount ?? 0f;
            if (healAmount > 0f && health.CurrentHp < health.MaxHp)
            {
                health.CurrentHp = Mathf.Min(health.CurrentHp + healAmount, health.MaxHp);
                context.Events.EntityDamaged(player.Id, health.CurrentHp, health.MaxHp);
            }

            // 3. Consume one bandage from the stack (only clear the slot when empty).
            int activeSlot = player.ActiveBandageSlot;
            if (activeSlot >= 0)
            {
                var item = inventory.Backpack[activeSlot];
                if (item != null)
                {
                    item.StackCount--;
                    if (item.StackCount <= 0)
                        inventory.Backpack[activeSlot] = null;
                    inventory.Version++;
                }
            }
        }

        static void StopBandage(PlayerEntityState player, in RaidContext context)
        {
            player.IsUsingBandage = false;
            player.ActiveBandageSlot = -1;
            context.Events.StatusEffectRemoved(player.Id, "BandageUse");
        }
    }
}
