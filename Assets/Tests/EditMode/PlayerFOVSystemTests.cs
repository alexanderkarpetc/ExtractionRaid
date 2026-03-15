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
        [SetUp]
        public void SetUp()
        {
            DevCheats.Reset();
            DevCheats.FOVEnabled = true;
            DevCheats.ForceShowAllBots = false;
            DevCheats.FOVNearRadius = 5f;
            DevCheats.FOVFarRadius = 30f;
            DevCheats.FOVAngle = 120f;
        }

        [TearDown]
        public void TearDown()
        {
            DevCheats.Reset();
        }

        static RaidState CreateStateWithBot(Vector3 playerPos, Vector3 playerFacing, Vector3 botPos)
        {
            var state = EditModeTestsUtils.CreateStateWithPlayer(playerPos);
            state.PlayerEntity.FacingDirection = playerFacing;

            var events = new FakeRaidEvents();
            BotSpawnSystem.SpawnBot(state, "Scav", botPos, new[] { botPos }, events);
            return state;
        }

        [Test]
        public void BotInNearRadius_IsVisible()
        {
            // Bot is 3m away (< NearRadius 5m), behind the player
            var state = CreateStateWithBot(Vector3.zero, Vector3.forward, new Vector3(0, 0, -3f));

            PlayerFOVSystem.Tick(state);

            Assert.IsTrue(state.Bots[0].IsVisibleToPlayer);
        }

        [Test]
        public void BotInSectorAngle_IsVisible()
        {
            // Bot is 15m ahead (within FarRadius 30m, within 120° cone)
            var state = CreateStateWithBot(Vector3.zero, Vector3.forward, new Vector3(0, 0, 15f));

            PlayerFOVSystem.Tick(state);

            Assert.IsTrue(state.Bots[0].IsVisibleToPlayer);
        }

        [Test]
        public void BotOutsideSector_NotVisible()
        {
            // Bot is 15m directly behind (angle = 180° > halfAngle 60°)
            var state = CreateStateWithBot(Vector3.zero, Vector3.forward, new Vector3(0, 0, -15f));

            PlayerFOVSystem.Tick(state);

            Assert.IsFalse(state.Bots[0].IsVisibleToPlayer);
        }

        [Test]
        public void BotBeyondFarRadius_NotVisible()
        {
            // Bot is 50m ahead (> FarRadius 30m)
            var state = CreateStateWithBot(Vector3.zero, Vector3.forward, new Vector3(0, 0, 50f));

            PlayerFOVSystem.Tick(state);

            Assert.IsFalse(state.Bots[0].IsVisibleToPlayer);
        }

        [Test]
        public void BotBehindPlayer_InNearRadius_StillVisible()
        {
            // Bot is 4m behind (< NearRadius 5m) — near sphere is 360°
            var state = CreateStateWithBot(Vector3.zero, Vector3.forward, new Vector3(0, 0, -4f));

            PlayerFOVSystem.Tick(state);

            Assert.IsTrue(state.Bots[0].IsVisibleToPlayer);
        }

        [Test]
        public void FOVDisabled_AllBotsVisible()
        {
            DevCheats.FOVEnabled = false;

            // Bot is far behind — would normally be invisible
            var state = CreateStateWithBot(Vector3.zero, Vector3.forward, new Vector3(0, 0, -50f));

            PlayerFOVSystem.Tick(state);

            Assert.IsTrue(state.Bots[0].IsVisibleToPlayer);
        }

        [Test]
        public void ForceShowAllBots_AllVisible()
        {
            DevCheats.ForceShowAllBots = true;

            // Bot is far behind — would normally be invisible
            var state = CreateStateWithBot(Vector3.zero, Vector3.forward, new Vector3(0, 0, -50f));

            PlayerFOVSystem.Tick(state);

            Assert.IsTrue(state.Bots[0].IsVisibleToPlayer);
        }

        [Test]
        public void BotAtSectorEdge_IsVisible()
        {
            // Bot at 59° angle (just inside halfAngle 60°), 20m distance — should be visible
            float angle = 59f * Mathf.Deg2Rad;
            var botPos = new Vector3(Mathf.Sin(angle) * 20f, 0f, Mathf.Cos(angle) * 20f);

            var state = CreateStateWithBot(Vector3.zero, Vector3.forward, botPos);

            PlayerFOVSystem.Tick(state);

            Assert.IsTrue(state.Bots[0].IsVisibleToPlayer);
        }

        [Test]
        public void BotJustOutsideSectorEdge_NotVisible()
        {
            // Bot at 65° angle (> halfAngle 60°), 20m distance — should be invisible
            float angle = 65f * Mathf.Deg2Rad;
            var botPos = new Vector3(Mathf.Sin(angle) * 20f, 0f, Mathf.Cos(angle) * 20f);

            var state = CreateStateWithBot(Vector3.zero, Vector3.forward, botPos);

            PlayerFOVSystem.Tick(state);

            Assert.IsFalse(state.Bots[0].IsVisibleToPlayer);
        }
    }
}
