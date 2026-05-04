using Adapters;
using NUnit.Framework;
using Session;
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
        // Per-test FOVConfig replaces former DevCheats SetUp/TearDown — fixture is now
        // self-contained and cannot pollute later test runs (P0-1 refactor).

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
            var state = CreateStateWithBot(Vector3.zero, Vector3.forward, new Vector3(0, 0, -50f));
            var fov = FOVConfig.Default;
            fov.Enabled = false;
            var ctx = TestContextFactory.Create(fovConfig: fov);

            PlayerFOVSystem.Tick(state, in ctx);

            Assert.IsTrue(state.Bots[0].IsVisibleToPlayer);
        }

        [Test]
        public void ForceShowAllBots_AllVisible()
        {
            var state = CreateStateWithBot(Vector3.zero, Vector3.forward, new Vector3(0, 0, -50f));
            var fov = FOVConfig.Default;
            fov.ForceShowAllBots = true;
            var ctx = TestContextFactory.Create(fovConfig: fov);

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
            var state = CreateStateWithBot(Vector3.zero, Vector3.forward, new Vector3(0, 0, 15f));
            var physics = new FakePhysicsAdapter { Blocked = true };
            var fov = FOVConfig.Default;
            fov.OcclusionEnabled = false;
            var ctx = TestContextFactory.Create(physics: physics, fovConfig: fov);

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
