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
    public class BotCombatSystemTests
    {
        static RaidContext CreateContext(float dt = 1f / 60f, IRaidEvents events = null)
        {
            return new RaidContext(
                deltaTime: dt,
                events: events ?? new RaidEventBuffer(),
                time: new FakeTimeAdapter { DeltaTime = dt },
                input: new FakeInputAdapter(),
                navMesh: new FakeNavMeshAdapter()
            );
        }

        static RaidState CreateStateWithBotWantingToFire(string typeId = "Scav")
        {
            var state = EditModeTestsUtils.CreateStateWithPlayer(Vector3.zero);
            var events = new FakeRaidEvents();
            BotSpawnSystem.SpawnBot(state, typeId, new Vector3(0, 0, 10f),
                new[] { Vector3.zero }, events);

            var bot = state.Bots[0];
            bot.WantsToFire = true;
            bot.DesiredAimPoint = state.PlayerEntity.Position;
            bot.FacingDirection = -Vector3.forward;
            return state;
        }

        [Test]
        public void Tick_BotWantsToFire_SpawnsProjectile()
        {
            var state = CreateStateWithBotWantingToFire();
            var ctx = CreateContext();

            BotCombatSystem.Tick(state, in ctx);

            Assert.GreaterOrEqual(state.Projectiles.Count, 1);
        }

        [Test]
        public void Tick_BotDoesNotWantToFire_NoProjectile()
        {
            var state = CreateStateWithBotWantingToFire();
            state.Bots[0].WantsToFire = false;
            var ctx = CreateContext();

            BotCombatSystem.Tick(state, in ctx);

            Assert.AreEqual(0, state.Projectiles.Count);
        }

        [Test]
        public void Tick_BotWantsToHeal_IncreasesHpAndConsumesMedkit()
        {
            var state = EditModeTestsUtils.CreateStateWithPlayer(Vector3.zero);
            var events = new FakeRaidEvents();
            BotSpawnSystem.SpawnBot(state, "PMC", Vector3.zero, new[] { Vector3.zero }, events);

            var bot = state.Bots[0];
            state.HealthMap[bot.Id].CurrentHp = 50f;
            bot.WantsToHeal = true;
            int medkitsBefore = bot.Blackboard.MedkitsRemaining;
            var ctx = CreateContext();

            BotCombatSystem.Tick(state, in ctx);

            Assert.AreEqual(80f, state.HealthMap[bot.Id].CurrentHp, 0.01f);
            Assert.AreEqual(medkitsBefore - 1, bot.Blackboard.MedkitsRemaining);
        }

        [Test]
        public void Tick_FireRespectsCooldown()
        {
            var state = CreateStateWithBotWantingToFire();
            state.Bots[0].Weapon.LastFireTime = 0f;
            state.ElapsedTime = 0.1f;
            var ctx = CreateContext();

            BotCombatSystem.Tick(state, in ctx);

            Assert.AreEqual(0, state.Projectiles.Count,
                "Should not fire during weapon cooldown");
        }

        [Test]
        public void Tick_BossSpawnsMultiplePellets()
        {
            var state = CreateStateWithBotWantingToFire("Boss");
            var ctx = CreateContext();

            BotCombatSystem.Tick(state, in ctx);

            Assert.AreEqual(7, state.Projectiles.Count);
        }

        [Test]
        public void Tick_EmitsProjectileSpawnedEvents()
        {
            var state = CreateStateWithBotWantingToFire();
            var eventBuffer = new RaidEventBuffer();
            var ctx = CreateContext(events: eventBuffer);

            BotCombatSystem.Tick(state, in ctx);

            int count = 0;
            foreach (var e in eventBuffer.All)
                if (e.Type == RaidEventType.ProjectileSpawned) count++;

            Assert.GreaterOrEqual(count, 1);
        }
    }
}
