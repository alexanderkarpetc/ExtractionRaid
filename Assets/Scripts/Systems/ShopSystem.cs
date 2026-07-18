using System.Collections.Generic;
using Constants;
using Session;
using State;
using UnityEngine;

namespace Systems
{
    /// <summary>
    /// Vendor / shop transactions. A shop is just a <see cref="LootableContainerState"/>
    /// with <c>IsShop=true</c> — the inventory window's existing sub-panel logic picks
    /// it up automatically when it lands near the player, and the cross-source drag
    /// pipeline routes through <see cref="TryBuy"/>/<see cref="TrySell"/> in place of
    /// the free-loot <see cref="LootSystem.TryTransfer"/>. Prices live in the shop's
    /// <see cref="LootableContainerState.ShopCatalog"/> dictionary so sell-back is
    /// still resolvable after stock depletes.
    /// </summary>
    public static class ShopSystem
    {
        /// <summary>
        /// Spawns the NPC's stock as a shop-flagged lootable near them. Idempotent
        /// per-NPC: if a shop with the same OwnerNpcId is already open, the existing
        /// one is returned (we don't want trade choices to reset stock the player
        /// already touched in the same dialogue session).
        /// </summary>
        public static LootableContainerState OpenShopFor(RaidState state, NpcState npc,
            ShopDefinitionAsset def)
        {
            if (state == null || npc == null || def == null) return null;

            var existing = FindShopByOwner(state, npc.NpcId);
            if (existing != null) return existing;

            var inv = new InventoryState();
            var catalog = new Dictionary<string, int>(def.Stock?.Length ?? 0);

            int slot = 0;
            if (def.Stock != null)
            {
                foreach (var entry in def.Stock)
                {
                    if (string.IsNullOrEmpty(entry.ItemDefId) || entry.Quantity <= 0) continue;
                    if (slot >= InventoryState.BackpackSize) break;

                    var itemDef = ItemDefinition.Get(entry.ItemDefId);
                    if (itemDef == null)
                    {
                        Debug.LogWarning($"[Shop] '{def.OwnerNpcId}' stock references unknown item '{entry.ItemDefId}'.");
                        continue;
                    }

                    catalog[entry.ItemDefId] = entry.BuyPrice;

                    // Stacks honor MaxStackSize: a stock entry of 50 ammo with cap=30
                    // spreads into two backpack slots (30 + 20). Weapons never stack,
                    // so weapon entries occupy one slot per unit.
                    int remaining = entry.Quantity;
                    int maxStack = Mathf.Max(1, itemDef.MaxStackSize);
                    while (remaining > 0 && slot < InventoryState.BackpackSize)
                    {
                        int count = Mathf.Min(remaining, maxStack);
                        var itemId = state.AllocateEId();
                        inv.Backpack[slot++] = ItemState.Create(itemId, entry.ItemDefId, count);
                        remaining -= count;
                    }
                }
            }

            var id = state.AllocateEId();
            var shop = LootableContainerState.CreateShop(id, npc.Position, npc.NpcId, inv, catalog,
                def.SellRatio, Mathf.Max(0, def.DefaultSellPrice));
            state.Lootables.Add(shop);
            return shop;
        }

        public static void CloseShopFor(RaidState state, string ownerNpcId)
        {
            if (state == null || string.IsNullOrEmpty(ownerNpcId)) return;
            for (int i = state.Lootables.Count - 1; i >= 0; i--)
            {
                var l = state.Lootables[i];
                if (l.IsShop && l.OwnerNpcId == ownerNpcId)
                    state.Lootables.RemoveAt(i);
            }
        }

        public static LootableContainerState FindShopByOwner(RaidState state, string ownerNpcId)
        {
            if (state == null || string.IsNullOrEmpty(ownerNpcId)) return null;
            for (int i = 0; i < state.Lootables.Count; i++)
            {
                var l = state.Lootables[i];
                if (l.IsShop && l.OwnerNpcId == ownerNpcId) return l;
            }
            return null;
        }

