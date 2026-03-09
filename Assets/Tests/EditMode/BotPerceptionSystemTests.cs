using Adapters;
using NUnit.Framework;
using Session;
using State;
using Systems.Bot;
using Tests.EditMode.Fakes;
using UnityEngine;

namespace Tests.EditMode
{
    [TestFixture]
    public class BotPerceptionSystemTests
    {
        static RaidContext CreateContext(float dt = 1f / 60f)
        {
            return new RaidContext(
                deltaTime: dt,
                events: new RaidEventBuffer(),
                time: new FakeTimeAdapter { DeltaTime = dt },
                input: new FakeInputAdapter(),
                navMesh: new FakeNavMeshAdapter()
            );
        }

        static RaidState CreateStateWithPlayerAndBot(Vector3 playerPos, Vector3 botPos,
            string botType = "Scav")
        {
            var state = EditModeTestsUtils.CreateStateWithPlayer(playerPos);
            var events = new FakeRaidEvents();
            BotSpawnSystem.SpawnBot(state, botType, botPos, new[] { botPos }, events);
            state.Bots[0].Blackboard.PerceptionTimer = 0f;
            return state;
        }

        [Test]
        public void Tick_PlayerInVisionRange_DetectsTarget()
        {
            var state = CreateStateWithPlayerAndBot(Vector3.zero, new Vector3(0, 0, 10f));
            state.Bots[0].FacingDirection = -Vector3.forward;
            var ctx = CreateContext();

            BotPerceptionSystem.Tick(state, in ctx);

            Assert.IsTrue(state.Bots[0].Blackboard.HasTarget);
            Assert.IsTrue(state.Bots[0].Blackboard.CanSeeTarget);
        }

        [Test]
        public void Tick_PlayerOutOfRange_NoDetection()
        {
            var state = CreateStateWithPlayerAndBot(Vector3.zero, new Vector3(0, 0, 100f));
            state.Bots[0].FacingDirection = -Vector3.forward;
            var ctx = CreateContext();

            BotPerceptionSystem.Tick(state, in ctx);

            Assert.IsFalse(state.Bots[0].Blackboard.HasTarget);
        }

        [Test]
        public void Tick_PlayerBehindBot_NotSeen()
        {
            var state = CreateStateWithPlayerAndBot(Vector3.zero, new Vector3(0, 0, 10f));
            state.Bots[0].FacingDirection = Vector3.forward;
            var ctx = CreateContext();

            BotPerceptionSystem.Tick(state, in ctx);

            Assert.IsFalse(state.Bots[0].Blackboard.CanSeeTarget);
        }

        [Test]
        public void Tick_PlayerMovingNearby_HeardByBot()
        {
            var state = CreateStateWithPlayerAndBot(Vector3.zero, new Vector3(0, 0, 10f));
            state.Bots[0].FacingDirection = Vector3.forward;
            state.PlayerEntity.Velocity = Vector3.right * 5f;
            var ctx = CreateContext();

            BotPerceptionSystem.Tick(state, in ctx);

            Assert.IsTrue(state.Bots[0].Blackboard.HasTarget);
        }

        [Test]
        public void Tick_TargetLostAfterMemoryExpires()
        {
            var state = CreateStateWithPlayerAndBot(Vector3.zero, new Vector3(0, 0, 10f));
            state.Bots[0].FacingDirection = -Vector3.forward;
            var ctx = CreateContext();

            BotPerceptionSystem.Tick(state, in ctx);
            Assert.IsTrue(state.Bots[0].Blackboard.HasTarget);

            state.PlayerEntity.Position = new Vector3(0, 0, 200f);
            state.PlayerEntity.Velocity = Vector3.zero;
            state.Bots[0].Blackboard.TimeSinceTargetSeen = 100f;
            state.Bots[0].Blackboard.PerceptionTimer = 0f;

            BotPerceptionSystem.Tick(state, in ctx);

            Assert.IsFalse(state.Bots[0].Blackboard.HasTarget);
        }
    }
}
