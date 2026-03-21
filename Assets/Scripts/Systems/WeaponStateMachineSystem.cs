using Dev;
using Session;
using State;

namespace Systems
{
    public static class WeaponStateMachineSystem
    {
        public static void Tick(RaidState state, in RaidContext context)
        {
            var player = state.PlayerEntity;
            if (player == null) return;
            if (player.AreHandsBusy) return;
            if (player.IsInMenu) return;

            var weapon = player.EquippedWeapon;
            if (weapon == null)
            {
                ProcessUnarmedPending(player, state, in context);
                return;
            }

            var elapsed = state.ElapsedTime;
            var phaseDuration = elapsed - weapon.PhaseStartTime;

            switch (weapon.Phase)
            {
                case WeaponPhase.Ready:
                    if (context.Input != null && context.Input.ReloadPressed
                        && AmmoSystem.CanReload(weapon, state.Inventory))
                    {
                        weapon.Phase = WeaponPhase.Reloading;
                        weapon.PhaseStartTime = elapsed;
                        context.Events.WeaponReloadStarted(weapon.PrefabId);
                    }
                    else
                    {
                        ProcessSwapIntent(player, weapon, state, in context);
                    }
                    break;

                case WeaponPhase.Firing:
                    weapon.Phase = WeaponPhase.Cooldown;
                    weapon.PhaseStartTime = elapsed;
                    break;

                case WeaponPhase.Cooldown:
                    float effectiveInterval = weapon.FireInterval / DevCheats.FireRateMultiplier;
                    if (phaseDuration >= effectiveInterval)
                    {
                        weapon.Phase = WeaponPhase.Ready;
                        weapon.PhaseStartTime = elapsed;
                    }

                    if (weapon.Phase == WeaponPhase.Ready && context.Input != null
                        && context.Input.ReloadPressed
                        && AmmoSystem.CanReload(weapon, state.Inventory))
                    {
                        weapon.Phase = WeaponPhase.Reloading;
                        weapon.PhaseStartTime = elapsed;
                        context.Events.WeaponReloadStarted(weapon.PrefabId);
                    }
                    else
                    {
                        ProcessSwapIntent(player, weapon, state, in context);
                    }
                    break;

                case WeaponPhase.Equipping:
                    if (phaseDuration >= weapon.EquipTime)
                    {
                        weapon.Phase = WeaponPhase.Ready;
                        weapon.PhaseStartTime = elapsed;
                        context.Events.WeaponEquipFinished(weapon.PrefabId);
                    }
                    else
                    {
                        ProcessSwapIntent(player, weapon, state, in context);
                    }

                    break;

                case WeaponPhase.Unequipping:
                    if (phaseDuration >= weapon.UnequipTime)
                    {
                        CompleteUnequip(player, state, in context);
                    }
                    break;

                case WeaponPhase.Reloading:
                    if (phaseDuration >= weapon.ReloadTime)
                    {
                        AmmoSystem.CompleteReload(weapon, state.Inventory);
                        weapon.Phase = WeaponPhase.Ready;
                        weapon.PhaseStartTime = elapsed;
                        context.Events.WeaponReloadFinished(weapon.PrefabId);
                    }
                    else
                    {
                        ProcessSwapIntent(player, weapon, state, in context);
                    }
                    break;
            }
        }

        static void ProcessSwapIntent(PlayerEntityState player, WeaponEntityState weapon,
            RaidState state, in RaidContext context)
        {
            if (player.PendingHotbarSlot < 0) return;

            weapon.Phase = WeaponPhase.Unequipping;
            weapon.PhaseStartTime = state.ElapsedTime;
            context.Events.WeaponUnequipStarted(weapon.PrefabId);
        }

        static void CompleteUnequip(PlayerEntityState player, RaidState state, in RaidContext context)
        {
            var pendingSlot = player.PendingHotbarSlot;

            if (pendingSlot >= 0 && pendingSlot == player.SelectedHotbarSlot)
            {
                // Toggle off: go unarmed
                player.SelectedHotbarSlot = -1;
                player.EquippedWeapon = null;
                player.PendingHotbarSlot = -1;
            }
            else if (pendingSlot >= 0)
            {
                var targetWeapon = player.Hotbar[pendingSlot];
                player.SelectedHotbarSlot = pendingSlot;
                player.PendingHotbarSlot = -1;

                if (targetWeapon != null)
                {
                    player.EquippedWeapon = targetWeapon;
                    targetWeapon.Phase = WeaponPhase.Equipping;
                    targetWeapon.PhaseStartTime = state.ElapsedTime;
                    context.Events.WeaponEquipStarted(targetWeapon.PrefabId);
                }
                else
                {
                    player.EquippedWeapon = null;
                }
            }
            else
            {
                // No pending slot (edge case): go unarmed
                player.EquippedWeapon = null;
                player.SelectedHotbarSlot = -1;
            }
        }

        static void ProcessUnarmedPending(PlayerEntityState player, RaidState state, in RaidContext context)
        {
            if (player.PendingHotbarSlot < 0) return;

            var pendingSlot = player.PendingHotbarSlot;
            var targetWeapon = player.Hotbar[pendingSlot];
            player.SelectedHotbarSlot = pendingSlot;
            player.PendingHotbarSlot = -1;

            if (targetWeapon != null)
            {
                player.EquippedWeapon = targetWeapon;
                targetWeapon.Phase = WeaponPhase.Equipping;
                targetWeapon.PhaseStartTime = state.ElapsedTime;
                context.Events.WeaponEquipStarted(targetWeapon.PrefabId);
            }
            else
            {
                player.EquippedWeapon = null;
            }
        }
    }
}
