using System.Linq;
using Adapters;
using Systems;
using NUnit.Framework;
using Session;
using State;
using Tests.EditMode.Fakes;
using UnityEngine;

namespace Tests.EditMode
{
    [TestFixture]
    public class ProjectileSystemTests
    {
        static RaidContext CreateContext(float deltaTime = 1f / 60f, IRaidEvents events = null)
        {
            return new RaidContext(
                deltaTime: deltaTime,
                events: events ?? new RaidEventBuffer(),
                time: new FakeTimeAdapter { DeltaTime = deltaTime },
                input: new FakeInputAdapter(),
                navMesh: new FakeNavMeshAdapter()
            );
        }

        static ProjectileEntityState CreateProjectile(
            RaidState state, Vector3 position, Vector3 direction,
            float speed = 20f, float spawnTime = 0f, float lifetime = 3f)
        {
            var id = state.AllocateEId();
            var proj = ProjectileEntityState.Create(id, position, direction, speed, spawnTime, lifetime);
            state.Projectiles.Add(proj);
            return proj;
        }

        [Test]
        public void Tick_MovesProjectileAlongDirection()
        {
            var state = RaidState.Create();
            CreateProjectile(state, Vector3.zero, Vector3.forward, speed: 10f);
            var context = CreateContext(deltaTime: 1f);

            ProjectileSystem.Tick(state, in context);

            Assert.AreEqual(10f, state.Projectiles[0].Position.z, 0.001f);
        }

        [Test]
        public void Tick_RespectsSpeed()
        {
            var state = RaidState.Create();
            CreateProjectile(state, Vector3.zero, Vector3.forward, speed: 5f);
            CreateProjectile(state, Vector3.zero, Vector3.forward, speed: 15f);
            var context = CreateContext(deltaTime: 1f);

            ProjectileSystem.Tick(state, in context);

            Assert.AreEqual(5f, state.Projectiles[0].Position.z, 0.001f);
            Assert.AreEqual(15f, state.Projectiles[1].Position.z, 0.001f);
        }

        [Test]
        public void Tick_RespectsDeltaTime()
        {
            var state = RaidState.Create();
            CreateProjectile(state, Vector3.zero, Vector3.forward, speed: 10f);
            var context = CreateContext(deltaTime: 0.5f);

            ProjectileSystem.Tick(state, in context);

            Assert.AreEqual(5f, state.Projectiles[0].Position.z, 0.001f);
        }

        [Test]
        public void Tick_RemovesProjectileAfterLifetime()
        {
            var state = RaidState.Create();
            state.ElapsedTime = 4f;
            CreateProjectile(state, Vector3.zero, Vector3.forward, spawnTime: 0f, lifetime: 3f);
            var context = CreateContext();

            ProjectileSystem.Tick(state, in context);

            Assert.AreEqual(0, state.Projectiles.Count);
        }

        [Test]
        public void Tick_EmitsDespawnEventForExpiredProjectile()
        {
            var state = RaidState.Create();
            state.ElapsedTime = 4f;
            var proj = CreateProjectile(state, Vector3.zero, Vector3.forward, spawnTime: 0f, lifetime: 3f);
            var eventBuffer = new RaidEventBuffer();
            var context = CreateContext(events: eventBuffer);

            ProjectileSystem.Tick(state, in context);

            var despawned = eventBuffer.All.Where(e => e.Type == RaidEventType.ProjectileDespawned).ToList();
            Assert.AreEqual(1, despawned.Count);
            Assert.AreEqual(proj.Id, despawned[0].Id);
        }

        [Test]
        public void Tick_MultipleProjectiles_AllMove()
        {
            var state = RaidState.Create();
            CreateProjectile(state, Vector3.zero, Vector3.forward, speed: 10f);
            CreateProjectile(state, Vector3.zero, Vector3.right, speed: 10f);
            CreateProjectile(state, Vector3.zero, Vector3.left, speed: 10f);
            var context = CreateContext(deltaTime: 1f);

            ProjectileSystem.Tick(state, in context);

            Assert.AreEqual(3, state.Projectiles.Count);
            Assert.AreEqual(10f, state.Projectiles[0].Position.z, 0.001f);
            Assert.AreEqual(10f, state.Projectiles[1].Position.x, 0.001f);
            Assert.AreEqual(-10f, state.Projectiles[2].Position.x, 0.001f);
        }

        [Test]
        public void Tick_EmptyList_DoesNotThrow()
        {
            var state = RaidState.Create();
            var context = CreateContext();

            Assert.DoesNotThrow(() => ProjectileSystem.Tick(state, in context));
        }

        [Test]
        public void Tick_OnlyRemovesExpiredProjectiles()
        {
            var state = RaidState.Create();
            state.ElapsedTime = 4f;
            CreateProjectile(state, Vector3.zero, Vector3.forward, spawnTime: 0f, lifetime: 3f);
            var alive = CreateProjectile(state, Vector3.zero, Vector3.right, spawnTime: 3f, lifetime: 3f);
            var context = CreateContext();

            ProjectileSystem.Tick(state, in context);

            Assert.AreEqual(1, state.Projectiles.Count);
            Assert.AreEqual(alive.Id, state.Projectiles[0].Id);
        }

        [Test]
        public void Tick_ReverseIterationDoesNotSkipProjectiles()
        {
            var state = RaidState.Create();
            state.ElapsedTime = 5f;
            CreateProjectile(state, Vector3.zero, Vector3.forward, spawnTime: 0f, lifetime: 1f);
            var alive = CreateProjectile(state, Vector3.zero, Vector3.right, spawnTime: 4f, lifetime: 3f);
            CreateProjectile(state, Vector3.zero, Vector3.left, spawnTime: 0f, lifetime: 2f);
            var context = CreateContext(deltaTime: 1f);

            ProjectileSystem.Tick(state, in context);

            Assert.AreEqual(1, state.Projectiles.Count);
            Assert.AreEqual(alive.Id, state.Projectiles[0].Id);
        }
    }
}
