using Constants;
using NUnit.Framework;
using State;
using Systems;

namespace Tests.EditMode
{
    /// <summary>
    /// The "Value" line tooltips show comes from <see cref="ShopSystem.GetGlobalSellPrice"/>, which
    /// used to be built purely from vendor stock lists — 8 ids against the 93 that ItemBalance
    /// prices. Mods, modules, materials and quest items therefore showed no worth at all. These
    /// tests read the shipped balance table, so they also fail if an item's price is removed.
    ///
    /// Display-only path: real transactions go through GetBuyPrice / GetSellPrice with the shop's
    /// own catalog, which these tests deliberately do not touch.
    /// </summary>
    [TestFixture]
    public class GlobalItemValueTests
    {
        [Test]
        public void GlobalValue_ItemNoVendorStocks_FallsBackToTheBalanceTable()
        {
            // Basic_Scope is priced in ItemBalance and stocked by nobody.
            Assert.Greater(ItemBalanceAsset.PriceOf("Basic_Scope"), 0,
                "Fixture assumption: the balance table prices this item.");

            var scope = ItemState.Create(new EId(1), "Basic_Scope");

            Assert.Greater(ShopSystem.GetGlobalSellPrice(scope), 0,
                "An item with an authored price must show a Value even if no vendor stocks it.");
        }

        [Test]
        public void GlobalValue_UnpricedItem_StaysZero()
        {
            // No price anywhere → no Value row (AppendPrice drops it), rather than a made-up 1¢.
            var ghost = ItemState.Create(new EId(2), "NotARealItemId");

            Assert.AreEqual(0, ShopSystem.GetGlobalSellPrice(ghost));
        }

        [Test]
        public void GlobalValue_ScalesWithStackCount()
        {
            var single = ItemState.Create(new EId(3), "Ammo_Rifle", 1);
            var stack  = ItemState.Create(new EId(4), "Ammo_Rifle", 10);

            int one = ShopSystem.GetGlobalSellPrice(single);
            Assert.Greater(one, 0);
            Assert.AreEqual(one * 10, ShopSystem.GetGlobalSellPrice(stack));
        }
    }
}
