using Adapters;
using ApplicationCore;
using Constants;
using State;
using UnityEngine;

namespace Systems
{
    public static class PlayerSpawnSystem
    {
        public static void SpawnPlayer(RaidState state, Vector3 spawnPosition, IRaidEvents events,
            string levelId = null, float maxHp = BotConstants.PlayerMaxHp)
        {
            if (state.PlayerEntity != null) return;

            var playerId = state.AllocateEId();
            state.PlayerEntity = PlayerEntityState.Create(playerId, spawnPosition);
            state.HealthMap[playerId] = HealthState.Create(Mathf.Max(1f, maxHp));

            var inventory = App.Instance.Player.Inventory;
            // Test scenes always get the full cheat loadout (all archetypes) for combat
            // testing. Real play grants the weak baseline floor whenever the player has
            // nothing — a fresh save, or after a KIA gear-wipe with an empty stash — so the
            // risk loop can take the player's GOOD gear on death without ever soft-locking
            // them. A non-empty inventory (re-geared from stash / carried loot) is left alone.
            if (IsTestRange(levelId))
            {
                inventory.ClearAll();
                GiveTestRangeLoadout(state, inventory);
            }
            else if (IsInventoryEmpty(inventory))
            {
                inventory.ClearAll();
                GiveBaselineFloor(state, inventory);
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

        // Dev/combat test scenes — regranted a full cheat loadout each spawn. Public + pure
        // so EditMode tests can classify a level id without an App/Player singleton.
        public static bool IsTestRange(string levelId) =>
            levelId == "shooting_range" || levelId == "kill_feel_range"
            || levelId == "horde_range" || levelId == "ranged_range"
            || levelId == "feedback_range";

        // Gates the baseline-floor grant: fresh save or post-KIA-wipe (empty) → floor;
        // re-geared from stash / carrying loot (non-empty) → left alone.
        public static bool IsInventoryEmpty(InventoryState inv)
        {
            for (int i = 0; i < InventoryState.WeaponSlotCount; i++)
                if (inv.WeaponSlots[i] != null) return false;
            if (inv.HelmetSlot != null || inv.BodyArmorSlot != null) return false;
            for (int i = 0; i < InventoryState.BackpackSize; i++)
                if (inv.Backpack[i] != null) return false;
            return true;
        }

        // Baseline floor — the always-available minimum. Granted whenever the player has
        // nothing (fresh save, or after a KIA gear-wipe with an empty stash). Deliberately
        // weak so death still costs the player's GOOD gear: one Common pistol + a bandage +
        // a medkit, NO armor. Loads exactly one full magazine (the pistol's real capacity —
        // never overfill) and keeps the rest as spare, so the total round count is fixed at
        // TotalRounds regardless of the mag size. Stash is never touched.
        // See docs/ai/release-scope.md (baseline loadout).
        static void GiveBaselineFloor(RaidState state, InventoryState inventory)
        {
            const int totalRounds = 36; // loaded + spare, kept constant

            var registry = App.Instance.CoreDefinitions;
            var payloadDef = registry?.GetPayload("BallisticRound");
            var deliveryDef = registry?.GetDelivery("SingleAction");

            int magSize = payloadDef != null && deliveryDef != null
                ? WeaponStatComposer.Compose(payloadDef, RarityTier.Common, deliveryDef, RarityTier.Common).MagazineSize
                : 6;
            magSize = Mathf.Clamp(magSize, 1, totalRounds);

            var config = new WeaponConfiguration(
                payload:        new PayloadCoreInstance("BallisticRound", RarityTier.Common),
                delivery:       new DeliveryCoreInstance("SingleAction", RarityTier.Common),
                exotic:         null,
                ammoInMagazine: magSize);
            inventory.WeaponSlots[0] = ItemState.CreateWeapon(state.AllocateEId(), "Weapon", config);

            // Remaining rounds as spare (total stays at totalRounds).
            string ammoId = payloadDef?.AmmoType ?? "Ammo_Rifle";
            inventory.Backpack[0] = ItemState.Create(state.AllocateEId(), ammoId, totalRounds - magSize);
            inventory.Backpack[1] = ItemState.Create(state.AllocateEId(), "Bandage");
            inventory.Backpack[2] = ItemState.Create(state.AllocateEId(), "Medkit");
        }

        // CHEAT loadout — all 6 weapon variants (2 payloads × 3 deliveries) + armor +
        // consumables, for testing weapon/combat permutations on the shooting ranges.
        // NOT used in real play (see GiveBaselineFloor).
        static void GiveTestRangeLoadout(RaidState state, InventoryState inventory)
        {
            var combos = new (string payload, string delivery, int magSize)[]
            {
                ("BallisticRound", "Auto",         30),
                ("BallisticRound", "SingleAction", 12),
                ("BallisticRound", "Scatter",      5),
                ("LaserCharge",    "Auto",         30),
                ("LaserCharge",    "SingleAction", 12),
                ("LaserCharge",    "Scatter",      5),
            };

            inventory.WeaponSlots[0] = MakeWeapon(state, combos[0]);
            inventory.WeaponSlots[1] = MakeWeapon(state, combos[1]);
            for (int i = 2; i < combos.Length; i++)
                inventory.Backpack[i - 2] = MakeWeapon(state, combos[i]);

            inventory.HelmetSlot = ItemState.Create(state.AllocateEId(), "Helmet_Basic");
            inventory.BodyArmorSlot = ItemState.Create(state.AllocateEId(), "Armor_Basic");

            inventory.Backpack[5] = ItemState.Create(state.AllocateEId(), "Grenade");
            inventory.Backpack[6] = ItemState.Create(state.AllocateEId(), "Medkit");
            inventory.Backpack[7] = ItemState.Create(state.AllocateEId(), "Bandage");
        }

        static ItemState MakeWeapon(RaidState state, (string payload, string delivery, int magSize) combo)
        {
            // Random rarity per core so the dual-rarity inventory frame + tooltip colors are
            // immediately testable. Stats fall back to Common until per-tier values exist
            // (Tier 4b) — rarity is visual-only.
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