        public static int GetBuyPrice(LootableContainerState shop, ItemState item)
        {
            if (shop == null || item == null) return 0;
            int unit = (shop.ShopCatalog != null
                        && shop.ShopCatalog.TryGetValue(item.DefinitionId, out var p))
                       ? p
                       : shop.DefaultSellPrice;
            return unit * Mathf.Max(1, item.StackCount);
        }

        public static int GetSellPrice(LootableContainerState shop, ItemState item)
        {
            if (shop == null || item == null) return 0;
            int unit;
            if (shop.ShopCatalog != null && shop.ShopCatalog.TryGetValue(item.DefinitionId, out var p))
                unit = Mathf.Max(1, Mathf.RoundToInt(p * shop.SellRatio));
            else
                unit = Mathf.Max(0, shop.DefaultSellPrice);
            return unit * Mathf.Max(1, item.StackCount);
        }

        // Catalog-wide fallback: aggregates every ShopDefinitionAsset under
        // Resources/Configs/Shops so tooltips can show a sell value even when no
        // shop instance is live. First buy price encountered for each item id
        // wins; sell value derived via the asset's SellRatio.
        static bool s_globalLoaded;
        static readonly Dictionary<string, int> s_globalSellPrices = new();

        public static int GetGlobalSellPrice(ItemState item)
        {
            if (item == null) return 0;
            EnsureGlobalCatalog();
            if (!s_globalSellPrices.TryGetValue(item.DefinitionId, out var unit))
                return 0;
            return unit * Mathf.Max(1, item.StackCount);
        }

        static void EnsureGlobalCatalog()
        {
            if (s_globalLoaded) return;
            s_globalLoaded = true;
            var assets = Resources.LoadAll<ShopDefinitionAsset>("Configs/Shops");
            foreach (var def in assets)
            {
                if (def?.Stock == null) continue;
                float ratio = Mathf.Max(0f, def.SellRatio);
                foreach (var entry in def.Stock)
                {
                    if (string.IsNullOrEmpty(entry.ItemDefId)) continue;
                    if (s_globalSellPrices.ContainsKey(entry.ItemDefId)) continue;
                    s_globalSellPrices[entry.ItemDefId] = Mathf.Max(1, Mathf.RoundToInt(entry.BuyPrice * ratio));
                }
            }
        }

        /// <summary>
        /// Player picks up an item from the shop. Target slot must be empty — swaps
        /// would double as a sell in the same operation, which complicates UX and
        /// rollback. Caller (UI) refuses the drag visually for non-empty targets.
        /// </summary>
        public static bool TryBuy(Player player, LootableContainerState shop,
            InventorySlotRef shopSlot, InventorySlotRef playerSlot)
        {
            if (player == null || shop == null || !shop.IsShop) return false;
            var inv = player.Inventory;
            if (inv == null) return false;

            var item = shop.Inventory.GetSlot(shopSlot);
            if (item == null) return false;
            if (inv.GetSlot(playerSlot) != null) return false;

            int price = GetBuyPrice(shop, item);
            if (!player.TryDebit(price)) return false;

            if (!LootSystem.TryTransfer(shop.Inventory, shopSlot, inv, playerSlot))
            {
                player.Credit(price); // rollback — slot-type mismatch etc.
                return false;
            }
            return true;
        }

        /// <summary>
        /// Player sells an item to the shop. The item is removed from the player's
        /// inventory and the credit value paid out — the shop's own inventory isn't
        /// touched (sold goods evaporate). Lets us ignore "shop full" failure paths
        /// entirely while keeping the per-shop sell pricing.
        /// </summary>
        public static bool TrySell(Player player, LootableContainerState shop,
            InventorySlotRef playerSlot)
        {
            if (player == null || shop == null || !shop.IsShop) return false;
            var inv = player.Inventory;
            if (inv == null) return false;

            var item = inv.GetSlot(playerSlot);
            if (item == null) return false;

            int price = GetSellPrice(shop, item);

            inv.SetSlot(playerSlot, null);
            player.Credit(price);
            if (ApplicationCore.App.IsInitialized)
                QuestSystem.OnItemSold(player.QuestProgress,
                    ApplicationCore.App.Instance.QuestDatabase, price);
            return true;
        }
    }
}
