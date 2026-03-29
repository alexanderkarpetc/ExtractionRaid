using Constants;
using NUnit.Framework;
using State;
using Systems;

namespace Tests.EditMode
{
    [TestFixture]
    public class ArmorSystemTests
    {
        // ── EffectiveDurabilityMultiplier ──────────────────────

        [Test]
        public void EffectiveDurabilityMultiplier_FullDur_ReturnsOne()
        {
            Assert.AreEqual(1f, ArmorSystem.EffectiveDurabilityMultiplier(1.0f), 0.001f);
        }

        [Test]
        public void EffectiveDurabilityMultiplier_AtThreshold_ReturnsOne()
        {
            Assert.AreEqual(1f, ArmorSystem.EffectiveDurabilityMultiplier(0.7f), 0.001f);
        }

        [Test]
        public void EffectiveDurabilityMultiplier_BelowThreshold_ReturnsParabolic()
        {
            // 50% durability: t = 0.5/0.7 ≈ 0.714, t^2 ≈ 0.510
            float result = ArmorSystem.EffectiveDurabilityMultiplier(0.5f);
            Assert.AreEqual(0.510f, result, 0.01f);
        }

        [Test]
        public void EffectiveDurabilityMultiplier_ZeroDur_ReturnsZero()
        {
            Assert.AreEqual(0f, ArmorSystem.EffectiveDurabilityMultiplier(0f), 0.001f);
        }

        [Test]
        public void EffectiveDurabilityMultiplier_AboveThreshold_ReturnsOne()
        {
            Assert.AreEqual(1f, ArmorSystem.EffectiveDurabilityMultiplier(0.9f), 0.001f);
        }

        // ── EffectiveArmorPoints ──────────────────────────────

        [Test]
        public void EffectiveArmorPoints_NullArmor_ReturnsZero()
        {
            Assert.AreEqual(0f, ArmorSystem.EffectiveArmorPoints(null), 0.001f);
        }

        [Test]
        public void EffectiveArmorPoints_BrokenArmor_ReturnsZero()
        {
            var armor = ArmorState.Create(65f, 100f);
            armor.CurrentDurability = 0f;
            Assert.AreEqual(0f, ArmorSystem.EffectiveArmorPoints(armor), 0.001f);
        }

        [Test]
        public void EffectiveArmorPoints_FullDur_ReturnsBase()
        {
            var armor = ArmorState.Create(65f, 100f);
            Assert.AreEqual(65f, ArmorSystem.EffectiveArmorPoints(armor), 0.001f);
        }

        [Test]
        public void EffectiveArmorPoints_DegradedDur_ReturnsReduced()
        {
            var armor = ArmorState.Create(65f, 100f);
            armor.CurrentDurability = 50f; // 50% dur → below 70% threshold
            float effective = ArmorSystem.EffectiveArmorPoints(armor);
            Assert.Less(effective, 65f);
            Assert.Greater(effective, 0f);
        }

        // ── CalcDamageMultiplier ──────────────────────────────

        [Test]
        public void CalcDamageMultiplier_PenExceedsArmor_ReturnsOne()
        {
            Assert.AreEqual(1f, ArmorSystem.CalcDamageMultiplier(30f, 50f), 0.001f);
        }

        [Test]
        public void CalcDamageMultiplier_PenEqualsArmor_ReturnsOne()
        {
            Assert.AreEqual(1f, ArmorSystem.CalcDamageMultiplier(40f, 40f), 0.001f);
        }

        [Test]
        public void CalcDamageMultiplier_ArmorAdvantage10_Returns075()
        {
            // diff=10, K=30: 30/(30+10) = 0.75
            Assert.AreEqual(0.75f, ArmorSystem.CalcDamageMultiplier(50f, 40f), 0.001f);
        }

        [Test]
        public void CalcDamageMultiplier_ArmorAdvantage30_Returns05()
        {
            // diff=30, K=30: 30/(30+30) = 0.5
            Assert.AreEqual(0.5f, ArmorSystem.CalcDamageMultiplier(60f, 30f), 0.001f);
        }

        [Test]
        public void CalcDamageMultiplier_ArmorAdvantage80_ReturnsLow()
        {
            // diff=80, K=30: 30/(30+80) ≈ 0.273
            Assert.AreEqual(0.273f, ArmorSystem.CalcDamageMultiplier(80f, 0f), 0.01f);
        }

        // ── CalcArmorDurabilityDamage ─────────────────────────

        [Test]
        public void CalcArmorDurDamage_FullPen_ReturnsBase()
        {
            // absorptionRatio = 0 (full pen), so 1 + 0 = 1x
            Assert.AreEqual(10f, ArmorSystem.CalcArmorDurabilityDamage(10f, 0f), 0.001f);
        }

        [Test]
        public void CalcArmorDurDamage_HalfAbsorbed_Returns15x()
        {
            // absorptionRatio = 0.5, so 1 + 0.5 = 1.5x
            Assert.AreEqual(15f, ArmorSystem.CalcArmorDurabilityDamage(10f, 0.5f), 0.001f);
        }

