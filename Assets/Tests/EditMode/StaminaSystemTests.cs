using NUnit.Framework;
using Session;
using State;
using Systems;
using Tests.EditMode.Fakes;
using UnityEngine;

namespace Tests.EditMode
{
    /// <summary>
    /// Covers the exhaustion hysteresis gate added 2026-05-26 (Battle HUD Stage 5).
    /// The core invariant: once stamina empties, sprint is LOCKED until stamina recovers
    /// past <c>ExhaustionRecoveryRatio</c> — a single regen tick at empty must NOT re-enable
    /// sprint (no stutter-sprint).
    ///
    /// Regen-path tests set LastSprintStopTime far in the past so the RegenDelay window is
    /// already elapsed at the fake clock's default Time=0 (avoids plumbing a custom clock).
    /// </summary>
    [TestFixture]
    public class StaminaSystemTests
    {
        static RaidState NewPlayerState(float stamina, float maxStamina = 100f)
        {
            var state = RaidState.Create(EditModeTestsUtils.NewAllocator());
            var pid = state.AllocateEId();
            state.PlayerEntity = PlayerEntityState.Create(pid, Vector3.zero);
            state.PlayerEntity.Stamina = stamina;
            state.PlayerEntity.MaxStamina = maxStamina;
            return state;
        }

        static FakeInputAdapter Sprinting() =>
            new FakeInputAdapter { MoveInput = new Vector2(0f, 1f), SprintPressed = true };

        // ── Drain → exhaustion latch ─────────────────────────────────────────

        [Test]
        public void Drain_HitsZero_SetsExhausted()
        {
            var state = NewPlayerState(stamina: 0.1f);
            var ctx = TestContextFactory.Create(input: Sprinting());

            StaminaSystem.Tick(state, in ctx);

            Assert.AreEqual(0f, state.PlayerEntity.Stamina, 0.001f);
            Assert.IsTrue(state.PlayerEntity.IsExhausted, "Should latch exhausted at empty");
        }

        [Test]
        public void Exhausted_LocksSprint_EvenWhileHoldingSprint()
        {
            var state = NewPlayerState(stamina: 0.1f);
            var ctx = TestContextFactory.Create(input: Sprinting());

            StaminaSystem.Tick(state, in ctx); // drains to 0, latches exhausted
            StaminaSystem.Tick(state, in ctx); // still holding sprint

            Assert.IsTrue(state.PlayerEntity.IsExhausted);
            Assert.IsFalse(state.PlayerEntity.IsSprinting, "Sprint must stay locked while exhausted");
        }

        // ── Hysteresis: tiny regen does NOT unlock ───────────────────────────

        [Test]
        public void Exhausted_StaysLocked_BelowRecoveryThreshold()
        {
            // Max 100, ratio 0.10 → unlock at 10. Start just-regenerating from 5.
            var state = NewPlayerState(stamina: 5f);
            state.PlayerEntity.IsExhausted = true;
            state.PlayerEntity.LastSprintStopTime = -100f; // regen window already elapsed

            var ctx = TestContextFactory.Create(input: Sprinting());
            StaminaSystem.Tick(state, in ctx);

            Assert.Less(state.PlayerEntity.Stamina, 10f, "Should still be below threshold");
            Assert.IsTrue(state.PlayerEntity.IsExhausted, "Must remain exhausted below threshold");
            Assert.IsFalse(state.PlayerEntity.IsSprinting, "Sprint still locked below threshold");
        }

        [Test]
        public void Recovery_ClearsExhausted_AtThreshold()
        {
            // Start at 9.9; one regen tick (15/s × 1/60 = 0.25) crosses 10.
            var state = NewPlayerState(stamina: 9.9f);
            state.PlayerEntity.IsExhausted = true;
            state.PlayerEntity.LastSprintStopTime = -100f;

            var ctx = TestContextFactory.Create(input: new FakeInputAdapter()); // idle → regen
            StaminaSystem.Tick(state, in ctx);

            Assert.GreaterOrEqual(state.PlayerEntity.Stamina, 10f);
            Assert.IsFalse(state.PlayerEntity.IsExhausted, "Should clear exhausted at/above threshold");
        }

        [Test]
        public void Recovered_AllowsSprintAgain()
        {
            var state = NewPlayerState(stamina: 50f); // well above threshold, not exhausted
            var ctx = TestContextFactory.Create(input: Sprinting());

            StaminaSystem.Tick(state, in ctx);

            Assert.IsFalse(state.PlayerEntity.IsExhausted);
            Assert.IsTrue(state.PlayerEntity.IsSprinting, "Sprint enabled when not exhausted + has stamina");
        }

        // ── Config plumbing ──────────────────────────────────────────────────

        [Test]
        public void ExhaustionRecoveryRatio_FromConfig_IsRespected()
        {
            // Custom threshold 0.50 → unlock only at 50. Stamina 30 must stay locked.
            var state = NewPlayerState(stamina: 30f);
            state.PlayerEntity.IsExhausted = true;
            state.PlayerEntity.LastSprintStopTime = -100f;

            var cfg = StaminaConfig.Default;
            cfg.ExhaustionRecoveryRatio = 0.50f;
            var ctx = TestContextFactory.Create(input: new FakeInputAdapter(), staminaConfig: cfg);

            StaminaSystem.Tick(state, in ctx);

            Assert.IsTrue(state.PlayerEntity.IsExhausted, "30 < 50% threshold → still locked");
        }
    }
}
