using Adapters;
using Dev;
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
        [SetUp]
        public void SetUp()
        {
            DevCheats.Reset();
            DevCheats.FOVEnabled = true;
            DevCheats.ForceShowAllBots = false;
            DevCheats.FOVOcclusionEnabled = true;
            DevCheats.FOVNearRadius = 5f;
            DevCheats.FOVFarRadius = 25f;
            DevCheats.FOVAngle = 130f;
        }

        [TearDown]
        public void TearDown()
        {
            DevCheats.Reset();
        }

        static RaidContext CreateContext(IPhysicsAdapter physics = null)
        {
            return new RaidContext(
                deltaTime: 1f / 60f,
                events: new RaidEventBuffer(),
                time: new FakeTimeAdapter { DeltaTime = 1f / 60f },
                input: new FakeInputAdapter(),
                navMesh: new FakeNavMeshAdapter(),
                physics: physics
            );
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
            var ctx = CreateContext();

            PlayerFOVSystem.Tick(state, in ctx);

            Assert.IsTrue(state.Bots[0].IsVisibleToPlayer);
        }

        [Test]
        public void BotInSectorAngle_IsVisible()
        {
            var state = CreateStateWithBot(Vector3.zero, Vector3.forward, new Vector3(0, 0, 15f));
            var ctx = CreateContext();

            PlayerFOVSystem.Tick(state, in ctx);

            Assert.IsTrue(state.Bots[0].IsVisibleToPlayer);
        }

        [Test]
        public void BotOutsideSector_NotVisible()
        {
            var state = CreateStateWithBot(Vector3.zero, Vector3.forward, new Vector3(0, 0, -15f));
            var ctx = CreateContext();

            PlayerFOVSystem.Tick(state, in ctx);

            Assert.IsFalse(state.Bots[0].IsVisibleToPlayer);
        }

        [Test]
        public void BotBeyondFarRadius_NotVisible()
        {
            var state = CreateStateWithBot(Vector3.zero, Vector3.forward, new Vector3(0, 0, 50f));
            var ctx = CreateContext();

            PlayerFOVSystem.Tick(state, in ctx);

            Assert.IsFalse(state.Bots[0].IsVisibleToPlayer);
        }

        [Test]
        public void BotBehindPlayer_InNearRadius_StillVisible()
        {
            var state = CreateStateWithBot(Vector3.zero, Vector3.forward, new Vector3(0, 0, -4f));
            var ctx = CreateContext();

            PlayerFOVSystem.Tick(state, in ctx);

            Assert.IsTrue(state.Bots[0].IsVisibleToPlayer);
        }

        [Test]
        public void FOVDisabled_AllBotsVisible()
        {
            DevCheats.FOVEnabled = false;
            var state = CreateStateWithBot(Vector3.zero, Vector3.forward, new Vector3(0, 0, -50f));
            var ctx = CreateContext();

            PlayerFOVSystem.Tick(state, in ctx);

            Assert.IsTrue(state.Bots[0].IsVisibleToPlayer);
        }

        [Test]
        public void ForceShowAllBots_AllVisible()
        {
            DevCheats.ForceShowAllBots = true;
            var state = CreateStateWithBot(Vector3.zero, Vector3.forward, new Vector3(0, 0, -50f));
            var ctx = CreateContext();

            PlayerFOVSystem.Tick(state, in ctx);

            Assert.IsTrue(state.Bots[0].IsVisibleToPlayer);
        }

        [Test]
        public void BotAtSectorEdge_IsVisible()
        {
            float angle = 59f * Mathf.Deg2Rad;
            var botPos = new Vector3(Mathf.Sin(angle) * 20f, 0f, Mathf.Cos(angle) * 20f);
            var state = CreateStateWithBot(Vector3.zero, Vector3.forward, botPos);
            var ctx = CreateContext();

            PlayerFOVSystem.Tick(state, in ctx);

            Assert.IsTrue(state.Bots[0].IsVisibleToPlayer);
        }

        [Test]
        public void BotJustOutsideSectorEdge_NotVisible()
        {
            float angle = 65f * Mathf.Deg2Rad;
            var botPos = new Vector3(Mathf.Sin(angle) * 20f, 0f, Mathf.Cos(angle) * 20f);
            var state = CreateStateWithBot(Vector3.zero, Vector3.forward, botPos);
            var ctx = CreateContext();

            PlayerFOVSystem.Tick(state, in ctx);

            Assert.IsFalse(state.Bots[0].IsVisibleToPlayer);
        }

        // ── Occlusion tests ─────────────────────────────────────

        [Test]
        public void BotInSector_Occluded_NotVisible()
        {
            var state = CreateStateWithBot(Vector3.zero, Vector3.forward, new Vector3(0, 0, 15f));
            var physics = new FakePhysicsAdapter { Blocked = true };
            var ctx = CreateContext(physics);

            PlayerFOVSystem.Tick(state, in ctx);

            Assert.IsFalse(state.Bots[0].IsVisibleToPlayer);
        }

        [Test]
        public void BotInNearRadius_Occluded_NotVisible()
        {
            var state = CreateStateWithBot(Vector3.zero, Vector3.forward, new Vector3(0, 0, 3f));
            var physics = new FakePhysicsAdapter { Blocked = true };
            var ctx = CreateContext(physics);

            PlayerFOVSystem.Tick(state, in ctx);

            Assert.IsFalse(state.Bots[0].IsVisibleToPlayer);
        }

        [Test]
        public void BotInSector_NotOccluded_IsVisible()
        {
            var state = CreateStateWithBot(Vector3.zero, Vector3.forward, new Vector3(0, 0, 15f));
            var physics = new FakePhysicsAdapter { Blocked = false };
            var ctx = CreateContext(physics);

            PlayerFOVSystem.Tick(state, in ctx);

            Assert.IsTrue(state.Bots[0].IsVisibleToPlayer);
        }

        [Test]
        public void BotInNearRadius_NotOccluded_IsVisible()
        {
            var state = CreateStateWithBot(Vector3.zero, Vector3.forward, new Vector3(0, 0, -3f));
            var physics = new FakePhysicsAdapter { Blocked = false };
            var ctx = CreateContext(physics);

            PlayerFOVSystem.Tick(state, in ctx);

            Assert.IsTrue(state.Bots[0].IsVisibleToPlayer);
        }

        [Test]
        public void OcclusionDisabledViaCheats_OccludedBotStillVisible()
        {
            DevCheats.FOVOcclusionEnabled = false;
            var state = CreateStateWithBot(Vector3.zero, Vector3.forward, new Vector3(0, 0, 15f));
            var physics = new FakePhysicsAdapter { Blocked = true };
            var ctx = CreateContext(physics);

            PlayerFOVSystem.Tick(state, in ctx);

            Assert.IsTrue(state.Bots[0].IsVisibleToPlayer);
        }

        [Test]
        public void NullPhysics_NoOcclusion_BotVisible()
        {
            var state = CreateStateWithBot(Vector3.zero, Vector3.forward, new Vector3(0, 0, 15f));
            var ctx = CreateContext(null);

            PlayerFOVSystem.Tick(state, in ctx);

            Assert.IsTrue(state.Bots[0].IsVisibleToPlayer);
        }
    }
}
