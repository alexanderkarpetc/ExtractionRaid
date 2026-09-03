using Constants;
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

        static RaidState CreateStateWithPlayerAndBot(Vector3 playerPos, Vector3 botPos,
            string botType = "Scav")
        {
            var state = EditModeTestsUtils.CreateStateWithPlayer(playerPos);
            var events = new FakeRaidEvents();
            BotSpawnSystem.SpawnBot(state, botType, botPos, new[] { botPos }, events);
            state.Bots[0].Blackboard.PerceptionTimer = 0f;
            return state;
        }

        // Graduated vision means distant targets take several perception ticks to
        // register — run N ticks with the interval gate reset between them.
        static void TickPerception(RaidState state, in RaidContext ctx, int ticks)
        {
            for (int i = 0; i < ticks; i++)
            {
                state.Bots[0].Blackboard.PerceptionTimer = 0f;
                BotPerceptionSystem.Tick(state, in ctx);
            }
        }

        [Test]
        public void Tick_PlayerInVisionRange_DetectsTargetWithinAFewTicks()
        {
            var state = CreateStateWithPlayerAndBot(Vector3.zero, new Vector3(0, 0, 10f));
            state.Bots[0].FacingDirection = -Vector3.forward;
            var ctx = TestContextFactory.Create();

            TickPerception(state, in ctx, 3);

            Assert.IsTrue(state.Bots[0].Blackboard.HasTarget);
            Assert.IsTrue(state.Bots[0].Blackboard.CanSeeTarget);
        }

        [Test]
        public void Tick_CloseTarget_DetectedInstantly()
        {
            // Within VisionInstantFraction of vision range → single-tick detection.
            var state = CreateStateWithPlayerAndBot(Vector3.zero, new Vector3(0, 0, 6f));
            state.Bots[0].FacingDirection = -Vector3.forward;
            var ctx = TestContextFactory.Create();

            BotPerceptionSystem.Tick(state, in ctx);

            Assert.IsTrue(state.Bots[0].Blackboard.CanSeeTarget);
        }

        [Test]
        public void Tick_FarTarget_NotDetectedOnFirstTick()
        {
            // Near max vision range → awareness accumulates over ~1 s, not instantly.
            var state = CreateStateWithPlayerAndBot(Vector3.zero, new Vector3(0, 0, 22f));
            state.Bots[0].FacingDirection = -Vector3.forward;
            var ctx = TestContextFactory.Create();

            BotPerceptionSystem.Tick(state, in ctx);

            Assert.IsFalse(state.Bots[0].Blackboard.HasTarget,
                "Far target should take multiple perception ticks to register");

            TickPerception(state, in ctx, 8);
            Assert.IsTrue(state.Bots[0].Blackboard.HasTarget,
                "Far target should eventually be detected while it stays in the cone");
        }

        [Test]
        public void Tick_PlayerAdjacentBehindBot_SensedByProximity()
        {
            // 360° close-presence sense: someone standing next to the bot is noticed
            // even outside the vision cone.
            var state = CreateStateWithPlayerAndBot(Vector3.zero, new Vector3(0, 0, 2f));
            state.Bots[0].FacingDirection = Vector3.forward; // facing away
            var ctx = TestContextFactory.Create();

            BotPerceptionSystem.Tick(state, in ctx);

            Assert.IsTrue(state.Bots[0].Blackboard.HasTarget);
        }

        [Test]
        public void Tick_PlayerOutOfRange_NoDetection()
        {
            var state = CreateStateWithPlayerAndBot(Vector3.zero, new Vector3(0, 0, 100f));
            state.Bots[0].FacingDirection = -Vector3.forward;
            var ctx = TestContextFactory.Create();

            BotPerceptionSystem.Tick(state, in ctx);

            Assert.IsFalse(state.Bots[0].Blackboard.HasTarget);
        }

        [Test]
        public void Tick_PlayerBehindBot_NotSeen()
        {
            var state = CreateStateWithPlayerAndBot(Vector3.zero, new Vector3(0, 0, 10f));
            state.Bots[0].FacingDirection = Vector3.forward;
            var ctx = TestContextFactory.Create();

            BotPerceptionSystem.Tick(state, in ctx);

            Assert.IsFalse(state.Bots[0].Blackboard.CanSeeTarget);
        }

        [Test]
        public void Tick_WorldGeometryBlocksSight_DoesNotAcquireTarget()
        {
            var state = CreateStateWithPlayerAndBot(Vector3.zero, new Vector3(0, 0, 6f));
            state.Bots[0].FacingDirection = -Vector3.forward;
            var physics = new FakePhysicsAdapter { Blocked = true };
            var ctx = TestContextFactory.Create(physics: physics);

            BotPerceptionSystem.Tick(state, in ctx);

            Assert.AreEqual(1, physics.LineOfSightCallCount);
            Assert.IsFalse(state.Bots[0].Blackboard.HasTarget);
            Assert.IsFalse(state.Bots[0].Blackboard.CanSeeTarget);
        }

        [Test]
        public void Tick_SightBecomesBlocked_StopsFireIntentAndChasesLastKnownPosition()
        {
            var state = CreateStateWithPlayerAndBot(Vector3.zero, new Vector3(0, 0, 6f));
            var bot = state.Bots[0];
            bot.FacingDirection = -Vector3.forward;
            var physics = new FakePhysicsAdapter();
            var ctx = TestContextFactory.Create(physics: physics);

            BotPerceptionSystem.Tick(state, in ctx);
            Assert.IsTrue(bot.Blackboard.CanSeeTarget);

            physics.Blocked = true;
            bot.Blackboard.PerceptionTimer = 0f;
            bot.Blackboard.ReactionTimer = 999f;
            BotPerceptionSystem.Tick(state, in ctx);
            BotBrainSystem.Tick(state, in ctx);

            Assert.IsFalse(bot.Blackboard.CanSeeTarget);
            Assert.IsFalse(bot.WantsToFire);
            Assert.AreEqual("Chase", bot.Blackboard.DebugStatus);
            Assert.AreNotEqual(Vector3.zero, bot.DesiredVelocity);
        }

        [Test]
        public void Tick_PlayerMovingNearby_HeardByBot()
        {
            var state = CreateStateWithPlayerAndBot(Vector3.zero, new Vector3(0, 0, 5f));
            state.Bots[0].FacingDirection = Vector3.forward;
            state.PlayerEntity.Velocity = Vector3.right * 5f;
            var ctx = TestContextFactory.Create();

            BotPerceptionSystem.Tick(state, in ctx);

            Assert.IsTrue(state.Bots[0].Blackboard.HasTarget);
        }

        [Test]
        public void Tick_GunshotHeard_FarBeyondFootstepRange_WithFuzzedPosition()
        {
            // Player fires 30 m away, well outside HearingRange (6) but inside
            // GunshotHearingRange (40). Bot acquires a fuzzy contact, not a GPS pin.
            var state = CreateStateWithPlayerAndBot(Vector3.zero, new Vector3(0, 0, 30f));
            state.Bots[0].FacingDirection = Vector3.forward; // not looking at player
            state.ElapsedTime = 5f;
            state.PlayerEntity.EquippedWeapon = new WeaponEntityState { LastFireTime = 4.9f };
            var ctx = TestContextFactory.Create();

            BotPerceptionSystem.Tick(state, in ctx);

            var bb = state.Bots[0].Blackboard;
            Assert.IsTrue(bb.HasTarget, "Gunshot should alert the bot");
            Assert.IsFalse(bb.CanSeeTarget);
            float posError = (bb.LastKnownTargetPos - state.PlayerEntity.Position).magnitude;
            Assert.LessOrEqual(posError, 30f * BotConstants.GunshotPosErrorFraction + 0.01f,
                "Heard position error should stay within the localization bound");
        }

        [Test]
        public void Tick_SprintingPlayer_HeardFartherThanWalking()
        {
            // 10 m: outside base HearingRange (6) but inside sprint radius (6 * 2.2).
            var state = CreateStateWithPlayerAndBot(Vector3.zero, new Vector3(0, 0, 10f));
            state.Bots[0].FacingDirection = Vector3.forward;
            state.PlayerEntity.Velocity = Vector3.right * 6f;
            state.PlayerEntity.IsSprinting = true;
            var ctx = TestContextFactory.Create();

            BotPerceptionSystem.Tick(state, in ctx);

            Assert.IsTrue(state.Bots[0].Blackboard.HasTarget);
        }

        [Test]
        public void Tick_SneakingPlayer_QuieterThanWalking()
        {
            // 4 m with slow movement: sneak radius = 6 * 0.45 = 2.7 → unheard.
            var state = CreateStateWithPlayerAndBot(Vector3.zero, new Vector3(0, 0, 4f));
            state.Bots[0].FacingDirection = Vector3.forward;
            state.PlayerEntity.Velocity = Vector3.right * 1f;
            var ctx = TestContextFactory.Create();

            BotPerceptionSystem.Tick(state, in ctx);

            Assert.IsFalse(state.Bots[0].Blackboard.HasTarget,
                "Slow movement at 4 m should be under the sneak noise radius");
        }

        [Test]
        public void Tick_TargetLostAfterMemoryExpires()
        {
            var state = CreateStateWithPlayerAndBot(Vector3.zero, new Vector3(0, 0, 10f));
            state.Bots[0].FacingDirection = -Vector3.forward;
            var ctx = TestContextFactory.Create();

            TickPerception(state, in ctx, 3);
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
