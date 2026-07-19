using System;
using UnityEngine;

namespace Constants
{
    /// <summary>
    /// Designer-authored shop catalog. One asset per NPC vendor (keyed by
    /// <see cref="OwnerNpcId"/>, which must match <c>NpcState.NpcId</c>). Stock is
    /// instantiated into a per-raid <see cref="State.LootableContainerState"/> by
    /// <c>ShopSystem.OpenShopFor</c> when the player picks the "Trade" dialogue
    /// option, and despawned when they exit the dialogue.
    /// </summary>
    [CreateAssetMenu(fileName = "Shop", menuName = "Shops/Shop Definition")]
    public class ShopDefinitionAsset : ScriptableObject
    {
        [Serializable]
        public class StockEntry
        {
            [Tooltip("ItemDefinition id (matches ItemDefinition.Get(id)).")]
            public string ItemDefId;

            [Tooltip("How many units the shop offers per raid. Stacks honor MaxStackSize.")]
            public int Quantity = 1;

            // Buy price is no longer authored per shop — it comes from the global
            // ItemBalanceAsset (Resources/Configs/ItemBalance). This asset only decides
            // WHAT and HOW MANY a vendor stocks; the balance table decides the price.
        }

        [Header("Identity")]
        [Tooltip("Must match NpcState.NpcId of the vendor that owns this shop.")]
        public string OwnerNpcId;

        [Tooltip("If set, the Trade option stays hidden until this quest is Completed. Leave empty for an always-open shop.")]
        public string RequiredQuestId;

        [Header("Stock")]
        public StockEntry[] Stock;

        [Header("Sell-back rules")]
        [Tooltip("Multiplier applied to BuyPrice when the player sells back an item the shop also stocks.")]
        [Range(0f, 1f)]
        public float SellRatio = 0.5f;

        [Tooltip("Fallback sell price (per unit) for items NOT in this shop's stock catalog.")]
        public int DefaultSellPrice = 1;
    }
}
