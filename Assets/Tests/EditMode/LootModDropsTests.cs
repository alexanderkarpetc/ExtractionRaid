using System.Linq;
using Constants;
using NUnit.Framework;
using State;

namespace Tests.EditMode
{
    /// <summary>
    /// Attachment mods drop from loot: the shared mod pool resolves to real WeaponMod items,
    /// unique (archetype-restricted) mods are rarer, and the mixed containers include mods.
    /// </summary>
    [TestFixture]
    public class LootModDropsTests
    {
        [Test]
        public void AttachmentModDrops_AllResolveToWeaponModItems()
        {
            foreach (var d in ContainerConstants.AttachmentModDrops())
            {
                var def = ItemDefinition.Get(d.DefinitionId);
                Assert.IsNotNull(def, d.DefinitionId);
                Assert.AreEqual(ItemCategory.WeaponMod, def.Category, d.DefinitionId);
            }
        }

        [Test]
        public void AttachmentModDrops_UniqueAreRarerThanUniversal()
        {
            var pool = ContainerConstants.AttachmentModDrops();
            float universal = pool.First(d => d.DefinitionId == "RedDot").Weight;
            float unique    = pool.First(d => d.DefinitionId == "LaserFocusing").Weight;
            Assert.Greater(universal, unique);
        }

        [Test]
        public void AttachmentModDrops_ScaleScalesWeights()
        {
            var full = ContainerConstants.AttachmentModDrops(1f);
            var half = ContainerConstants.AttachmentModDrops(0.5f);
            for (int i = 0; i < full.Length; i++)
                Assert.AreEqual(full[i].Weight * 0.5f, half[i].Weight, 1e-4);
        }

        [Test]
        public void MixedContainers_IncludeModsAndKeepCores()
        {
            Assert.IsTrue(ContainerConstants.TryGetConfig("RandomLootBox", out var rb));
            Assert.IsTrue(rb.PossibleDrops.Any(d => d.DefinitionId == "RedDot"), "RandomLootBox has mods");

            Assert.IsTrue(ContainerConstants.TryGetConfig("ModuleCache", out var mc));
            Assert.IsTrue(mc.PossibleDrops.Any(d => d.DefinitionId == "ExtendedMag"), "ModuleCache has mods");
            Assert.IsTrue(mc.PossibleDrops.Any(d => d.DefinitionId == "BallisticRound"), "ModuleCache keeps cores");
        }
    }
}
