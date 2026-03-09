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
    public class BotBrainSystemTests
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

        static RaidState CreateStateWithBot(string typeId, Vector3 botPos, Vector3[] waypoints = null)
        {
            var state = EditModeTestsUtils.CreateStateWithPlayer(Vector3.zero);
            var events = new FakeRaidEvents();
            BotSpawnSystem.SpawnBot(state, typeId, botPos,
                waypoints ?? new[] { botPos, botPos + Vector3.forward * 10f }, events);
            return state;
        }

        [Test]
        public void Tick_NoTarget_ScavPatrols()
        {
            var waypoints = new[] { new Vector3(0, 0, 10f), new Vector3(10f, 0, 0) };
            var state = CreateStateWithBot("Scav", Vector3.zero, waypoints);
            var ctx = CreateContext();

            BotBrainSystem.Tick(state, in ctx);

            Assert.AreNotEqual(Vector3.zero, state.Bots[0].DesiredVelocity,
                "Bot should have patrol velocity");
        }

        [Test]
        public void Tick_WithVisibleTarget_BotWantsToFire()
        {
            var state = CreateStateWithBot("Scav", new Vector3(0, 0, 10f));
            var bot = state.Bots[0];
            bot.FacingDirection = -Vector3.forward;
            bot.Blackboard.HasTarget = true;
            bot.Blackboard.CanSeeTarget = true;
            bot.Blackboard.DistanceToTarget = 10f;
            bot.Blackboard.LastKnownTargetPos = Vector3.zero;
            bot.Blackboard.ReactionTimer = 999f;
            var ctx = CreateContext();

            BotBrainSystem.Tick(state, in ctx);

            Assert.IsTrue(bot.WantsToFire, "Bot should want to fire when target visible and reacted");
        }

        [Test]
        public void Tick_WithTarget_BotChases()
        {
            var state = CreateStateWithBot("Scav", new Vector3(0, 0, 20f));
            var bot = state.Bots[0];
            bot.Blackboard.HasTarget = true;
            bot.Blackboard.CanSeeTarget = true;
            bot.Blackboard.DistanceToTarget = 20f;
            bot.Blackboard.LastKnownTargetPos = Vector3.zero;
            bot.Blackboard.ReactionTimer = 0f;
            var ctx = CreateContext();

            BotBrainSystem.Tick(state, in ctx);

            Assert.IsTrue(bot.DesiredVelocity.z < 0f, "Bot should move toward player (negative Z)");
        }

        [Test]
        public void Tick_DeadBot_Skipped()
        {
            var state = CreateStateWithBot("Scav", Vector3.zero);
            state.HealthMap[state.Bots[0].Id].IsAlive = false;
            var ctx = CreateContext();

            BotBrainSystem.Tick(state, in ctx);

            Assert.AreEqual(Vector3.zero, state.Bots[0].DesiredVelocity);
            Assert.IsFalse(state.Bots[0].WantsToFire);
        }

        [Test]
        public void Tick_PMC_CanHeal()
        {
            var state = CreateStateWithBot("PMC", new Vector3(0, 0, 10f));
            var bot = state.Bots[0];
            bot.Blackboard.HasTarget = true;
            bot.Blackboard.CanSeeTarget = true;
            bot.Blackboard.DistanceToTarget = 10f;
            bot.Blackboard.LastKnownTargetPos = Vector3.zero;
            state.HealthMap[bot.Id].CurrentHp = 10f;
            var ctx = CreateContext();

            BotBrainSystem.Tick(state, in ctx);

            Assert.IsTrue(bot.WantsToHeal, "PMC should want to heal when HP is low");
        }

        [Test]
        public void Tick_Scav_CannotHeal()
        {
            var state = CreateStateWithBot("Scav", new Vector3(0, 0, 10f));
            var bot = state.Bots[0];
            state.HealthMap[bot.Id].CurrentHp = 10f;
            var ctx = CreateContext();

            BotBrainSystem.Tick(state, in ctx);

            Assert.IsFalse(bot.WantsToHeal, "Scav should not be able to heal");
        }
    }
}
