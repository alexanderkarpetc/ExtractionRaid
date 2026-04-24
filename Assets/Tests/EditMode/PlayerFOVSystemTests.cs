using Adapters;
using Dev;
using NUnit.Framework;
using State;
using Systems;
using Systems.Bot;
using Tests.EditMode.Fakes;
using UnityEngine;

namespace Tests.EditMode
{
    [TestFixture]
    public class PlayerFOVSystemTests
    {
        // NOTE: PlayerFOVSystem reads DevCheats.* directly (see tests-review.md P4-α).
        // Until that's refactored into a Config struct on RaidContext, we own the
        // DevCheats state for the duration of each test and restore defaults in TearDown
        // so later test fixtures aren't polluted with our values.

        bool _savedFovEnabled;
        bool _savedForceShowAllBots;
        bool _savedFovOcclusion;
        float _savedNearRadius;
        float _savedFarRadius;
        float _savedAngle;

        [SetUp]
        public void SetUp()
        {
            _savedFovEnabled       = DevCheats.FOVEnabled;
            _savedForceShowAllBots = DevCheats.ForceShowAllBots;
            _savedFovOcclusion     = DevCheats.FOVOcclusionEnabled;
            _savedNearRadius       = DevCheats.FOVNearRadius;
            _savedFarRadius        = DevCheats.FOVFarRadius;
            _savedAngle            = DevCheats.FOVAngle;

            DevCheats.FOVEnabled          = true;
            DevCheats.ForceShowAllBots    = false;
            DevCheats.FOVOcclusionEnabled = true;
            DevCheats.FOVNearRadius       = 5f;
            DevCheats.FOVFarRadius        = 25f;
            DevCheats.FOVAngle            = 130f;
        }

        [TearDown]
        public void TearDown()
        {
            DevCheats.FOVEnabled          = _savedFovEnabled;
            DevCheats.ForceShowAllBots    = _savedForceShowAllBots;
            DevCheats.FOVOcclusionEnabled = _savedFovOcclusion;
            DevCheats.FOVNearRadius       = _savedNearRadius;
            DevCheats.FOVFarRadius        = _savedFarRadius;
            DevCheats.FOVAngle            = _savedAngle;
        }

        static RaidState CreateStateWithBot(Vector3 playerPos, Vector3 playerFacing, Vector3 botPos)
        {
            var state = EditModeTestsUtils.CreateStateWithPlayer(playerPos);
            state.PlayerEntity.FacingDirection = playerFacing;

            var events = new FakeRaidEvents();
            BotSpawnSystem.SpawnBot(state, "Scav", botPos, new[] { botPos }, events);
            return state;
        }

        // ── Distance + Angle tests (no physics) ─────────────────

        [Test]
        public void BotInNearRadius_IsVisible()
        {
            var state = CreateStateWithBot(Vector3.zero, Vector3.forward, new Vector3(0, 0, -3f));
            var ctx = TestContextFactory.Create();

            PlayerFOVSystem.Tick(state, in ctx);

            Assert.IsTrue(state.Bots[0].IsVisibleToPlayer);
        }

        [Test]
        public void BotInSectorAngle_IsVisible()
        {
            var state = CreateStateWithBot(Vector3.zero, Vector3.forward, new Vector3(0, 0, 15f));
            var ctx = TestContextFactory.Create();

            PlayerFOVSystem.Tick(state, in ctx);

            Assert.IsTrue(state.Bots[0].IsVisibleToPlayer);
        }

        [Test]
        public void BotOutsideSector_NotVisible()
        {
            var state = CreateStateWithBot(Vector3.zero, Vector3.forward, new Vector3(0, 0, -15f));
            var ctx = TestContextFactory.Create();

            PlayerFOVSystem.Tick(state, in ctx);

            Assert.IsFalse(state.Bots[0].IsVisibleToPlayer);
        }

        [Test]
        public void BotBeyondFarRadius_NotVisible()
        {
            var state = CreateStateWithBot(Vector3.zero, Vector3.forward, new Vector3(0, 0, 50f));
            var ctx = TestContextFactory.Create();

            PlayerFOVSystem.Tick(state, in ctx);

            Assert.IsFalse(state.Bots[0].IsVisibleToPlayer);
        }

        [Test]
        public void BotBehindPlayer_InNearRadius_StillVisible()
        {
            var state = CreateStateWithBot(Vector3.zero, Vector3.forward, new Vector3(0, 0, -4f));
            var ctx = TestContextFactory.Create();

            PlayerFOVSystem.Tick(state, in ctx);

            Assert.IsTrue(state.Bots[0].IsVisibleToPlayer);
        }

