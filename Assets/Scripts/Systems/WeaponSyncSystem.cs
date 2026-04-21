using System.Collections.Generic;
using ApplicationCore;
using Session;
using State;

namespace Systems
{
    /// <summary>
    /// Keeps player's hotbar slots in sync with the inventory's weapon slots.
    ///
    /// Tier 0b pipeline: inventory items of legacy weapon types (Rifle / Pistol / Shotgun)
    /// are mapped to a <see cref="WeaponConfiguration"/> via <see cref="LegacyDefinitionToConfig"/>
    /// and then run through <see cref="WeaponAssemblySystem.TryAssemble"/>. The compat layer
    /// and the fallback to <c>WeaponEntityState.CreateFromDefinitionId</c> go away in Cluster E.
    ///
    /// Per D7 (ghost-weapon): assembly failure leaves the inventory item untouched,
    /// empties the hotbar slot, and emits <see cref="IRaidEvents.WeaponAssemblyFailed"/>.
    ///
    /// See docs/ai/weapon-builder/architecture.md §7, §D7.
    /// </summary>
    public static class WeaponSyncSystem
    {
        // ── TEMPORARY compat layer — removed at end of Tier 0b Cluster E ──
        // Maps legacy inventory-item DefinitionIds onto WeaponConfigurations
        // that the new pipeline can consume.
        static readonly Dictionary<string, WeaponConfiguration> LegacyDefinitionToConfig = new()
        {
            ["Rifle"] = new WeaponConfiguration(
                new PayloadCoreInstance("BallisticRound", RarityTier.Common),
                new DeliveryCoreInstance("Auto",          RarityTier.Common),
                exotic: null,
                ammoInMagazine: 30),

            ["Pistol"] = new WeaponConfiguration(
                new PayloadCoreInstance("BallisticRound", RarityTier.Common),
                new DeliveryCoreInstance("SingleAction",  RarityTier.Common),
                exotic: null,
                ammoInMagazine: 12),

            ["Shotgun"] = new WeaponConfiguration(
                new PayloadCoreInstance("BallisticRound", RarityTier.Common),
                new DeliveryCoreInstance("Scatter",       RarityTier.Common),
                exotic: null,
                ammoInMagazine: 5),
        };

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
                    player.Hotbar[i] = BuildWeaponForItem(invItem, in context);
                    continue;
                }

                if (invItem != null && hotbarWeapon != null
                    && hotbarWeapon.Id != invItem.Id)
                {
                    player.Hotbar[i] = BuildWeaponForItem(invItem, in context);

                    if (player.SelectedHotbarSlot == i)
                        player.EquippedWeapon = player.Hotbar[i];
                }
            }
        }

        /// <summary>
        /// Attempts to build a <see cref="WeaponEntityState"/> for an inventory weapon item
        /// via the assembly pipeline. Returns null on failure (ghost-weapon path per D7):
        /// item remains in inventory, hotbar slot empty, event emitted.
        /// </summary>
        static WeaponEntityState BuildWeaponForItem(ItemState invItem, in RaidContext context)
        {
            if (!LegacyDefinitionToConfig.TryGetValue(invItem.DefinitionId, out var config))
            {
                // Unknown weapon type: nothing the pipeline can do. Ghost.
                context.Events.WeaponAssemblyFailed(
                    invItem.DefinitionId,
                    $"No weapon configuration found for definition '{invItem.DefinitionId}'.");
                return null;
            }

            if (context.CoreDefinitions == null)
            {
                // Registry missing (e.g. CoreDefinitionDatabase.asset not created).
                // Fall back to the legacy factory so gameplay keeps working during Tier 0b rollout.
                // This fallback is removed alongside factories in Cluster E.
                return WeaponEntityState.CreateFromDefinitionId(invItem.Id, invItem.DefinitionId);
            }

            if (!WeaponAssemblySystem.TryAssemble(config, context.CoreDefinitions,
                    out var result, out var reason))
            {
                context.Events.WeaponAssemblyFailed(invItem.DefinitionId, reason);
                return null;
            }

            return new WeaponEntityState
            {
                Id             = invItem.Id,
                PrefabId       = GetPrefabIdForLegacy(invItem.DefinitionId),

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

        // TEMPORARY (removed in Cluster E): prefab id still tied to legacy definition.
        // In a later tier PrefabId will be derived from the composition
        // (e.g. delivery form-factor + payload VFX skin).
        static string GetPrefabIdForLegacy(string legacyDefinitionId) => legacyDefinitionId switch
        {
            "Rifle"   => "Weapon_Rifle",
            "Pistol"  => "Weapon_Pistol",
            "Shotgun" => "Weapon_Shotgun",
            _         => null,
        };
    }
}
