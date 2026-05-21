using System.Collections.Generic;
using UnityEngine;

namespace State
{
    public class LootableContainerState
    {
        public EId Id;
        public Vector3 Position;
        public string TypeId;
        public InventoryState Inventory;
        public bool IsContainer;
        public bool IsOpened;

        // ── Shop overlay ────────────────────────────────────────────
        // When IsShop is true this lootable represents an NPC's stock. Items inside
        // are not free loot; ShopSystem.TryBuy/TrySell intercepts transfers and
        // charges/credits the player via Player.TryDebit/Credit.
        //
        // ShopCatalog maps ItemDefinition.Id → buy price per unit. It's stable for
        // the whole trade session: when stock depletes ShopSystem still resolves
        // sell-back prices by looking up the original buy price × SellRatio. Items
        // not in the catalog use DefaultSellPrice.
        public bool IsShop;
        public string OwnerNpcId;
        public Dictionary<string, int> ShopCatalog;
        public float SellRatio = 0.5f;
        public int DefaultSellPrice = 1;

        public static LootableContainerState Create(EId id, Vector3 position, string typeId,
            InventoryState inventory, bool isContainer = false)
        {
            return new LootableContainerState
            {
                Id = id,
                Position = position,
                TypeId = typeId,
                Inventory = inventory,
                IsContainer = isContainer,
            };
        }

        public static LootableContainerState CreateShop(EId id, Vector3 position, string ownerNpcId,
            InventoryState inventory, Dictionary<string, int> shopCatalog,
            float sellRatio, int defaultSellPrice)
        {
            return new LootableContainerState
            {
                Id = id,
                Position = position,
                TypeId = ownerNpcId, // sub-panel title fallback when not overridden
                Inventory = inventory,
                IsContainer = true,
                IsShop = true,
                OwnerNpcId = ownerNpcId,
                ShopCatalog = shopCatalog,
                SellRatio = sellRatio,
                DefaultSellPrice = defaultSellPrice,
            };
        }
    }
}
