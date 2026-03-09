using NUnit.Framework;
using State;
using Systems.Bot;
using Tests.EditMode.Fakes;
using UnityEngine;

namespace Tests.EditMode
{
    [TestFixture]
    public class BotSpawnSystemTests
    {
        [Test]
        public void SpawnBot_CreatesBotInState()
        {
            var state = RaidState.Create();
            var events = new FakeRaidEvents();
            var waypoints = new[] { Vector3.zero, Vector3.one };

            BotSpawnSystem.SpawnBot(state, "Scav", Vector3.zero, waypoints, events);

            Assert.AreEqual(1, state.Bots.Count);
            Assert.IsTrue(state.Bots[0].Id.IsValid);
            Assert.AreEqual("Scav", state.Bots[0].TypeId);
        }

        [Test]
        public void SpawnBot_RegistersHealthInHealthMap()
        {
            var state = RaidState.Create();
            var events = new FakeRaidEvents();

            BotSpawnSystem.SpawnBot(state, "Scav", Vector3.zero, new[] { Vector3.zero }, events);

            var botId = state.Bots[0].Id;
            Assert.IsTrue(state.HealthMap.ContainsKey(botId));
            Assert.AreEqual(80f, state.HealthMap[botId].MaxHp, 0.01f);
        }

        [Test]
        public void SpawnBot_EmitsBotSpawnedEvent()
        {
            var state = RaidState.Create();
            var events = new FakeRaidEvents();

            BotSpawnSystem.SpawnBot(state, "PMC", new Vector3(5f, 0f, 5f), new[] { Vector3.zero }, events);

            Assert.IsTrue(events.BotSpawnedCalled);
            Assert.AreEqual(state.Bots[0].Id, events.BotSpawnedId);
            Assert.AreEqual("PMC", events.BotSpawnedTypeId);
        }

        [Test]
        public void SpawnBot_CreatesWeaponFromConfig()
        {
            var state = RaidState.Create();
            var events = new FakeRaidEvents();

            BotSpawnSystem.SpawnBot(state, "Boss", Vector3.zero, new[] { Vector3.zero }, events);

            var bot = state.Bots[0];
            Assert.IsNotNull(bot.Weapon);
            Assert.AreEqual("Weapon_Shotgun", bot.Weapon.PrefabId);
            Assert.AreEqual(7, bot.Weapon.ProjectilesPerShot);
        }

        [Test]
        public void SpawnBot_SetsPatrolWaypoints()
        {
            var state = RaidState.Create();
            var events = new FakeRaidEvents();
            var waypoints = new[] { new Vector3(1, 0, 0), new Vector3(2, 0, 0), new Vector3(3, 0, 0) };

            BotSpawnSystem.SpawnBot(state, "Scav", Vector3.zero, waypoints, events);

            Assert.AreEqual(3, state.Bots[0].Blackboard.PatrolWaypoints.Length);
        }
    }
}
