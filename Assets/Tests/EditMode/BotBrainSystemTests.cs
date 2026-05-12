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
            var ctx = TestContextFactory.Create();

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
            var ctx = TestContextFactory.Create();

            BotBrainSystem.Tick(state, in ctx);

            Assert.IsTrue(bot.WantsToFire, "Bot should want to fire when target visible and reacted");
        }

        [Test]
        public void Tick_WithTarget_BotChases()
        {
            var state = CreateStateWithBot("Scav", new Vector3(0, 0, 40f));
            var bot = state.Bots[0];
            bot.Blackboard.HasTarget = true;
            bot.Blackboard.CanSeeTarget = true;
            bot.Blackboard.DistanceToTarget = 40f;
            bot.Blackboard.LastKnownTargetPos = Vector3.zero;
            bot.Blackboard.ReactionTimer = 0f;
            var ctx = TestContextFactory.Create();

            BotBrainSystem.Tick(state, in ctx);

            Assert.IsTrue(bot.DesiredVelocity.z < 0f, "Bot should move toward player (negative Z)");
        }

        [Test]
        public void Tick_DeadBot_Skipped()
        {
            var state = CreateStateWithBot("Scav", Vector3.zero);
            state.HealthMap[state.Bots[0].Id].IsAlive = false;
            var ctx = TestContextFactory.Create();

            BotBrainSystem.Tick(state, in ctx);

            Assert.AreEqual(Vector3.zero, state.Bots[0].DesiredVelocity);
            Assert.IsFalse(state.Bots[0].WantsToFire);
        }

        [Test]
        public void Tick_PMC_CanHeal_WhenSafe()
        {
            var state = CreateStateWithBot("PMC", new Vector3(0, 0, 10f));
            var bot = state.Bots[0];
            bot.Blackboard.HasTarget = true;
            bot.Blackboard.CanSeeTarget = false;
            bot.Blackboard.DistanceToTarget = 15f;
            bot.Blackboard.LastKnownTargetPos = Vector3.zero;
            state.HealthMap[bot.Id].CurrentHp = 10f;
            state.ElapsedTime = 5f;
            var ctx = TestContextFactory.Create();

            BotBrainSystem.Tick(state, in ctx);

            Assert.IsTrue(bot.WantsToHeal, "PMC should want to heal when safe");
        }

        // (Scav_CannotHeal covered by BotHealTests.Scav_CannotHeal)

        // Scav default EngageRange = 20m. Tests pick distances that exercise gate without
        // tripping per-type EngageRange — gate effect is isolated.

        [Test]
        public void Tick_EngagementGate_OutsideRadius_BotDoesNotFire()
        {
            // Bot inside per-type EngageRange (20) but outside global player-screen radius (15) → no fire.
            var state = CreateStateWithBot("Scav", new Vector3(0, 0, 18f));
            var bot = state.Bots[0];
            bot.FacingDirection = -Vector3.forward;
            bot.Blackboard.HasTarget = true;
            bot.Blackboard.CanSeeTarget = true;
            bot.Blackboard.DistanceToTarget = 18f;
            bot.Blackboard.LastKnownTargetPos = Vector3.zero;
            bot.Blackboard.ReactionTimer = 999f;
            var ctx = TestContextFactory.Create(botEngagementConfig: new BotEngagementConfig
            {
                Enabled = true,
                MaxEngagementRadius = 15f,
            });

            BotBrainSystem.Tick(state, in ctx);

            Assert.IsFalse(bot.WantsToFire, "Bot outside gate radius should not fire even з visible target");
        }

        [Test]
        public void Tick_EngagementGate_InsideRadius_BotFires()
        {
            // Bot inside global radius → gate doesn't trigger, fires normally.
            var state = CreateStateWithBot("Scav", new Vector3(0, 0, 10f));
            var bot = state.Bots[0];
            bot.FacingDirection = -Vector3.forward;
            bot.Blackboard.HasTarget = true;
            bot.Blackboard.CanSeeTarget = true;
            bot.Blackboard.DistanceToTarget = 10f;
            bot.Blackboard.LastKnownTargetPos = Vector3.zero;
            bot.Blackboard.ReactionTimer = 999f;
            var ctx = TestContextFactory.Create(botEngagementConfig: new BotEngagementConfig
            {
                Enabled = true,
                MaxEngagementRadius = 15f,
            });

            BotBrainSystem.Tick(state, in ctx);

            Assert.IsTrue(bot.WantsToFire, "Bot inside gate radius should fire normally");
        }

        [Test]
        public void Tick_EngagementGate_Disabled_FiresAtAnyDistanceWithinEngageRange()
        {
            // Gate disabled — fall through to per-type EngageRange only. Bot at 18m (within Scav's 20m).
            var state = CreateStateWithBot("Scav", new Vector3(0, 0, 18f));
            var bot = state.Bots[0];
            bot.FacingDirection = -Vector3.forward;
            bot.Blackboard.HasTarget = true;
            bot.Blackboard.CanSeeTarget = true;
            bot.Blackboard.DistanceToTarget = 18f;
            bot.Blackboard.LastKnownTargetPos = Vector3.zero;
            bot.Blackboard.ReactionTimer = 999f;
            var ctx = TestContextFactory.Create(botEngagementConfig: new BotEngagementConfig
            {
                Enabled = false,
                MaxEngagementRadius = 15f, // irrelevant — gate off
            });

            BotBrainSystem.Tick(state, in ctx);

            Assert.IsTrue(bot.WantsToFire, "Gate disabled = per-type EngageRange is the only check");
        }
    }
}
