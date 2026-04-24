using Constants;
using NUnit.Framework;
using State;
using Systems;
using Tests.EditMode.Fakes;

namespace Tests.EditMode
{
    [TestFixture]
    public class StatusEffectSystemTests
    {

        // ── TickBleed L1/L2 ───────────────────────────────────

        [Test]
        public void TickBleed_L1_Applies3DmgPerTick()
        {
            var state = RaidState.Create(EditModeTestsUtils.NewAllocator());
            var entityId = state.AllocateEId();
            state.HealthMap[entityId] = HealthState.Create(100f);

            StatusEffectSystem.ApplyEffect(state, entityId, StatusEffectType.Bleeding);
            // Level defaults to 1

            state.ElapsedTime = 1.5f; // past 1s tick interval
            var context = TestContextFactory.Create();
            StatusEffectSystem.Tick(state, in context);

            Assert.AreEqual(100f - StatusEffectConstants.BleedL1DamagePerTick,
                state.HealthMap[entityId].CurrentHp, 0.001f);
        }

        [Test]
        public void TickBleed_L2_Applies6DmgPerTick()
        {
            var state = RaidState.Create(EditModeTestsUtils.NewAllocator());
            var entityId = state.AllocateEId();
            state.HealthMap[entityId] = HealthState.Create(100f);

            // Apply twice to upgrade to L2
            StatusEffectSystem.ApplyEffect(state, entityId, StatusEffectType.Bleeding);
            StatusEffectSystem.ApplyEffect(state, entityId, StatusEffectType.Bleeding);

            state.ElapsedTime = 1.5f;
            var context = TestContextFactory.Create();
            StatusEffectSystem.Tick(state, in context);

            Assert.AreEqual(100f - StatusEffectConstants.BleedL2DamagePerTick,
                state.HealthMap[entityId].CurrentHp, 0.001f);
        }

        // ── ApplyEffect upgrade ───────────────────────────────

        [Test]
        public void ApplyEffect_AlreadyL1_UpgradesToL2()
        {
            var state = RaidState.Create(EditModeTestsUtils.NewAllocator());
            var entityId = state.AllocateEId();

            StatusEffectSystem.ApplyEffect(state, entityId, StatusEffectType.Bleeding);
            Assert.AreEqual(1, StatusEffectSystem.GetBleedLevel(state, entityId));

            StatusEffectSystem.ApplyEffect(state, entityId, StatusEffectType.Bleeding);
            Assert.AreEqual(2, StatusEffectSystem.GetBleedLevel(state, entityId));
        }

        [Test]
        public void ApplyEffect_AlreadyL2_StaysL2()
        {
            var state = RaidState.Create(EditModeTestsUtils.NewAllocator());
            var entityId = state.AllocateEId();

            StatusEffectSystem.ApplyEffect(state, entityId, StatusEffectType.Bleeding);
            StatusEffectSystem.ApplyEffect(state, entityId, StatusEffectType.Bleeding);
            Assert.AreEqual(2, StatusEffectSystem.GetBleedLevel(state, entityId));

            // Third apply — should stay at L2
            StatusEffectSystem.ApplyEffect(state, entityId, StatusEffectType.Bleeding);
            Assert.AreEqual(2, StatusEffectSystem.GetBleedLevel(state, entityId));
        }

        // ── GetBleedLevel ─────────────────────────────────────

        [Test]
        public void GetBleedLevel_NoBleeding_ReturnsZero()
        {
            var state = RaidState.Create(EditModeTestsUtils.NewAllocator());
            var entityId = state.AllocateEId();

            Assert.AreEqual(0, StatusEffectSystem.GetBleedLevel(state, entityId));
        }

        [Test]
        public void GetBleedLevel_L1_ReturnsOne()
        {
            var state = RaidState.Create(EditModeTestsUtils.NewAllocator());
            var entityId = state.AllocateEId();

            StatusEffectSystem.ApplyEffect(state, entityId, StatusEffectType.Bleeding);
            Assert.AreEqual(1, StatusEffectSystem.GetBleedLevel(state, entityId));
        }

        // ── DowngradeBleed ────────────────────────────────────

        [Test]
        public void DowngradeBleed_L2_BecomesL1()
        {
            var state = RaidState.Create(EditModeTestsUtils.NewAllocator());
            var entityId = state.AllocateEId();

            StatusEffectSystem.ApplyEffect(state, entityId, StatusEffectType.Bleeding);
            StatusEffectSystem.ApplyEffect(state, entityId, StatusEffectType.Bleeding);
            Assert.AreEqual(2, StatusEffectSystem.GetBleedLevel(state, entityId));

            StatusEffectSystem.DowngradeBleed(state, entityId);
            Assert.AreEqual(1, StatusEffectSystem.GetBleedLevel(state, entityId));
        }

        [Test]
        public void DowngradeBleed_L1_RemovesFully()
        {
            var state = RaidState.Create(EditModeTestsUtils.NewAllocator());
            var entityId = state.AllocateEId();

            StatusEffectSystem.ApplyEffect(state, entityId, StatusEffectType.Bleeding);
            StatusEffectSystem.DowngradeBleed(state, entityId);

            Assert.AreEqual(0, StatusEffectSystem.GetBleedLevel(state, entityId));
            Assert.IsFalse(StatusEffectSystem.HasEffect(state, entityId, StatusEffectType.Bleeding));
        }

        [Test]
        public void DowngradeBleed_L2ThenL1_TwoDowngrades_FullCure()
        {
            var state = RaidState.Create(EditModeTestsUtils.NewAllocator());
            var entityId = state.AllocateEId();

            StatusEffectSystem.ApplyEffect(state, entityId, StatusEffectType.Bleeding);
            StatusEffectSystem.ApplyEffect(state, entityId, StatusEffectType.Bleeding);
            Assert.AreEqual(2, StatusEffectSystem.GetBleedLevel(state, entityId));

            StatusEffectSystem.DowngradeBleed(state, entityId);
            Assert.AreEqual(1, StatusEffectSystem.GetBleedLevel(state, entityId));

            StatusEffectSystem.DowngradeBleed(state, entityId);
            Assert.AreEqual(0, StatusEffectSystem.GetBleedLevel(state, entityId));
        }
    }
}
