using System.Collections.Generic;
using Progression;
using Session;
using State;
using UnityEngine;

namespace Systems
{
    /// <summary>
    /// The price of a progression node: its materials, and nothing else — there is no skill-point
    /// pool, so owning the items IS the gate. Connectivity stays in
    /// <see cref="ProgressionSystem"/> (state-only); this composes both and owns the item side,
    /// because counting/consuming needs <see cref="Player"/>.
    ///
    /// Supply is <b>Stash + Backpack</b> and is consumed stash-first, mirroring
    /// <see cref="BuildingSystem"/> so the player's raid loadout survives an unlock when the
    /// stash can cover it. Equipped weapons/armour are never counted or taken.
    /// </summary>
    public static class ProgressionCostSystem
    {
        /// <summary>How many of this cost line the player owns (stash + backpack).</summary>
        public static int Owned(Player player, ProgressionCostEntry entry)
        {
            if (player == null || entry == null) return 0;
            return entry.IsWeapon
                ? CountWeaponsInStash(player.Stash, entry) + CountWeaponsInBackpack(player.Inventory, entry)
                : BuildingSystem.GetAvailable(player, entry.ItemId);
        }

        /// <summary>Shortfall on this line — 0 when it's covered.</summary>
        public static int Missing(Player player, ProgressionCostEntry entry) =>
            entry == null ? 0 : Mathf.Max(0, entry.Quantity - Owned(player, entry));

        /// <summary>True when every line of the node's cost is covered. A node with no cost is free.</summary>
        public static bool CanPay(Player player, ProgressionNodeDef node)
        {
            if (node?.Cost == null || node.Cost.Count == 0) return true;
            if (player == null) return false;
            foreach (var entry in node.Cost)
                if (Owned(player, entry) < entry.Quantity) return false;
            return true;
        }

        /// <summary>Number of cost lines the player can't cover — drives the tooltip's status line.</summary>
        public static int MissingLineCount(Player player, ProgressionNodeDef node)
        {
            if (node?.Cost == null) return 0;
            int n = 0;
            foreach (var entry in node.Cost)
                if (Owned(player, entry) < entry.Quantity) n++;
            return n;
        }

        /// <summary>Connected to something allocated + every cost line covered.</summary>
        public static bool CanUnlock(ProgressionTreeConfig cfg, Player player, string nodeId)
        {
            if (cfg == null || player == null) return false;
            if (!ProgressionSystem.CanAllocate(cfg, player.Progression, nodeId)) return false;
            if (!cfg.TryFind(nodeId, out _, out _, out var node)) return false;
            return player.Progression.DevUnlockPoints > 0 || CanPay(player, node);
        }

        /// <summary>
        /// Charge the items, then allocate. All-or-nothing: nothing is consumed unless every
        /// line is covered. Permanent — there is no refund path.
        /// </summary>
        public static bool TryUnlock(ProgressionTreeConfig cfg, Player player, string nodeId)
        {
            if (!CanUnlock(cfg, player, nodeId)) return false;
            cfg.TryFind(nodeId, out _, out _, out var node);

            bool useDevPoint = player.Progression.DevUnlockPoints > 0;

            if (!useDevPoint && node.Cost != null)
                foreach (var entry in node.Cost)
                    Consume(player, entry);

            if (!ProgressionSystem.Allocate(cfg, player.Progression, nodeId))
            {
                Debug.LogError($"[Progression] Consumed the cost of '{nodeId}' but allocation failed.");
                return false;
            }

            if (useDevPoint)
                player.Progression.DevUnlockPoints--;
            return true;
        }

        // ── consuming ──────────────────────────────────────────────
        static void Consume(Player player, ProgressionCostEntry entry)
        {
            if (entry.IsWeapon) { ConsumeWeapons(player, entry); return; }
            BuildingSystem.ConsumeMaterial(player, entry.ItemId, entry.Quantity);
        }

        // Weapons are non-stackable ItemStates — drop whole entries, cheapest rarity first so
        // the player keeps their best gun when several match.
        static void ConsumeWeapons(Player player, ProgressionCostEntry entry)
        {
            int remaining = entry.Quantity;
            var stash = player.Stash;
            if (stash != null)
                while (remaining > 0)
                {
                    int idx = FindCheapestMatch(stash, entry);
                    if (idx < 0) break;
                    stash.RemoveAt(idx);
                    remaining--;
                }

            var inv = player.Inventory;
            if (inv == null) return;
            for (int i = 0; i < InventoryState.BackpackSize && remaining > 0; i++)
                if (Matches(inv.Backpack[i], entry))
                {
                    inv.Backpack[i] = null;
                    remaining--;
                }
        }

        static int FindCheapestMatch(List<ItemState> stash, ProgressionCostEntry entry)
        {
            int best = -1, bestRarity = int.MaxValue;
            for (int i = 0; i < stash.Count; i++)
            {
                if (!Matches(stash[i], entry)) continue;
                int rarity = Tier(stash[i]);
                if (rarity < bestRarity) { bestRarity = rarity; best = i; }
            }
            return best;
        }

        // ── counting ───────────────────────────────────────────────
        static int CountWeaponsInStash(List<ItemState> stash, ProgressionCostEntry entry)
        {
            if (stash == null) return 0;
            int count = 0;
            for (int i = 0; i < stash.Count; i++)
                if (Matches(stash[i], entry)) count++;
            return count;
        }

        static int CountWeaponsInBackpack(InventoryState inv, ProgressionCostEntry entry)
        {
            if (inv == null) return 0;
            int count = 0;
            for (int i = 0; i < InventoryState.BackpackSize; i++)
                if (Matches(inv.Backpack[i], entry)) count++;
            return count;
        }

        /// <summary>
        /// An assembled weapon satisfies the line when both cores match the requested
        /// Delivery/Payload combination and <i>both</i> are at MinRarity or better — a Rare
        /// barrel on a Common round is not a Rare weapon.
        /// </summary>
        static bool Matches(ItemState item, ProgressionCostEntry entry)
        {
            if (item == null || !item.HasWeaponConfiguration) return false;
            var cfg = item.WeaponConfiguration;
            if (!string.IsNullOrEmpty(entry.DeliveryId) && cfg.Delivery.DefinitionId != entry.DeliveryId) return false;
            if (!string.IsNullOrEmpty(entry.PayloadId) && cfg.Payload.DefinitionId != entry.PayloadId) return false;
            return cfg.Delivery.Rarity >= entry.MinRarity && cfg.Payload.Rarity >= entry.MinRarity;
        }

        // Lowest of the two core rarities — how "cheap" this weapon is to give up.
        static int Tier(ItemState item)
        {
            var cfg = item.WeaponConfiguration;
            return Mathf.Min((int)cfg.Delivery.Rarity, (int)cfg.Payload.Rarity);
        }
    }
}
