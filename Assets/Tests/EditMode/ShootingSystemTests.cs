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
    public class ShootingSystemTests
    {
        static RaidContext CreateContext(FakeInputAdapter input, float deltaTime = 1f / 60f,
            IRaidEvents events = null)
        {
            return new RaidContext(
                deltaTime: deltaTime,
                events: events ?? new RaidEventBuffer(),
                time: new FakeTimeAdapter { DeltaTime = deltaTime },
                input: input,
                navMesh: new FakeNavMeshAdapter()
            );
        }

        [Test]
        public void Tick_WithAttackPressedAndValidFacing_SpawnsProjectile()
        {
            var state = EditModeTestsUtils.CreateStateWithPlayer(Vector3.zero);
            state.PlayerEntity.FacingDirection = Vector3.forward;
            var input = new FakeInputAdapter { AttackPressed = true };
            var context = CreateContext(input);

            ShootingSystem.Tick(state, in context);

            Assert.AreEqual(1, state.Projectiles.Count);
        }

        [Test]
        public void Tick_WithAttackNotPressed_DoesNotSpawn()
        {
            var state = EditModeTestsUtils.CreateStateWithPlayer(Vector3.zero);
            state.PlayerEntity.FacingDirection = Vector3.forward;
            var input = new FakeInputAdapter { AttackPressed = false };
            var context = CreateContext(input);

            ShootingSystem.Tick(state, in context);

            Assert.AreEqual(0, state.Projectiles.Count);
        }

        [Test]
        public void Tick_WithinCooldown_DoesNotSpawn()
        {
            var state = EditModeTestsUtils.CreateStateWithPlayer(Vector3.zero);
            state.PlayerEntity.FacingDirection = Vector3.forward;
            state.ElapsedTime = 1f;
            state.PlayerEntity.Combat.LastFireTime = 0.9f;
            var input = new FakeInputAdapter { AttackPressed = true };
            var context = CreateContext(input);

            ShootingSystem.Tick(state, in context);

            Assert.AreEqual(0, state.Projectiles.Count);
        }

        [Test]
        public void Tick_AfterCooldownExpires_SpawnsAgain()
        {
            var state = EditModeTestsUtils.CreateStateWithPlayer(Vector3.zero);
            state.PlayerEntity.FacingDirection = Vector3.forward;
            state.ElapsedTime = 1f;
            state.PlayerEntity.Combat.LastFireTime = 0.5f;
            var input = new FakeInputAdapter { AttackPressed = true };
            var context = CreateContext(input);

            ShootingSystem.Tick(state, in context);

            Assert.AreEqual(1, state.Projectiles.Count);
        }

        [Test]
        public void Tick_ZeroAimDirection_DoesNotSpawn()
        {
            var state = EditModeTestsUtils.CreateStateWithPlayer(Vector3.zero);
            state.PlayerEntity.AimDirection = Vector3.zero;
            var input = new FakeInputAdapter { AttackPressed = true };
            var context = CreateContext(input);

            ShootingSystem.Tick(state, in context);

            Assert.AreEqual(0, state.Projectiles.Count);
        }

        [Test]
        public void Tick_ProjectileDirectionMatchesAimDirection()
        {
            var state = EditModeTestsUtils.CreateStateWithPlayer(Vector3.zero);
            var aimDir = new Vector3(1f, 0f, 1f).normalized;
            state.PlayerEntity.FacingDirection = Vector3.forward;
            state.PlayerEntity.AimDirection = aimDir;
            var input = new FakeInputAdapter { AttackPressed = true };
            var context = CreateContext(input);

            ShootingSystem.Tick(state, in context);

            Assert.AreEqual(1, state.Projectiles.Count);
            Assert.AreEqual(aimDir.x, state.Projectiles[0].Direction.x, 0.001f);
            Assert.AreEqual(aimDir.z, state.Projectiles[0].Direction.z, 0.001f);
        }

        [Test]
        public void Tick_ProjectileSpawnsAtMuzzleWorldPoint()
        {
            var muzzlePos = new Vector3(2f, 0.5f, 4.2f);
            var state = EditModeTestsUtils.CreateStateWithPlayer(new Vector3(2f, 0f, 3f));
            state.PlayerEntity.FacingDirection = Vector3.forward;
            var input = new FakeInputAdapter
            {
                AttackPressed = true,
                MuzzleWorldPoint = muzzlePos,
            };
            var context = CreateContext(input);

            ShootingSystem.Tick(state, in context);

            var proj = state.Projectiles[0];
            Assert.AreEqual(muzzlePos.x, proj.Position.x, 0.001f);
            Assert.AreEqual(muzzlePos.y, proj.Position.y, 0.001f);
            Assert.AreEqual(muzzlePos.z, proj.Position.z, 0.001f);
        }

        [Test]
        public void Tick_NullPlayer_DoesNotThrow()
        {
            var state = RaidState.Create();
            var input = new FakeInputAdapter { AttackPressed = true };
            var context = CreateContext(input);

            Assert.DoesNotThrow(() => ShootingSystem.Tick(state, in context));
        }

        [Test]
        public void Tick_SetsLastFireTime()
        {
            var state = EditModeTestsUtils.CreateStateWithPlayer(Vector3.zero);
            state.PlayerEntity.FacingDirection = Vector3.forward;
            state.ElapsedTime = 2.5f;
            var input = new FakeInputAdapter { AttackPressed = true };
            var context = CreateContext(input);

            ShootingSystem.Tick(state, in context);

            Assert.AreEqual(2.5f, state.PlayerEntity.Combat.LastFireTime, 0.001f);
        }

        [Test]
        public void Tick_EmitsProjectileSpawnedEvent()
        {
            var state = EditModeTestsUtils.CreateStateWithPlayer(Vector3.zero);
            state.PlayerEntity.FacingDirection = Vector3.forward;
            var eventBuffer = new RaidEventBuffer();
            var input = new FakeInputAdapter { AttackPressed = true };
            var context = CreateContext(input, events: eventBuffer);

            ShootingSystem.Tick(state, in context);

            var spawned = eventBuffer.All.Where(e => e.Type == RaidEventType.ProjectileSpawned).ToList();
            Assert.AreEqual(1, spawned.Count);
            Assert.AreEqual(state.Projectiles[0].Id, spawned[0].Id);
        }
    }
}