        [Test]
        public void CalcArmorDurDamage_NearBlock_ReturnsHigh()
        {
            // absorptionRatio = 0.73, so 1 + 0.73 = 1.73x
            Assert.AreEqual(17.3f, ArmorSystem.CalcArmorDurabilityDamage(10f, 0.73f), 0.1f);
        }

        // ── GetArmorForHit ────────────────────────────────────

        [Test]
        public void GetArmorForHit_Headshot_ReturnsHelmet()
        {
            var slots = new ArmorSlotState
            {
                Helmet = ArmorState.Create(50f, 100f),
                BodyArmor = ArmorState.Create(40f, 120f),
            };
            var result = ArmorSystem.GetArmorForHit(slots, isHeadshot: true);
            Assert.AreEqual(50f, result.ArmorPoints, 0.001f);
        }

        [Test]
        public void GetArmorForHit_Bodyshot_ReturnsBodyArmor()
        {
            var slots = new ArmorSlotState
            {
                Helmet = ArmorState.Create(50f, 100f),
                BodyArmor = ArmorState.Create(40f, 120f),
            };
            var result = ArmorSystem.GetArmorForHit(slots, isHeadshot: false);
            Assert.AreEqual(40f, result.ArmorPoints, 0.001f);
        }

        [Test]
        public void GetArmorForHit_NullSlots_ReturnsNull()
        {
            Assert.IsNull(ArmorSystem.GetArmorForHit(null, true));
            Assert.IsNull(ArmorSystem.GetArmorForHit(null, false));
        }

        // ── Calculate (orchestrator) ──────────────────────────

        [Test]
        public void Calculate_NoArmor_FullDamage()
        {
            var result = ArmorSystem.Calculate(50f, 30f, 10f, null, false);
            Assert.AreEqual(50f, result.HpDamage, 0.001f);
            Assert.IsFalse(result.ArmorHit);
        }

        [Test]
        public void Calculate_BrokenArmor_FullDamage()
        {
            var slots = new ArmorSlotState { BodyArmor = ArmorState.Create(60f, 100f) };
            slots.BodyArmor.CurrentDurability = 0f;

            var result = ArmorSystem.Calculate(50f, 30f, 10f, slots, false);
            Assert.AreEqual(50f, result.HpDamage, 0.001f);
            Assert.IsFalse(result.ArmorHit);
        }

        [Test]
        public void Calculate_WithArmor_ReducesDamage()
        {
            // 60 armor, 30 pen → diff=30, multi=30/(30+30)=0.5 → 50*0.5=25
            var slots = new ArmorSlotState { BodyArmor = ArmorState.Create(60f, 100f) };
            var result = ArmorSystem.Calculate(50f, 30f, 10f, slots, false);

            Assert.AreEqual(25f, result.HpDamage, 0.5f);
            Assert.IsTrue(result.ArmorHit);
            Assert.AreEqual(0.5f, result.AbsorptionRatio, 0.05f);
        }

        [Test]
        public void Calculate_HeadshotUsesHelmet()
        {
            var slots = new ArmorSlotState
            {
                Helmet = ArmorState.Create(80f, 100f),
                BodyArmor = ArmorState.Create(40f, 120f),
            };
            // Headshot uses helmet (80 pts), pen 30 → diff=50, multi=30/80=0.375
            var result = ArmorSystem.Calculate(50f, 30f, 10f, slots, isHeadshot: true);
            Assert.AreEqual(50f * 0.375f, result.HpDamage, 0.5f);
        }

        [Test]
        public void Calculate_BodyshotUsesVest()
        {
            var slots = new ArmorSlotState
            {
                Helmet = ArmorState.Create(80f, 100f),
                BodyArmor = ArmorState.Create(40f, 120f),
            };
            // Bodyshot uses vest (40 pts), pen 30 → diff=10, multi=30/40=0.75
            var result = ArmorSystem.Calculate(50f, 30f, 10f, slots, isHeadshot: false);
            Assert.AreEqual(50f * 0.75f, result.HpDamage, 0.5f);
        }

        // ── ApplyDurabilityDamage ─────────────────────────────

        [Test]
        public void ApplyDurabilityDamage_ReducesCurrent()
        {
            var armor = ArmorState.Create(50f, 100f);
            ArmorSystem.ApplyDurabilityDamage(armor, 15f);
            Assert.AreEqual(85f, armor.CurrentDurability, 0.001f);
        }

        [Test]
        public void ApplyDurabilityDamage_ClampsAtZero()
        {
            var armor = ArmorState.Create(50f, 100f);
            ArmorSystem.ApplyDurabilityDamage(armor, 150f);
            Assert.AreEqual(0f, armor.CurrentDurability, 0.001f);
        }

        [Test]
        public void ApplyDurabilityDamage_NullArmor_NoException()
        {
            Assert.DoesNotThrow(() => ArmorSystem.ApplyDurabilityDamage(null, 10f));
        }
    }
}
