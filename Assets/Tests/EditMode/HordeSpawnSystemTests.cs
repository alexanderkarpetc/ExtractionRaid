using Adapters;
using Dev;
using NUnit.Framework;
using State;
using Systems;
using Tests.EditMode.Fakes;
using UnityEngine;

namespace Tests.EditMode
{
    /// <summary>
    /// Coverage для горде-сцени spawner: grace period, interval, cap.
    /// Pure-logic — bypasses scene + Unity physics by passing FakeRaidEvents directly.
    /// </summary>
    [TestFixture]
    public class HordeSpawnSystemTests
    {
        DevCheatsHordeSection _cfg;
        bool _origEnabled;
        float _origGrace;
        float _origInterval;
        int _origBatch;
        int _origCap;

        [SetUp]
        public void SetUp()
        {
            EditModeTestsUtils.EnsureAppForTests();
            _cfg = DevCheats.Config.Horde;
            // Snapshot params we mutate so tests don't bleed state via the shared SO.
            _origEnabled  = _cfg.Enabled;
            _origGrace    = _cfg.GracePeriod;
            _origInterval = _cfg.SpawnInterval;
            _origBatch    = _cfg.SpawnBatchSize;
            _origCap      = _cfg.MaxAlive;

            _cfg.Enabled = true;
            _cfg.GracePeriod = 5f;
            _cfg.SpawnInterval = 1f;
            _cfg.SpawnBatchSize = 1;
            _cfg.MaxAlive = 3;
        }

        [TearDown]
        public void TearDown()
        {
            _cfg.Enabled = _origEnabled;
            _cfg.GracePeriod = _origGrace;
            _cfg.SpawnInterval = _origInterval;
            _cfg.SpawnBatchSize = _origBatch;
            _cfg.MaxAlive = _origCap;
        }

        [Test]
        public void Tick_DuringGracePeriod_NoSpawns()
        {
            var state = EditModeTestsUtils.CreateStateWithPlayer(Vector3.zero);
            state.ElapsedTime = 2f; // < 5s grace
            var ctx    = TestContextFactory.Create();
            var events = new FakeRaidEvents();

            HordeSpawnSystem.Tick(state, in ctx, events, null);

            Assert.AreEqual(0, state.Bots.Count);
        }

        [Test]
        public void Tick_AfterGrace_SpawnsFirstWave()
        {
            var state = EditModeTestsUtils.CreateStateWithPlayer(Vector3.zero);
            state.ElapsedTime = 5.5f;
            var ctx    = TestContextFactory.Create();
            var events = new FakeRaidEvents();

            HordeSpawnSystem.Tick(state, in ctx, events, null);

            Assert.AreEqual(1, state.Bots.Count);
            Assert.AreEqual("Zombie", state.Bots[0].TypeId);
        }

        [Test]
        public void Tick_BeforeInterval_NoSecondSpawn()
        {
            var state = EditModeTestsUtils.CreateStateWithPlayer(Vector3.zero);
            state.ElapsedTime = 5.5f;
            var ctx    = TestContextFactory.Create();
            var events = new FakeRaidEvents();

            HordeSpawnSystem.Tick(state, in ctx, events, null);
            // Same tick again before interval elapses → no extra spawn.
            HordeSpawnSystem.Tick(state, in ctx, events, null);

            Assert.AreEqual(1, state.Bots.Count);
        }

        [Test]
        public void Tick_PastCap_DoesNotOverspawn()
        {
            _cfg.MaxAlive = 2;
            _cfg.SpawnBatchSize = 5;

            var state = EditModeTestsUtils.CreateStateWithPlayer(Vector3.zero);
            state.ElapsedTime = 5.5f;
            var ctx    = TestContextFactory.Create();
            var events = new FakeRaidEvents();

            HordeSpawnSystem.Tick(state, in ctx, events, null);

            Assert.AreEqual(2, state.Bots.Count);
        }

        [Test]
        public void Tick_DisabledSection_NoSpawns()
        {
            _cfg.Enabled = false;
            var state = EditModeTestsUtils.CreateStateWithPlayer(Vector3.zero);
            state.ElapsedTime = 999f;
            var ctx    = TestContextFactory.Create();
            var events = new FakeRaidEvents();

            HordeSpawnSystem.Tick(state, in ctx, events, null);

            Assert.AreEqual(0, state.Bots.Count);
        }

        [Test]
        public void Tick_SpawnPosition_OnRingAroundPlayer()
        {
            var playerPos = new Vector3(5f, 0f, 5f);
            var state = EditModeTestsUtils.CreateStateWithPlayer(playerPos);
            state.ElapsedTime = 10f;
            _cfg.SpawnRingRadius = 10f;
            _cfg.SpawnRingJitter = 0f;
            var ctx    = TestContextFactory.Create();
            var events = new FakeRaidEvents();

            HordeSpawnSystem.Tick(state, in ctx, events, null);

            var z = state.Bots[0];
            float dist = Vector3.Distance(z.Position, playerPos);
            Assert.AreEqual(10f, dist, 0.01f);
        }
    }
}
