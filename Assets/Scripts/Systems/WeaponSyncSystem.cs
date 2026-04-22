using Adapters;
using ApplicationCore;
using Session;
using State;

namespace Systems
{
    /// <summary>
    /// Keeps player's hotbar slots in sync with the inventory's weapon slots.
    ///
    /// Each weapon inventory item carries its own <see cref="WeaponConfiguration"/>
    /// (attached at spawn time by <see cref="WeaponItemFactory"/>; later — by the
    /// Builder UI). <see cref="WeaponAssemblySystem.TryAssemble"/> resolves it into a
    /// runtime <see cref="WeaponEntityState"/>.
    ///
    /// Per D7 (ghost-weapon): assembly failure leaves the inventory item untouched,
    /// empties the hotbar slot, and emits <see cref="IRaidEvents.WeaponAssemblyFailed"/>.
    ///
    /// See docs/ai/weapon-builder/architecture.md §7, §D7.
    /// </summary>
    public static class WeaponSyncSystem
    {
        public static void Tick(RaidState state, in RaidContext context)
        {
            var player = state.PlayerEntity;
            if (player == null) return;

            var inventory = App.Instance.Player.Inventory;
            int slotCount = PlayerEntityState.HotbarSize;

            for (int i = 0; i < slotCount; i++)
            {
                var invItem = inventory.WeaponSlots[i];
                var hotbarWeapon = player.Hotbar[i];

                if (invItem == null && hotbarWeapon != null)
                {
                    player.Hotbar[i] = null;

                    if (player.SelectedHotbarSlot == i)
                    {
                        player.SelectedHotbarSlot = -1;
                        player.EquippedWeapon = null;
                    }

                    if (player.PendingHotbarSlot == i)
                        player.PendingHotbarSlot = -1;

                    continue;
                }

                if (invItem != null && hotbarWeapon == null)
                {
                    player.Hotbar[i] = BuildWeaponForItem(invItem, context.CoreDefinitions, context.Events);
                    continue;
                }

                if (invItem != null && hotbarWeapon != null
                    && hotbarWeapon.Id != invItem.Id)
                {
                    player.Hotbar[i] = BuildWeaponForItem(invItem, context.CoreDefinitions, context.Events);

                    if (player.SelectedHotbarSlot == i)
                        player.EquippedWeapon = player.Hotbar[i];
                }
            }
        }

        /// <summary>
        /// Builds a <see cref="WeaponEntityState"/> for an inventory weapon item via the
        /// assembly pipeline. Returns null on failure (ghost-weapon path per D7): caller
        /// should leave the inventory item in place and treat the hotbar slot as empty.
        ///
        /// Shared by <see cref="Tick"/> and <see cref="PlayerSpawnSystem"/> so initial
        /// hotbar population and ongoing inventory sync use the same code path.
        /// </summary>
        public static WeaponEntityState BuildWeaponForItem(
            ItemState invItem,
            ICoreDefinitionRegistry registry,
            IRaidEvents events)
        {
            if (!invItem.HasWeaponConfiguration)
            {
                events?.WeaponAssemblyFailed(
                    invItem.DefinitionId,
                    $"Inventory item '{invItem.DefinitionId}' has no WeaponConfiguration attached.");
                return null;
            }

            if (registry == null)
            {
                events?.WeaponAssemblyFailed(
                    invItem.DefinitionId,
                    "Core definition registry is not available.");
                return null;
            }

            var config = invItem.WeaponConfiguration;

            if (!WeaponAssemblySystem.TryAssemble(config, registry, out var result, out var reason))
            {
                events?.WeaponAssemblyFailed(invItem.DefinitionId, reason);
                return null;
            }

            return new WeaponEntityState
            {
                Id             = invItem.Id,
                PrefabId       = ResolveWeaponPrefab(invItem.Definition, result.DeliveryDefinition),

                PayloadCore        = config.Payload,
                DeliveryCore       = config.Delivery,
                HasExotic          = config.Exotic.HasValue,
                ExoticMod          = config.Exotic ?? default,
                PayloadDefinition  = result.PayloadDefinition,
                DeliveryDefinition = result.DeliveryDefinition,
                ExoticDefinition   = result.ExoticDefinition,

                Stats = result.Stats,

                AmmoType       = result.PayloadDefinition?.AmmoType,
                AmmoInMagazine = config.AmmoInMagazine,

                LastFireTime   = -999f,
                Phase          = WeaponPhase.Ready,
                PhaseStartTime = 0f,
            };
        }

        /// <summary>
        /// Chooses the weapon prefab for a built weapon.
        /// Priority: explicit <c>ItemDefinition.WeaponPrefabId</c> (legacy Rifle/Pistol
        /// inventory items carry one) → derived from Delivery form-factor (Builder-created
        /// weapons whose <c>ItemDefinition</c> is the generic "Weapon" entry).
        /// </summary>
        static string ResolveWeaponPrefab(ItemDefinition itemDef, DeliveryCoreDefinition deliveryDef)
        {
            if (!string.IsNullOrEmpty(itemDef?.WeaponPrefabId))
                return itemDef.WeaponPrefabId;
            return deliveryDef?.FormFactor switch
            {
                "Pistol" => "Weapon_Pistol",
                "Rifle"  => "Weapon_Rifle",
                _        => "Weapon_Rifle",
            };
        }
    }
}
