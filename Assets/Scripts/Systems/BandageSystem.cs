using ApplicationCore;
using Adapters;
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
            bool wantsBandage = QuickSlotRules.IsBandage(
                    QuickSlotSystem.GetActiveDefinitionId(player, inventory)) && player.QuickSlotHeld;
            if (player.ActiveQuickSlot == PlayerEntityState.InventoryUseQuickSlot)
            {
                var active = player.ActiveBandageSlot >= 0 && player.ActiveBandageSlot < inventory.Backpack.Length
                    ? inventory.Backpack[player.ActiveBandageSlot]
                    : null;
                wantsBandage = player.IsUsingBandage && QuickSlotRules.IsBandage(active?.DefinitionId);
            }

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
            TryStart(state, inventory, slot, fromInventory: false, context.Events);
        }

        public static bool TryStartFromInventory(RaidState state, InventoryState inventory, int slot,
            IRaidEvents events)
        {
            return TryStart(state, inventory, slot, fromInventory: true, events);
        }

        static bool TryStart(RaidState state, InventoryState inventory, int slot, bool fromInventory,
            IRaidEvents events)
        {
            var player = state?.PlayerEntity;
            if (player == null || inventory == null || events == null) return false;
            if (slot < 0 || slot >= inventory.Backpack.Length) return false;
            if (!state.HealthMap.TryGetValue(player.Id, out var health) || !health.IsAlive) return false;
            if (player.IsRolling || player.AreHandsBusy) return false;
            if (!QuickSlotRules.IsBandage(inventory.Backpack[slot]?.DefinitionId)) return false;

            bool isBleeding = StatusEffectSystem.HasEffect(state, player.Id, StatusEffectType.Bleeding);
            if (!isBleeding && health.CurrentHp >= health.MaxHp) return false;

            player.IsUsingBandage = true;
            player.BandageUseStartTime = state.ElapsedTime;
            player.ActiveBandageSlot = slot;
            if (fromInventory)
            {
                player.ActiveQuickSlot = PlayerEntityState.InventoryUseQuickSlot;
                player.QuickSlotHeld = true;
            }
            events.StatusEffectApplied(player.Id, "BandageUse");
            return true;
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
            if (player.ActiveQuickSlot == PlayerEntityState.InventoryUseQuickSlot)
            {
                player.ActiveQuickSlot = -1;
                player.QuickSlotHeld = false;
            }
            context.Events.StatusEffectRemoved(player.Id, "BandageUse");
        }
    }
}