        [Test]
        public void FOVDisabled_AllBotsVisible()
        {
            DevCheats.FOVEnabled = false;
            var state = CreateStateWithBot(Vector3.zero, Vector3.forward, new Vector3(0, 0, -50f));
            var ctx = TestContextFactory.Create();

            PlayerFOVSystem.Tick(state, in ctx);

            Assert.IsTrue(state.Bots[0].IsVisibleToPlayer);
        }

        [Test]
        public void ForceShowAllBots_AllVisible()
        {
            DevCheats.ForceShowAllBots = true;
            var state = CreateStateWithBot(Vector3.zero, Vector3.forward, new Vector3(0, 0, -50f));
            var ctx = TestContextFactory.Create();

            PlayerFOVSystem.Tick(state, in ctx);

            Assert.IsTrue(state.Bots[0].IsVisibleToPlayer);
        }

        [Test]
        public void BotAtSectorEdge_IsVisible()
        {
            float angle = 59f * Mathf.Deg2Rad;
            var botPos = new Vector3(Mathf.Sin(angle) * 20f, 0f, Mathf.Cos(angle) * 20f);
            var state = CreateStateWithBot(Vector3.zero, Vector3.forward, botPos);
            var ctx = TestContextFactory.Create();

            PlayerFOVSystem.Tick(state, in ctx);

            Assert.IsTrue(state.Bots[0].IsVisibleToPlayer);
        }

        [Test]
        public void BotJustOutsideSectorEdge_NotVisible()
        {
            float angle = 66f * Mathf.Deg2Rad;
            var botPos = new Vector3(Mathf.Sin(angle) * 20f, 0f, Mathf.Cos(angle) * 20f);
            var state = CreateStateWithBot(Vector3.zero, Vector3.forward, botPos);
            var ctx = TestContextFactory.Create();

            PlayerFOVSystem.Tick(state, in ctx);

            Assert.IsFalse(state.Bots[0].IsVisibleToPlayer);
        }

        // ── Occlusion tests ─────────────────────────────────────

        [Test]
        public void BotInSector_Occluded_NotVisible()
        {
            var state = CreateStateWithBot(Vector3.zero, Vector3.forward, new Vector3(0, 0, 15f));
            var physics = new FakePhysicsAdapter { Blocked = true };
            var ctx = TestContextFactory.Create(physics: physics);

            PlayerFOVSystem.Tick(state, in ctx);

            Assert.IsFalse(state.Bots[0].IsVisibleToPlayer);
        }

        [Test]
        public void BotInNearRadius_Occluded_NotVisible()
        {
            var state = CreateStateWithBot(Vector3.zero, Vector3.forward, new Vector3(0, 0, 3f));
            var physics = new FakePhysicsAdapter { Blocked = true };
            var ctx = TestContextFactory.Create(physics: physics);

            PlayerFOVSystem.Tick(state, in ctx);

            Assert.IsFalse(state.Bots[0].IsVisibleToPlayer);
        }

        [Test]
        public void BotInSector_NotOccluded_IsVisible()
        {
            var state = CreateStateWithBot(Vector3.zero, Vector3.forward, new Vector3(0, 0, 15f));
            var physics = new FakePhysicsAdapter { Blocked = false };
            var ctx = TestContextFactory.Create(physics: physics);

            PlayerFOVSystem.Tick(state, in ctx);

            Assert.IsTrue(state.Bots[0].IsVisibleToPlayer);
        }

        [Test]
        public void BotInNearRadius_NotOccluded_IsVisible()
        {
            var state = CreateStateWithBot(Vector3.zero, Vector3.forward, new Vector3(0, 0, -3f));
            var physics = new FakePhysicsAdapter { Blocked = false };
            var ctx = TestContextFactory.Create(physics: physics);

            PlayerFOVSystem.Tick(state, in ctx);

            Assert.IsTrue(state.Bots[0].IsVisibleToPlayer);
        }

        [Test]
        public void OcclusionDisabledViaCheats_OccludedBotStillVisible()
        {
            DevCheats.FOVOcclusionEnabled = false;
            var state = CreateStateWithBot(Vector3.zero, Vector3.forward, new Vector3(0, 0, 15f));
            var physics = new FakePhysicsAdapter { Blocked = true };
            var ctx = TestContextFactory.Create(physics: physics);

            PlayerFOVSystem.Tick(state, in ctx);

            Assert.IsTrue(state.Bots[0].IsVisibleToPlayer);
        }

        [Test]
        public void NullPhysics_NoOcclusion_BotVisible()
        {
            var state = CreateStateWithBot(Vector3.zero, Vector3.forward, new Vector3(0, 0, 15f));
            var ctx = TestContextFactory.Create(physics: null);

            PlayerFOVSystem.Tick(state, in ctx);

            Assert.IsTrue(state.Bots[0].IsVisibleToPlayer);
        }
    }
}
