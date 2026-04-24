using NUnit.Framework;
using State;

namespace Tests.EditMode
{
    /// <summary>
    /// Tests for <see cref="ArmorState"/> factory + derived properties. Sibling fixtures:
    /// <see cref="ProjectileEntityStateTests"/> for projectile-level penetration carry,
    /// <see cref="ItemDefinitionRegistryTests"/> for the core-items registry shape.
    /// </summary>
    [TestFixture]
    public class ArmorStateTests
    {
        // ── ArmorState.Create ─────────────────────────────────

        [Test]
        public void Create_SetsFieldsCorrectly()
        {
            var armor = ArmorState.Create(65f, 200f);
            Assert.AreEqual(65f, armor.ArmorPoints, 0.001f);
            Assert.AreEqual(200f, armor.MaxDurability, 0.001f);
        }

        [Test]
        public void Create_DurabilityStartsFull()
        {
            var armor = ArmorState.Create(50f, 150f);
            Assert.AreEqual(150f, armor.CurrentDurability, 0.001f);
            Assert.AreEqual(armor.MaxDurability, armor.CurrentDurability, 0.001f);
        }

        // ── IsBroken ──────────────────────────────────────────

        [Test]
        public void IsBroken_PositiveDurability_ReturnsFalse()
        {
            var armor = ArmorState.Create(30f, 100f);
            Assert.IsFalse(armor.IsBroken);
        }

        [Test]
        public void IsBroken_ZeroDurability_ReturnsTrue()
        {
            var armor = ArmorState.Create(30f, 100f);
            armor.CurrentDurability = 0f;
            Assert.IsTrue(armor.IsBroken);
        }

        [Test]
        public void IsBroken_NegativeDurability_ReturnsTrue()
        {
            var armor = ArmorState.Create(30f, 100f);
            armor.CurrentDurability = -5f;
            Assert.IsTrue(armor.IsBroken);
        }

        // ── DurabilityPercent ─────────────────────────────────

        [Test]
        public void DurabilityPercent_Full_ReturnsOne()
        {
            var armor = ArmorState.Create(50f, 100f);
            Assert.AreEqual(1f, armor.DurabilityPercent, 0.001f);
        }

        [Test]
        public void DurabilityPercent_Half_ReturnsHalf()
        {
            var armor = ArmorState.Create(50f, 100f);
            armor.CurrentDurability = 50f;
            Assert.AreEqual(0.5f, armor.DurabilityPercent, 0.001f);
        }

        [Test]
        public void DurabilityPercent_ZeroMax_ReturnsZero()
        {
            var armor = ArmorState.Create(50f, 0f);
            Assert.AreEqual(0f, armor.DurabilityPercent, 0.001f);
        }

        // ── RaidState.ArmorMap (lives here because it's an armor-related field) ──

        [Test]
        public void RaidState_Create_HasArmorMap()
        {
            var state = RaidState.Create(EditModeTestsUtils.NewAllocator());
            Assert.IsNotNull(state.ArmorMap);
            Assert.AreEqual(0, state.ArmorMap.Count);
        }
    }
}
