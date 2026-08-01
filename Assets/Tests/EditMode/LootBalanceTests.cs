using System.Collections.Generic;
using Constants;
using NUnit.Framework;
using State;
using Systems;

namespace Tests.EditMode
{
    /// <summary>
    /// ItemBalance is the single source of truth for loot: it decides WHICH item comes out of a
    /// category bucket and HOW MANY units one drop is worth. Loot configs only shape the mix.
    /// </summary>
    [TestFixture]
    public class LootBalanceTests
    {
        [Test]
        public void DropWeight_ExplicitZero_TakesItemOutOfRolls()
        {
            // A row weighted 0 is authoritative — that's how an item is retired from loot
            // without deleting it from the game (still buyable / craftable / questable).
            Assert.AreEqual(0f, ItemBalanceAsset.DropWeightOf("Worn_Warehouse_Key"),
                "An explicit DropWeight of 0 must not fall back to the derived default.");
        }

        [Test]
        public void DropWeight_UnknownItem_FallsBackToDerivedDefault()
        {
            // Items the table hasn't been synced with still roll at a sensible rate rather
            // than silently vanishing from loot.
            Assert.Greater(ItemBalanceAsset.DropWeightOf("NotARealItemId"), 0f);
        }

        [Test]
        public void DropCountRange_AuthoredRow_WinsOverDerivedDefault()
        {
            ItemBalanceAsset.DropCountRangeOf("Bandage", out int min, out int max);
            Assert.GreaterOrEqual(min, 1);
            Assert.GreaterOrEqual(max, min);

            // Bandages are authored as a multi-unit pack in the balance table; the stack-size
            // derived default for a 3-stack item would be 1..1, so this proves the row wins.
            Assert.Greater(max, 1, "Bandage should drop as a pack, per ItemBalance.");
        }

        [Test]
        public void DropCountRange_UnauthoredItem_DerivesFromStackSize()
        {
            ItemBalanceAsset.DefaultDropCountRange("Ammo_Rifle", out int min, out int max);
            var def = ItemDefinition.Get("Ammo_Rifle");
            Assert.AreEqual(1, min);
            Assert.LessOrEqual(max, def.MaxStackSize);
            Assert.GreaterOrEqual(max, 1);
        }

        [Test]
        public void DropCountRange_NonStackable_IsSingle()
        {
            ItemBalanceAsset.DropCountRangeOf("Medkit", out int min, out int max);
            Assert.AreEqual(1, min);
            Assert.AreEqual(1, max);
        }

        [Test]
        public void RollCount_StaysInsideBalanceRange()
        {
            UnityEngine.Random.InitState(7);
            ItemBalanceAsset.DropCountRangeOf("Ammo_Rifle", out int min, out int max);

            for (int i = 0; i < 100; i++)
            {
                int c = LootRoller.RollCount("Ammo_Rifle");
                Assert.GreaterOrEqual(c, min);
                Assert.LessOrEqual(c, max);
            }
        }

        [Test]
        public void PickFromCategory_OnlyReturnsDroppableItemsOfThatCategory()
        {
            UnityEngine.Random.InitState(31337);

            foreach (var bucket in new[] { LootCategory.Materials, LootCategory.Meds,
                                           LootCategory.Ammo, LootCategory.Gear })
            {
                var expected = LootConstants.ToItemCategory(bucket);
                for (int i = 0; i < 40; i++)
                {
                    var id = LootRoller.PickFromCategory(bucket);
                    Assert.IsNotNull(id, $"{bucket} produced nothing.");
                    var def = ItemDefinition.Get(id);
                    Assert.IsNotNull(def, id);
                    Assert.AreEqual(expected, def.Category, id);
                    Assert.Greater(ItemBalanceAsset.DropWeightOf(id), 0f,
                        $"'{id}' is weighted 0 and must never be rolled.");
                }
            }
        }

        [Test]
        public void PickFromCategory_SkipsExcludedIds()
        {
            UnityEngine.Random.InitState(555);

            var taken = new List<string>();
            var candidates = LootConstants.CandidatesFor(LootCategory.Meds);
            for (int i = 0; i < candidates.Count; i++)
            {
                var id = LootRoller.PickFromCategory(LootCategory.Meds, taken);
                if (id == null) break;                       // bucket exhausted (all remaining weighted 0)
                Assert.IsFalse(taken.Contains(id), $"'{id}' was picked twice despite exclusion.");
                taken.Add(id);
            }
            Assert.Greater(taken.Count, 0);
        }

        [Test]
        public void TryRollPool_EmptyOrZeroWeightPool_Fails()
        {
            Assert.IsFalse(LootRoller.TryRollPool(null, out _));
            Assert.IsFalse(LootRoller.TryRollPool(System.Array.Empty<LootPoolEntry>(), out _));
            Assert.IsFalse(LootRoller.TryRollPool(
                new[] { LootPoolEntry.FromCategory(LootCategory.Meds, 0f) }, out _));
        }

        [Test]
        public void TryRollPool_WeightsShapeTheMix_NotTheItemRarity()
        {
            UnityEngine.Random.InitState(2024);

            // 9:1 in favour of ammo — the pool weight decides the MIX. Which ammo / which med
            // is still ItemBalance's call, which this test deliberately doesn't assert on.
            var pool = new[]
            {
                LootPoolEntry.FromCategory(LootCategory.Ammo, 9f),
                LootPoolEntry.FromCategory(LootCategory.Meds, 1f),
            };

            int ammo = 0, meds = 0;
            for (int i = 0; i < 400; i++)
            {
                Assert.IsTrue(LootRoller.TryRollPool(pool, out var id));
                var cat = ItemDefinition.Get(id).Category;
                if (cat == ItemCategory.Ammo) ammo++;
                else if (cat == ItemCategory.Meds) meds++;
            }

            Assert.AreEqual(400, ammo + meds, "Pool produced an item outside both buckets.");
            Assert.Greater(ammo, meds * 2, "A 9:1 pool should be dominated by ammo.");
        }

        [Test]
        public void EveryLootBucket_ResolvesToAtLeastOneItem()
        {
            foreach (LootCategory bucket in System.Enum.GetValues(typeof(LootCategory)))
                Assert.Greater(LootConstants.CandidatesFor(bucket).Count, 0,
                    $"Loot bucket '{bucket}' resolves to no items — a config using it drops nothing.");
        }
    }
}
