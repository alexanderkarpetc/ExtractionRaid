using ApplicationCore;
using Adapters;
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

            var inventory = App.Instance.Player.Inventory;
            var activeMedkit = GetActiveMedkit(player, inventory);
            bool wantsHeal = QuickSlotRules.IsMedkit(
                    QuickSlotSystem.GetActiveDefinitionId(player, inventory)) && player.QuickSlotHeld;
            if (player.ActiveQuickSlot == PlayerEntityState.InventoryUseQuickSlot)
                wantsHeal = player.IsUsingMedkit && QuickSlotRules.IsMedkit(activeMedkit?.DefinitionId);

            if (player.IsUsingMedkit)
            {
                var medkit = activeMedkit;
                if (!wantsHeal || !health.IsAlive || medkit == null)
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

                drain = Mathf.Min(drain, medkit.CurrentResource);
                float actualHeal = Mathf.Min(drain, health.MaxHp - health.CurrentHp);

                health.CurrentHp = Mathf.Min(health.CurrentHp + actualHeal, health.MaxHp);
                medkit.Resource = medkit.CurrentResource - drain;
                player.MedkitHealFraction -= drain;
                context.Events.EntityDamaged(player.Id, health.CurrentHp, health.MaxHp);

                if (medkit.Resource <= 0)
                {
                    inventory.Backpack[player.ActiveMedkitSlot] = null;
                    inventory.Version++;
                    player.ActiveMedkitSlot = -1;
                    StopMedkit(state, player, context);
                    return;
                }

                if (health.CurrentHp >= health.MaxHp)
                    StopMedkit(state, player, context);

                return;
            }

            if (!wantsHeal) return;
            if (player.IsRolling || player.AreHandsBusy) return;
            if (health.CurrentHp >= health.MaxHp) return;

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
            if (player.IsRolling || player.AreHandsBusy || health.CurrentHp >= health.MaxHp) return false;
            if (!QuickSlotRules.IsMedkit(inventory.Backpack[slot]?.DefinitionId)) return false;

            player.IsUsingMedkit = true;
            player.ActiveMedkitSlot = slot;
            player.MedkitUseStartTime = state.ElapsedTime;
            player.MedkitHealingActive = false;
            player.MedkitHealFraction = 0f;
            if (fromInventory)
            {
                player.ActiveQuickSlot = PlayerEntityState.InventoryUseQuickSlot;
                player.QuickSlotHeld = true;
            }
            events.MedkitUseStarted();
            return true;
        }

        static ItemState GetActiveMedkit(PlayerEntityState player, InventoryState inventory)
        {
            if (player.ActiveMedkitSlot < 0) return null;
            return inventory.Backpack[player.ActiveMedkitSlot];
        }

        static void StopMedkit(RaidState state, PlayerEntityState player, in RaidContext context)
        {
            player.IsUsingMedkit = false;
            player.MedkitHealingActive = false;
            player.MedkitHealFraction = 0f;
            player.ActiveMedkitSlot = -1;
            if (player.ActiveQuickSlot == PlayerEntityState.InventoryUseQuickSlot)
            {
                player.ActiveQuickSlot = -1;
                player.QuickSlotHeld = false;
            }
            context.Events.MedkitUseStopped();
        }
    }
}
