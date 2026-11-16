using System.Linq;
using Adapters;
using Systems;
using NUnit.Framework;
using State;
using Tests.EditMode.Fakes;
using UnityEngine;

namespace Tests.EditMode
{
    [TestFixture]
    public class ProjectileSystemTests
    {
        [TestCase(2.5f, 2.5f)]
        [TestCase(0f, 0f)]
        [TestCase(-1f, 0f)]
        [TestCase(float.NaN, 0f)]
        [TestCase(float.PositiveInfinity, 0f)]
        public void ResolveCollision_EmitsResolvedImpulseOnce(float magnitude, float expected)
        {
            var state = RaidState.Create(EditModeTestsUtils.NewAllocator());
            var projectile = CreateProjectile(state, Vector3.zero, Vector3.right * 3f);
            projectile.ImpactImpulse = magnitude;
            projectile.Archetype = PayloadArchetypeKey.Laser;
            var events = new RaidEventBuffer();
            var context = TestContextFactory.Create(events: events);
            var collision = new CollisionSignal
            {
                ProjectileId = projectile.Id,
                Position = Vector3.one,
                Normal = Vector3.up,
                SurfaceType = "metal",
            };

            ProjectileSystem.ResolveCollision(state, in collision, in context);
            ProjectileSystem.ResolveCollision(state, in collision, in context);

            Assert.IsEmpty(state.Projectiles);
            Assert.AreEqual(2, events.All.Count);
            var hit = events.All[0];
            Assert.AreEqual(RaidEventType.ProjectileHit, hit.Type);
            Assert.AreEqual(Vector3.right * expected, hit.ImpactImpulse);
            Assert.AreEqual(collision.Position, hit.Position);
            Assert.AreEqual(collision.Normal, hit.Direction);
            Assert.AreEqual("metal", hit.StringPayload);
            Assert.AreEqual(PayloadArchetypeKey.Laser, hit.Archetype);
            Assert.AreEqual(RaidEventType.ProjectileDespawned, events.All[1].Type);
        }

        [Test]
        public void ResolveCollision_UnknownProjectile_DoesNotAffectLiveProjectile()
        {
            var state = RaidState.Create(EditModeTestsUtils.NewAllocator());
            var projectile = CreateProjectile(state, Vector3.zero, Vector3.forward);
            var events = new RaidEventBuffer();
            var context = TestContextFactory.Create(events: events);
            var collision = new CollisionSignal { ProjectileId = state.AllocateEId() };

            ProjectileSystem.ResolveCollision(state, in collision, in context);

            Assert.AreSame(projectile, state.Projectiles.Single());
            Assert.IsEmpty(events.All);
        }

        [Test]
        public void ResolveCollision_ZeroDirection_EmitsNoImpulse()
        {
            var state = RaidState.Create(EditModeTestsUtils.NewAllocator());
            var projectile = CreateProjectile(state, Vector3.zero, Vector3.zero);
            projectile.ImpactImpulse = 5f;
            var events = new RaidEventBuffer();
            var context = TestContextFactory.Create(events: events);
            var collision = new CollisionSignal { ProjectileId = projectile.Id };

            ProjectileSystem.ResolveCollision(state, in collision, in context);

            Assert.AreEqual(Vector3.zero, events.All[0].ImpactImpulse);
        }

        static ProjectileEntityState CreateProjectile(
            RaidState state, Vector3 position, Vector3 direction,
            float speed = 20f, float spawnTime = 0f, float lifetime = 3f,
            float damage = 10f)
        {
            var id = state.AllocateEId();
            var ownerId = new EId(0);
            var proj = ProjectileEntityState.Create(id, ownerId, position, direction, speed, spawnTime, lifetime, damage);
            state.Projectiles.Add(proj);
            return proj;
        }

        [Test]
        public void Tick_MovesProjectileAlongDirection()
        {
            var state = RaidState.Create(EditModeTestsUtils.NewAllocator());
            CreateProjectile(state, Vector3.zero, Vector3.forward, speed: 10f);
            var context = TestContextFactory.Create(deltaTime: 1f);

            ProjectileSystem.Tick(state, in context);

            Assert.AreEqual(10f, state.Projectiles[0].Position.z, 0.001f);
        }

        [Test]
        public void Tick_RespectsSpeed()
        {
            var state = RaidState.Create(EditModeTestsUtils.NewAllocator());
            CreateProjectile(state, Vector3.zero, Vector3.forward, speed: 5f);
            CreateProjectile(state, Vector3.zero, Vector3.forward, speed: 15f);
            var context = TestContextFactory.Create(deltaTime: 1f);

            ProjectileSystem.Tick(state, in context);

            Assert.AreEqual(5f, state.Projectiles[0].Position.z, 0.001f);
            Assert.AreEqual(15f, state.Projectiles[1].Position.z, 0.001f);
        }

