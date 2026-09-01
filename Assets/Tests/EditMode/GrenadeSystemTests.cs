using Adapters;
using NUnit.Framework;
using Session;
using State;
using Systems;
using Tests.EditMode.Fakes;
using UnityEngine;

namespace Tests.EditMode
{
    [TestFixture]
    public class GrenadeSystemTests
    {
        [TestCase(true, 100f)]
        [TestCase(false, 20f)]
        public void TickExplosions_PlayerDamage_RespectsGodMode(bool godMode, float expectedHp)
        {
            int nextId = 10;
            var state = RaidState.Create(() => new EId(nextId++));
            state.ElapsedTime = 2f;

            var playerId = new EId(1);
            state.PlayerEntity = PlayerEntityState.Create(playerId, Vector3.zero);
            state.HealthMap[playerId] = HealthState.Create(100f);
            state.Grenades.Add(GrenadeEntityState.Create(
                new EId(2), new EId(3), spawnTime: 0f, fuseTime: 1f,
                damage: 80f, explosionRadius: 5f));

            var cheats = CheatsConfig.Default;
            cheats.GodMode = godMode;
            var events = new FakeRaidEvents();
            var context = new RaidContext(
                deltaTime: 1f / 60f,
                events: events,
                time: new FakeTimeAdapter(),
                input: new FakeInputAdapter(),
                navMesh: new FakeNavMeshAdapter(),
                grenadePositions: new FixedGrenadePositionAdapter(Vector3.zero),
                cheatsConfig: cheats);

            GrenadeSystem.TickExplosions(state, in context);

            Assert.AreEqual(expectedHp, state.HealthMap[playerId].CurrentHp, 0.001f);
            Assert.IsTrue(state.HealthMap[playerId].IsAlive);
            Assert.IsTrue(events.EntityDamagedCalled,
                "God Mode should preserve the normal feedback event pipeline.");
            Assert.IsTrue(events.GrenadeExplodedCalled);
            Assert.AreEqual(0, state.Grenades.Count);
        }

        sealed class FixedGrenadePositionAdapter : IGrenadePositionAdapter
        {
            readonly Vector3 _position;

            public FixedGrenadePositionAdapter(Vector3 position) => _position = position;

            public Vector3? GetPosition(EId id) => _position;
        }
    }
}
