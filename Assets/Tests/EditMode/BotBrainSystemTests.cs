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
            bot.Blackboard.ReactionTimer = 999f;
            var ctx = TestContextFactory.Create();

            BotBrainSystem.Tick(state, in ctx);

            Assert.IsTrue(bot.DesiredVelocity.z < 0f, "Bot should move toward player (negative Z)");
        }

        [Test]
        public void Tick_ReactionWindowNotElapsed_NoCombatResponse()
        {
            // Reaction gates the whole chain — no chasing, no firing, until it elapses.
            var state = CreateStateWithBot("Scav", new Vector3(0, 0, 10f));
            var bot = state.Bots[0];
            bot.FacingDirection = -Vector3.forward;
            bot.Blackboard.HasTarget = true;
            bot.Blackboard.CanSeeTarget = true;
            bot.Blackboard.DistanceToTarget = 10f;
            bot.Blackboard.LastKnownTargetPos = Vector3.zero;
            bot.Blackboard.ReactionTimer = 0f;
            var ctx = TestContextFactory.Create();

            BotBrainSystem.Tick(state, in ctx);

            Assert.IsFalse(bot.WantsToFire, "Bot should not fire before reacting");
            Assert.IsFalse(bot.Blackboard.IsAlert);
        }

        [Test]
        public void Tick_ReactionAccumulates_ThenBotEngages()
        {
            var state = CreateStateWithBot("Scav", new Vector3(0, 0, 10f));
            var bot = state.Bots[0];
            bot.FacingDirection = -Vector3.forward;
            bot.Blackboard.HasTarget = true;
            bot.Blackboard.CanSeeTarget = true;
            bot.Blackboard.DistanceToTarget = 10f;
            bot.Blackboard.LastKnownTargetPos = Vector3.zero;
            bot.Blackboard.ReactionTimeMult = 1f; // pin the spawn-rolled personality
            bot.Blackboard.ReactionJitter = 0f;
            var ctx = TestContextFactory.Create(deltaTime: 0.5f);

            BotBrainSystem.Tick(state, in ctx); // 0.5 s < 0.8 s Scav reaction
            Assert.IsFalse(bot.WantsToFire);

            BotBrainSystem.Tick(state, in ctx); // 1.0 s ≥ 0.8 s
            Assert.IsTrue(bot.Blackboard.IsAlert, "Reaction window elapsed → alert");
            Assert.IsTrue(bot.WantsToFire);
        }

        [Test]
        public void Tick_ArrivedAtLastKnownPos_SearchesThenGivesUp()
        {
            // Target unseen and bot standing on the last known position → search scan,
            // then forget the target instead of freezing until memory expiry.
            var state = CreateStateWithBot("Scav", new Vector3(0, 0, 10f));
            var bot = state.Bots[0];
            bot.Blackboard.HasTarget = true;
            bot.Blackboard.CanSeeTarget = false;
            bot.Blackboard.LastKnownTargetPos = bot.Position;
            bot.Blackboard.ReactionTimer = 999f;
            bot.Blackboard.TimeSinceTargetSeen = 1f;
            var ctx = TestContextFactory.Create();

            BotBrainSystem.Tick(state, in ctx);
            Assert.AreEqual("Search", bot.Blackboard.DebugStatus);
            Assert.GreaterOrEqual(bot.Blackboard.SearchEndTime, 0f);

            state.ElapsedTime = bot.Blackboard.SearchEndTime + 0.1f;
            BotBrainSystem.Tick(state, in ctx);

            Assert.IsFalse(bot.Blackboard.HasTarget, "Bot should give up and forget the target");
        }

        [Test]
        public void Tick_TargetMemoryExpires_SearchStillRunsForItsFullWindow()
        {
            var state = CreateStateWithBot("Scav", new Vector3(0, 0, 10f));
            var bot = state.Bots[0];
            var bb = bot.Blackboard;
            bb.HasTarget = true;
            bb.CanSeeTarget = false;
            bb.LastKnownTargetPos = Vector3.zero;
            bb.TimeSinceTargetSeen = 4.9f;
            bb.ReactionTimer = 999f;
            var physics = new FakePhysicsAdapter { Blocked = true };
            var ctx = TestContextFactory.Create(physics: physics);

            BotPerceptionSystem.Tick(state, in ctx);
            BotBrainSystem.Tick(state, in ctx);

            Assert.IsTrue(bb.HasTarget, "Memory expiry should enter search, not forget immediately");
            Assert.AreEqual("Search", bb.DebugStatus);
            float searchEnd = bb.SearchEndTime;

            state.ElapsedTime = searchEnd - 0.1f;
            bb.PerceptionTimer = 0f;
            BotPerceptionSystem.Tick(state, in ctx);
            BotBrainSystem.Tick(state, in ctx);

            Assert.IsTrue(bb.HasTarget, "Perception must not interrupt an active search");
            Assert.AreEqual("Search", bb.DebugStatus);

            state.ElapsedTime = searchEnd + 0.1f;
            BotBrainSystem.Tick(state, in ctx);

            Assert.IsFalse(bb.HasTarget);
        }

        [Test]
        public void Tick_SearchEnds_ResumesAtNearestPatrolWaypoint()
        {
            var waypoints = new[] { Vector3.zero, new Vector3(0f, 0f, 12f) };
            var state = CreateStateWithBot("Scav", new Vector3(0f, 0f, 10f), waypoints);
            var bot = state.Bots[0];
            var bb = bot.Blackboard;
            bb.HasTarget = true;
            bb.CanSeeTarget = false;
            bb.LastKnownTargetPos = bot.Position;
            bb.ReactionTimer = 999f;
            var ctx = TestContextFactory.Create();

            BotBrainSystem.Tick(state, in ctx);
            state.ElapsedTime = bb.SearchEndTime + 0.1f;
            BotBrainSystem.Tick(state, in ctx);

            Assert.AreEqual(1, bb.PatrolWaypointIndex);
            Assert.Greater(bot.DesiredVelocity.z, 0f,
                "Bot should resume toward the nearby patrol point, not the stale first point");
        }

        [Test]
        public void Tick_SearchEnds_SingleSpawnFallbackBecomesLocalGuardPoint()
        {
            var waypoints = new[] { Vector3.zero };
            var state = CreateStateWithBot("Scav", new Vector3(0f, 0f, 10f), waypoints);
            var bot = state.Bots[0];
            var bb = bot.Blackboard;
            bb.HasTarget = true;
            bb.CanSeeTarget = false;
            bb.LastKnownTargetPos = bot.Position;
            bb.ReactionTimer = 999f;
            var ctx = TestContextFactory.Create();

            BotBrainSystem.Tick(state, in ctx);
            state.ElapsedTime = bb.SearchEndTime + 0.1f;
            BotBrainSystem.Tick(state, in ctx);

            Assert.AreEqual(bot.Position, bb.PatrolWaypoints[0]);
            Assert.AreEqual(Vector3.zero, bot.DesiredVelocity,
                "A single spawn fallback must not pull the bot back across the map");
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

        [Test]
        public void Tick_EngagementGate_OutsideViewport_BotApproachesWithoutFiring()
        {
            var state = CreateStateWithBot("Scav", new Vector3(0, 0, 10f));
            var bot = state.Bots[0];
            bot.FacingDirection = -Vector3.forward;
            bot.Blackboard.HasTarget = true;
            bot.Blackboard.CanSeeTarget = true;
            bot.Blackboard.DistanceToTarget = 10f;
            bot.Blackboard.LastKnownTargetPos = Vector3.zero;
            bot.Blackboard.ReactionTimer = 999f;
            var viewport = new FakeCombatViewportAdapter
            {
                IsInsideHandler = (_, _) => false,
            };
            var ctx = TestContextFactory.Create(botEngagementConfig: new BotEngagementConfig
            {
                Enabled = true,
                ViewportEnterMargin = 0.12f,
                ViewportExitMargin = 0.05f,
            }, combatViewport: viewport);

            BotBrainSystem.Tick(state, in ctx);

            Assert.IsFalse(bot.WantsToFire);
            Assert.Less(bot.DesiredVelocity.z, 0f, "Off-screen bot should approach the player");
            Assert.AreEqual("Enter view", bot.Blackboard.DebugStatus);
        }

        [Test]
        public void Tick_EngagementGate_InsideViewport_BotFires()
        {
            var state = CreateStateWithBot("Scav", new Vector3(0, 0, 10f));
            var bot = state.Bots[0];
            bot.FacingDirection = -Vector3.forward;
            bot.Blackboard.HasTarget = true;
            bot.Blackboard.CanSeeTarget = true;
            bot.Blackboard.DistanceToTarget = 10f;
            bot.Blackboard.LastKnownTargetPos = Vector3.zero;
            bot.Blackboard.ReactionTimer = 999f;
            var viewport = new FakeCombatViewportAdapter();
            var ctx = TestContextFactory.Create(botEngagementConfig: new BotEngagementConfig
            {
                Enabled = true,
                ViewportEnterMargin = 0.12f,
                ViewportExitMargin = 0.05f,
            }, combatViewport: viewport);

            BotBrainSystem.Tick(state, in ctx);

            Assert.IsTrue(bot.WantsToFire);
            Assert.IsTrue(bot.Blackboard.IsInsideEngagementView);
        }

        [Test]
        public void Tick_EngagementGate_UsesExitMarginAfterBotEntered()
        {
            var state = CreateStateWithBot("Scav", new Vector3(0, 0, 10f));
            var bot = state.Bots[0];
            bot.FacingDirection = -Vector3.forward;
            bot.Blackboard.HasTarget = true;
            bot.Blackboard.CanSeeTarget = true;
            bot.Blackboard.DistanceToTarget = 10f;
            bot.Blackboard.LastKnownTargetPos = Vector3.zero;
            bot.Blackboard.ReactionTimer = 999f;
            bot.Blackboard.IsInsideEngagementView = true;
            var viewport = new FakeCombatViewportAdapter
            {
                IsInsideHandler = (_, margin) => margin <= 0.05f,
            };
            var ctx = TestContextFactory.Create(botEngagementConfig: new BotEngagementConfig
            {
                Enabled = true,
                ViewportEnterMargin = 0.12f,
                ViewportExitMargin = 0.05f,
            }, combatViewport: viewport);

            BotBrainSystem.Tick(state, in ctx);

            Assert.IsTrue(bot.WantsToFire,
                "Bot already engaged should remain active inside the wider exit zone");
        }

        [Test]
        public void Tick_EngagementGate_OutsideViewport_PreemptsPmcCoverFire()
        {
            var state = CreateStateWithBot("PMC", new Vector3(0, 0, 10f));
            var bot = state.Bots[0];
            bot.FacingDirection = -Vector3.forward;
            bot.Blackboard.HasTarget = true;
            bot.Blackboard.CanSeeTarget = true;
            bot.Blackboard.DistanceToTarget = 10f;
            bot.Blackboard.LastKnownTargetPos = Vector3.zero;
            bot.Blackboard.ReactionTimer = 999f;
            var viewport = new FakeCombatViewportAdapter
            {
                IsInsideHandler = (_, _) => false,
            };
            var ctx = TestContextFactory.Create(botEngagementConfig: new BotEngagementConfig
            {
                Enabled = true,
                ViewportEnterMargin = 0.12f,
                ViewportExitMargin = 0.05f,
            }, combatViewport: viewport);

            BotBrainSystem.Tick(state, in ctx);

            Assert.IsFalse(bot.WantsToFire);
            Assert.AreEqual(CoverPhase.None, bot.Blackboard.CoverPhase);
            Assert.AreEqual("Enter view", bot.Blackboard.DebugStatus);
        }
    }
}