        [Test]
        public void Tick_RespectsDeltaTime()
        {
            var state = RaidState.Create(EditModeTestsUtils.NewAllocator());
            CreateProjectile(state, Vector3.zero, Vector3.forward, speed: 10f);
            var context = TestContextFactory.Create(deltaTime: 0.5f);

            ProjectileSystem.Tick(state, in context);

            Assert.AreEqual(5f, state.Projectiles[0].Position.z, 0.001f);
        }

        [Test]
        public void Tick_RemovesProjectileAfterLifetime()
        {
            var state = RaidState.Create(EditModeTestsUtils.NewAllocator());
            state.ElapsedTime = 4f;
            CreateProjectile(state, Vector3.zero, Vector3.forward, spawnTime: 0f, lifetime: 3f);
            var context = TestContextFactory.Create();

            ProjectileSystem.Tick(state, in context);

            Assert.AreEqual(0, state.Projectiles.Count);
        }

        [Test]
        public void Tick_EmitsDespawnEventForExpiredProjectile()
        {
            var state = RaidState.Create(EditModeTestsUtils.NewAllocator());
            state.ElapsedTime = 4f;
            var proj = CreateProjectile(state, Vector3.zero, Vector3.forward, spawnTime: 0f, lifetime: 3f);
            var eventBuffer = new RaidEventBuffer();
            var context = TestContextFactory.Create(events: eventBuffer);

            ProjectileSystem.Tick(state, in context);

            var despawned = eventBuffer.All.Where(e => e.Type == RaidEventType.ProjectileDespawned).ToList();
            Assert.AreEqual(1, despawned.Count);
            Assert.AreEqual(proj.Id, despawned[0].Id);
        }

        [Test]
        public void Tick_MultipleProjectiles_AllMove()
        {
            var state = RaidState.Create(EditModeTestsUtils.NewAllocator());
            CreateProjectile(state, Vector3.zero, Vector3.forward, speed: 10f);
            CreateProjectile(state, Vector3.zero, Vector3.right, speed: 10f);
            CreateProjectile(state, Vector3.zero, Vector3.left, speed: 10f);
            var context = TestContextFactory.Create(deltaTime: 1f);

            ProjectileSystem.Tick(state, in context);

            Assert.AreEqual(3, state.Projectiles.Count);
            Assert.AreEqual(10f, state.Projectiles[0].Position.z, 0.001f);
            Assert.AreEqual(10f, state.Projectiles[1].Position.x, 0.001f);
            Assert.AreEqual(-10f, state.Projectiles[2].Position.x, 0.001f);
        }

        [Test]
        public void Tick_EmptyList_DoesNotThrow()
        {
            var state = RaidState.Create(EditModeTestsUtils.NewAllocator());
            var context = TestContextFactory.Create();

            Assert.DoesNotThrow(() => ProjectileSystem.Tick(state, in context));
        }

        [Test]
        public void Tick_OnlyRemovesExpiredProjectiles()
        {
            var state = RaidState.Create(EditModeTestsUtils.NewAllocator());
            state.ElapsedTime = 4f;
            CreateProjectile(state, Vector3.zero, Vector3.forward, spawnTime: 0f, lifetime: 3f);
            var alive = CreateProjectile(state, Vector3.zero, Vector3.right, spawnTime: 3f, lifetime: 3f);
            var context = TestContextFactory.Create();

            ProjectileSystem.Tick(state, in context);

            Assert.AreEqual(1, state.Projectiles.Count);
            Assert.AreEqual(alive.Id, state.Projectiles[0].Id);
        }

        [Test]
        public void Tick_ReverseIterationDoesNotSkipProjectiles()
        {
            var state = RaidState.Create(EditModeTestsUtils.NewAllocator());
            state.ElapsedTime = 5f;
            CreateProjectile(state, Vector3.zero, Vector3.forward, spawnTime: 0f, lifetime: 1f);
            var alive = CreateProjectile(state, Vector3.zero, Vector3.right, spawnTime: 4f, lifetime: 3f);
            CreateProjectile(state, Vector3.zero, Vector3.left, spawnTime: 0f, lifetime: 2f);
            var context = TestContextFactory.Create(deltaTime: 1f);

            ProjectileSystem.Tick(state, in context);

            Assert.AreEqual(1, state.Projectiles.Count);
            Assert.AreEqual(alive.Id, state.Projectiles[0].Id);
        }
    }
}
