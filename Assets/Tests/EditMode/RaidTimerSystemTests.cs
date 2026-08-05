using NUnit.Framework;
using Session;
using State;
using Systems;
using Tests.EditMode.Fakes;
using UnityEngine;

namespace Tests.EditMode
{
    /// <summary>
    /// Raid clock (M1.2). The system owns one decision — "time is up, kill the player" — and the
    /// rules around it are all about NOT firing: no clock on this level, time left, already dead,
    /// already extracted. Death itself rides the normal <c>EntityDied</c> path, which is what makes
    /// <c>RaidSession.ProcessDeathEvents</c> turn it into a KIA outcome + gear wipe.
    /// </summary>
    [TestFixture]
    public class RaidTimerSystemTests
    {
        const float Duration = 60f;

        [Test]
        public void Tick_NoClockOnLevel_NeverKills()
        {
            var (state, events) = MakeState(duration: 0f, elapsed: 99999f);
            var context = MakeContext(events);

            RaidTimerSystem.Tick(state, in context);

            Assert.IsFalse(events.EntityDiedCalled, "Duration 0 means the level has no clock at all.");
            Assert.IsTrue(state.HealthMap[state.PlayerEntity.Id].IsAlive);
        }

        [Test]
        public void Tick_TimeLeft_LeavesPlayerAlone()
        {
            var (state, events) = MakeState(duration: Duration, elapsed: Duration - 0.5f);
            var context = MakeContext(events);

            RaidTimerSystem.Tick(state, in context);

            Assert.IsFalse(events.EntityDiedCalled);
            Assert.IsTrue(state.HealthMap[state.PlayerEntity.Id].IsAlive);
        }

        [Test]
        public void Tick_TimeUp_KillsPlayerThroughTheDeathEvent()
        {
            var (state, events) = MakeState(duration: Duration, elapsed: Duration);
            var context = MakeContext(events);

            RaidTimerSystem.Tick(state, in context);

            var health = state.HealthMap[state.PlayerEntity.Id];
            Assert.IsTrue(events.EntityDiedCalled, "The KIA outcome is driven by EntityDied downstream.");
            Assert.AreEqual(state.PlayerEntity.Id, events.EntityDiedId);
            Assert.IsFalse(health.IsAlive);
            Assert.AreEqual(0f, health.CurrentHp);
        }

        [Test]
        public void Tick_AlreadyDead_DoesNotFireASecondDeath()
        {
            var (state, events) = MakeState(duration: Duration, elapsed: Duration);
            var context = MakeContext(events);
            RaidTimerSystem.Tick(state, in context);
            events.EntityDiedCalled = false;

            RaidTimerSystem.Tick(state, in context);

            Assert.IsFalse(events.EntityDiedCalled, "A corpse must not re-emit EntityDied every frame.");
        }

        [Test]
        public void Tick_ExtractionCompleted_WinsTheTie()
        {
            var (state, events) = MakeState(duration: Duration, elapsed: Duration);
            state.PlayerEntity.ExtractionProgress01 = 1f;
            var context = MakeContext(events);

            RaidTimerSystem.Tick(state, in context);

            Assert.IsFalse(events.EntityDiedCalled,
                "Extraction ticks earlier in the frame — a completed run must not flip to KIA.");
            Assert.IsTrue(state.HealthMap[state.PlayerEntity.Id].IsAlive);
        }

        [Test]
        public void Tick_GodMode_DoesNotStopTheClock()
        {
            var (state, events) = MakeState(duration: Duration, elapsed: Duration);
            var context = MakeContext(events, godMode: true);

            RaidTimerSystem.Tick(state, in context);

            Assert.IsTrue(events.EntityDiedCalled,
                "GodMode is a combat cheat; the deadline is escaped by setting duration 0.");
        }

        [Test]
        public void TimeRemaining_CountsDownAndFloorsAtZero()
        {
            var (state, _) = MakeState(duration: Duration, elapsed: 20f);
            Assert.AreEqual(40f, RaidTimerSystem.TimeRemaining(state), 0.001f);
            Assert.IsTrue(RaidTimerSystem.HasClock(state));

            state.ElapsedTime = Duration + 10f;
            Assert.AreEqual(0f, RaidTimerSystem.TimeRemaining(state), "Overshoot must not read negative.");

            state.RaidDurationSeconds = 0f;
            Assert.IsFalse(RaidTimerSystem.HasClock(state));
            Assert.AreEqual(0f, RaidTimerSystem.TimeRemaining(state));
        }

        // ── helpers ──────────────────────────────────────────

        static (RaidState, FakeRaidEvents) MakeState(float duration, float elapsed)
        {
            int next = 0;
            var state = RaidState.Create(() => new EId(++next));
            state.RaidDurationSeconds = duration;
            state.ElapsedTime = elapsed;

            var playerId = state.AllocateEId();
            state.PlayerEntity = PlayerEntityState.Create(playerId, Vector3.zero);
            state.HealthMap[playerId] = HealthState.Create(100f);

            return (state, new FakeRaidEvents());
        }

        static RaidContext MakeContext(FakeRaidEvents events, bool godMode = false) =>
            TestContextFactory.Create(
                events: events,
                cheatsConfig: new CheatsConfig { GodMode = godMode });
    }
}
