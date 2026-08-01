using Constants;
using NUnit.Framework;
using State;
using Systems;

namespace Tests.EditMode
{
    /// <summary>
    /// The attachment / core buckets resolve to real WeaponMod items, ItemBalance (not a
    /// hardcoded weight table) decides their rarity, and the mixed containers can roll both.
    /// </summary>
    [TestFixture]
    public class LootModDropsTests
    {
        [Test]
        public void AttachmentBucket_AllResolveToWeaponModItems()
        {
            var candidates = LootConstants.CandidatesFor(LootCategory.Attachments);
            Assert.AreEqual(LootConstants.AttachmentIds.Length, candidates.Count,
                "Every curated attachment id must resolve to a registry item.");
            foreach (var def in candidates)
                Assert.AreEqual(ItemCategory.WeaponMod, def.Category, def.Id);
        }

        [Test]
        public void WeaponCoreBucket_AllResolveToWeaponModItems()
        {
            var candidates = LootConstants.CandidatesFor(LootCategory.WeaponCores);
            Assert.AreEqual(LootConstants.WeaponCoreIds.Length, candidates.Count,
                "Every curated core id must resolve to a registry item.");
            foreach (var def in candidates)
                Assert.AreEqual(ItemCategory.WeaponMod, def.Category, def.Id);
        }

        [Test]
        public void AttachmentRarity_ComesFromItemBalance()
        {
            // Cheap handling mods are common; the archetype-locked / premium optics are scarce.
            // Both numbers live in ItemBalance — nothing here restates them.
            float universal = ItemBalanceAsset.DropWeightOf("RedDot");
            float unique    = ItemBalanceAsset.DropWeightOf("LaserFocusing");
            Assert.Greater(universal, unique,
                "ItemBalance should keep the archetype-locked mod rarer than the red dot.");
        }

        [Test]
        public void MixedContainers_CanRollModsAndCores()
        {
            Assert.IsTrue(ContainerConstants.TryGetConfig("RandomLootBox", out var rb));
            Assert.IsTrue(HasBucket(rb, LootCategory.Attachments), "RandomLootBox rolls attachments");
            Assert.IsTrue(HasBucket(rb, LootCategory.WeaponCores), "RandomLootBox rolls cores");

            Assert.IsTrue(ContainerConstants.TryGetConfig("ModuleCache", out var mc));
            Assert.IsTrue(HasBucket(mc, LootCategory.Attachments), "ModuleCache rolls attachments");
            Assert.IsTrue(HasBucket(mc, LootCategory.WeaponCores), "ModuleCache keeps cores");
        }

        [Test]
        public void PickWeighted_OverAttachmentSet_ReturnsRealDroppableItems()
        {
            UnityEngine.Random.InitState(4242);
            for (int i = 0; i < 50; i++)
            {
                var id = LootRoller.PickWeighted(LootConstants.AttachmentIds);
                Assert.IsNotNull(id);
                Assert.IsNotNull(ItemDefinition.Get(id), id);
                Assert.Greater(ItemBalanceAsset.DropWeightOf(id), 0f,
                    $"'{id}' has drop weight 0 and must never be picked.");
            }
        }

        static bool HasBucket(in ContainerTypeConfig cfg, LootCategory category)
        {
            if (cfg.RandomPool == null) return false;
            foreach (var entry in cfg.RandomPool)
                if (entry.IsCategory && entry.Category == category) return true;
            return false;
        }
    }
}
