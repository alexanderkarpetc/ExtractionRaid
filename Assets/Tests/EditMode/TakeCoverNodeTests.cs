using NUnit.Framework;
using Session;
using State;
using Systems.Bot;
using Tests.EditMode.Fakes;
using UnityEngine;

namespace Tests.EditMode
{
    /// <summary>
    /// TakeCoverNode is exercised through BotBrainSystem.Tick on a RangedTarget
    /// (Chase | Shoot | TakeCover) so the selector fall-through contract is tested,
    /// not just the node in isolation. Line-of-sight geometry is simulated with a
    /// single wall segment on the XZ plane via FakePhysicsAdapter.LinecastFunc.
    /// </summary>
    [TestFixture]
    public class TakeCoverNodeTests
    {
        // Wall from (-0.8, z=5) to (0.8, z=5). A linecast is blocked when its XZ
        // projection crosses the segment — enough geometry to give the cover search
        // a real shadow (points behind the wall) and clear peek angles beside it.
        static bool WallBlocks(Vector3 from, Vector3 to)
        {
            return SegmentsIntersect(
                new Vector2(from.x, from.z), new Vector2(to.x, to.z),
                new Vector2(-0.8f, 5f), new Vector2(0.8f, 5f));
        }

        static bool SegmentsIntersect(Vector2 p1, Vector2 p2, Vector2 q1, Vector2 q2)
        {
            float d1 = Cross(q2 - q1, p1 - q1);
            float d2 = Cross(q2 - q1, p2 - q1);
            float d3 = Cross(p2 - p1, q1 - p1);
            float d4 = Cross(p2 - p1, q2 - p1);
            return d1 * d2 < 0f && d3 * d4 < 0f;
        }

        static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;

        /// <summary>Player at origin, alert RangedTarget with eyes on it at botPos.</summary>
        static (RaidState state, BotEntityState bot) CreateEngagedBot(Vector3 botPos)
        {
            var state = EditModeTestsUtils.CreateStateWithPlayer(Vector3.zero);
            BotSpawnSystem.SpawnBot(state, "RangedTarget", botPos, new[] { botPos }, new FakeRaidEvents());
            var bot = state.Bots[0];
            var bb = bot.Blackboard;
            bb.HasTarget = true;
            bb.CanSeeTarget = true;
            bb.DistanceToTarget = botPos.magnitude;
            bb.LastKnownTargetPos = Vector3.zero;
            bb.TimeSinceTargetSeen = 0f;
            bb.ReactionTimer = 999f; // alert on the first brain tick
            return (state, bot);
        }

        [Test]
        public void Tick_CoverAvailable_BotPicksPointAndMovesToIt()
        {
            var (state, bot) = CreateEngagedBot(new Vector3(0f, 0f, 8f));
            var ctx = TestContextFactory.Create(
                physics: new FakePhysicsAdapter { LinecastFunc = WallBlocks });

            BotBrainSystem.Tick(state, in ctx);

            var bb = bot.Blackboard;
            Assert.AreEqual(CoverPhase.MoveTo, bb.CoverPhase, "Bot should be running to cover");
            Assert.AreNotEqual(Vector3.zero, bot.DesiredVelocity);
            Assert.IsTrue(
                WallBlocks(Vector3.zero + Vector3.up * 1f, bb.CoverPoint + Vector3.up * 1.1f),
                "Picked point must actually be hidden from the enemy eye");
            Assert.IsFalse(
                WallBlocks(Vector3.zero + Vector3.up * 1f, bb.CoverPeekPos + Vector3.up * 1.1f),
                "Peek spot must have a clear line back to the enemy");
        }

        [Test]
        public void Tick_NoCoverAnywhere_FallsThroughToShoot()
        {
            var (state, bot) = CreateEngagedBot(new Vector3(0f, 0f, 8f));
            var ctx = TestContextFactory.Create(
                physics: new FakePhysicsAdapter { Blocked = false }); // open field

            BotBrainSystem.Tick(state, in ctx);

            Assert.AreEqual(CoverPhase.None, bot.Blackboard.CoverPhase);
            Assert.IsTrue(bot.WantsToFire, "No cover → selector falls through to ShootNode");
        }

        [Test]
        public void Tick_Exposed_YieldsTriggerToShootNode()
        {
            var (state, bot) = CreateEngagedBot(new Vector3(1.6f, 0f, 8f));
            var bb = bot.Blackboard;
            state.ElapsedTime = 1f;
            bb.CoverPhase = CoverPhase.Exposed;
            bb.CoverPoint = new Vector3(0f, 0f, 8f);
            bb.CoverPeekPos = bot.Position;
            bb.CoverEnemyAnchor = bb.LastKnownTargetPos;
            bb.CoverPhaseStartTime = 0.5f;
            bb.CoverPhaseEndTime = 10f;             // exposure window still open
            bb.CoverRevalidateTimer = 999f;         // skip the periodic re-check
            var ctx = TestContextFactory.Create(
                physics: new FakePhysicsAdapter { LinecastFunc = WallBlocks });

            BotBrainSystem.Tick(state, in ctx);

            Assert.AreEqual(CoverPhase.Exposed, bot.Blackboard.CoverPhase);
            Assert.IsTrue(bot.WantsToFire, "Exposed → TakeCover yields and ShootNode fires");
        }

        [Test]
        public void Tick_ExposureWindowElapsed_DucksBackToCover()
        {
            var (state, bot) = CreateEngagedBot(new Vector3(1.6f, 0f, 8f));
            var bb = bot.Blackboard;
            state.ElapsedTime = 1f;
            bb.CoverPhase = CoverPhase.Exposed;
            bb.CoverPoint = new Vector3(0f, 0f, 8f);
            bb.CoverPeekPos = bot.Position;
            bb.CoverEnemyAnchor = bb.LastKnownTargetPos;
            bb.CoverPhaseStartTime = 0.2f;
            bb.CoverPhaseEndTime = 0.5f;            // window already spent
            bb.CoverRevalidateTimer = 999f;
            var ctx = TestContextFactory.Create(
                physics: new FakePhysicsAdapter { LinecastFunc = WallBlocks });

            BotBrainSystem.Tick(state, in ctx);

            Assert.AreEqual(CoverPhase.Return, bot.Blackboard.CoverPhase);
            Assert.IsFalse(bot.WantsToFire, "Ducking back — trigger stays off");
        }

        [Test]
        public void Tick_ShotWhileHidden_BlacklistsSpotAndDropsCover()
        {
            var (state, bot) = CreateEngagedBot(new Vector3(0f, 0f, 8f));
            var bb = bot.Blackboard;
            state.ElapsedTime = 2f;
            bb.CoverPhase = CoverPhase.Hold;
            bb.CoverPoint = bot.Position;
            bb.CoverPeekPos = bot.Position + Vector3.right * 1.6f;
            bb.CoverEnemyAnchor = bb.LastKnownTargetPos;
            bb.CoverPhaseStartTime = 1f;
            bb.CoverPhaseEndTime = 10f;
            bb.CoverRevalidateTimer = 999f;
            bb.LastDamageTime = 1.5f;               // hit after the hold began
            var coverPoint = bb.CoverPoint;
            var ctx = TestContextFactory.Create(
                physics: new FakePhysicsAdapter { LinecastFunc = WallBlocks });

            BotBrainSystem.Tick(state, in ctx);

            Assert.AreEqual(CoverPhase.None, bb.CoverPhase, "Compromised cover is dropped");
            Assert.Greater(bb.CoverBlacklistUntil, state.ElapsedTime);
            Assert.AreEqual(coverPoint, bb.CoverBlacklistPos);
        }
    }
}
