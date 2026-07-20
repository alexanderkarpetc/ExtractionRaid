using Adapters;
using ApplicationCore;
using Constants;
using State;
using UnityEngine;

namespace Systems
{
    public static class PlayerSpawnSystem
    {
        public static void SpawnPlayer(RaidState state, Vector3 spawnPosition, IRaidEvents events, string levelId = null)
        {
            if (state.PlayerEntity != null) return;

            var playerId = state.AllocateEId();
            state.PlayerEntity = PlayerEntityState.Create(playerId, spawnPosition);
            state.HealthMap[playerId] = HealthState.Create(BotConstants.PlayerMaxHp);

            var inventory = App.Instance.Player.Inventory;
            var profile = App.Instance.Player.ProfileState;
            bool isTestRange = IsTestRange(levelId);
            // Test scenes always regrant a fresh cheat loadout each spawn. Real raids
            // grant the starting kit ONLY once per profile — after a KIA gear-wipe the
            // player re-gears from stash, not from a free handout. (M1.1 gear-loss)
            if (ShouldGrantStartingKit(levelId, profile.HasReceivedStartingKit))
            {
                inventory.ClearAll();
                GiveStartingLoadout(state, inventory);
                if (!isTestRange)
                    profile.HasReceivedStartingKit = true;
            }

            for (int i = 0; i < PlayerEntityState.HotbarSize; i++)
            {
                var invItem = inventory.WeaponSlots[i];
                if (invItem == null) continue;
                var weapon = WeaponSyncSystem.BuildWeaponForItem(
                    invItem, App.Instance.CoreDefinitions, events);
                if (weapon == null) continue;
                state.PlayerEntity.Hotbar[i] = weapon;
            }

            for (int i = 0; i < PlayerEntityState.HotbarSize; i++)
            {
                if (state.PlayerEntity.Hotbar[i] != null)
                {
                    state.PlayerEntity.SelectedHotbarSlot = i;
                    state.PlayerEntity.EquippedWeapon = state.PlayerEntity.Hotbar[i];
                    break;
                }
            }

            EquipmentSystem.SyncArmorFromInventory(state, playerId, inventory);

            events.PlayerSpawned(playerId);
        }

        // Test/dev scenes always get a fresh cheat loadout each spawn. Real raids grant
        // the starting kit only once per profile (see M1.1 gear-loss). Pure + public so
        // EditMode tests can exercise the gate without an App/Player singleton.
        public static bool IsTestRange(string levelId) =>
            levelId == "shooting_range" || levelId == "kill_feel_range"
            || levelId == "horde_range" || levelId == "ranged_range"
            || levelId == "feedback_range";

        public static bool ShouldGrantStartingKit(string levelId, bool hasReceivedStartingKit) =>
            IsTestRange(levelId) || !hasReceivedStartingKit;

        static void GiveStartingLoadout(RaidState state, InventoryState inventory)
        {
            // CHEAT loadout (2026-05-05): all 6 weapon variants assembled via Builder
            // pipeline (2 payloads × 3 deliveries) + equipped armor + minimal consumables.
            // Used for testing weapon/combat permutations on test scenes; normal raid
            // flow preserves player inventory across sessions.
            var combos = new (string payload, string delivery, int magSize)[]
            {
                ("BallisticRound", "Auto",         30),
                ("BallisticRound", "SingleAction", 12),
                ("BallisticRound", "Scatter",      5),
                ("LaserCharge",    "Auto",         30),
                ("LaserCharge",    "SingleAction", 12),
                ("LaserCharge",    "Scatter",      5),
            };

            // First 2 weapons go into hotbar slots (HotbarSize = 2)
            inventory.WeaponSlots[0] = MakeWeapon(state, combos[0]);
            inventory.WeaponSlots[1] = MakeWeapon(state, combos[1]);

            // Remaining 4 weapons into backpack (slots 0-3)
            for (int i = 2; i < combos.Length; i++)
                inventory.Backpack[i - 2] = MakeWeapon(state, combos[i]);

            // Armor equipped directly (HelmetSlot + BodyArmorSlot — not backpack)
            inventory.HelmetSlot = ItemState.Create(state.AllocateEId(), "Helmet_Basic");
            inventory.BodyArmorSlot = ItemState.Create(state.AllocateEId(), "Armor_Basic");

            // Minimal consumables: 1 grenade, 1 medkit, 1 bandage
            inventory.Backpack[5] = ItemState.Create(state.AllocateEId(), "Grenade");
            // One medkit (resource pool defaults to full via ItemDefinition.MaxResource).
            inventory.Backpack[6] = ItemState.Create(state.AllocateEId(), "Medkit");
            inventory.Backpack[7] = ItemState.Create(state.AllocateEId(), "Bandage");

            // Attachment mods are no longer granted here — they drop from loot (containers + bots;
            // see LootSystem / ContainerConstants.AttachmentModDrops). Dev cheat "Give All Mods" stays for testing.
        }

        static ItemState MakeWeapon(RaidState state, (string payload, string delivery, int magSize) combo)
        {
            // CHEAT loadout: random rarity per core so the dual-rarity inventory frame +
            // tooltip colors are immediately testable on a fresh player. Stats fall back
            // to Common until per-tier values are authored (Tier 4b) — rarity is visual-only.
            var config = new WeaponConfiguration(
                payload:        new PayloadCoreInstance(combo.payload, RandomRarity()),
                delivery:       new DeliveryCoreInstance(combo.delivery, RandomRarity()),
                exotic:         null,
                ammoInMagazine: combo.magSize);
            return ItemState.CreateWeapon(state.AllocateEId(), "Weapon", config);
        }

        static RarityTier RandomRarity() => (RarityTier)UnityEngine.Random.Range(0, 5);
    }
}
