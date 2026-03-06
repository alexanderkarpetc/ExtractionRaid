using Adapters;
using Managers;
using NUnit.Framework;
using Session;
using State;
using Tests.EditMode.Fakes;
using UnityEngine;

namespace Tests.EditMode
{
    [TestFixture]
    public class ShootingManagerTests
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

            ShootingManager.Tick(state, in context);

            Assert.AreEqual(1, state.Projectiles.Count);
        }

        [Test]
        public void Tick_WithAttackNotPressed_DoesNotSpawn()
        {
            var state = EditModeTestsUtils.CreateStateWithPlayer(Vector3.zero);
            state.PlayerEntity.FacingDirection = Vector3.forward;
            var input = new FakeInputAdapter { AttackPressed = false };
            var context = CreateContext(input);

            ShootingManager.Tick(state, in context);

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

            ShootingManager.Tick(state, in context);

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

            ShootingManager.Tick(state, in context);

            Assert.AreEqual(1, state.Projectiles.Count);
        }

        [Test]
        public void Tick_ZeroFacingDirection_DoesNotSpawn()
        {
            var state = EditModeTestsUtils.CreateStateWithPlayer(Vector3.zero);
            state.PlayerEntity.FacingDirection = Vector3.zero;
            var input = new FakeInputAdapter { AttackPressed = true };
            var context = CreateContext(input);

            ShootingManager.Tick(state, in context);

            Assert.AreEqual(0, state.Projectiles.Count);
        }

        [Test]
        public void Tick_ProjectileDirectionMatchesFacingDirection()
        {
            var state = EditModeTestsUtils.CreateStateWithPlayer(Vector3.zero);
            var facing = new Vector3(1f, 0f, 1f).normalized;
            state.PlayerEntity.FacingDirection = facing;
            var input = new FakeInputAdapter { AttackPressed = true };
            var context = CreateContext(input);

            ShootingManager.Tick(state, in context);

            Assert.AreEqual(1, state.Projectiles.Count);
            Assert.AreEqual(facing.x, state.Projectiles[0].Direction.x, 0.001f);
            Assert.AreEqual(facing.z, state.Projectiles[0].Direction.z, 0.001f);
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

            ShootingManager.Tick(state, in context);

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

            Assert.DoesNotThrow(() => ShootingManager.Tick(state, in context));
        }

        [Test]
        public void Tick_SetsLastFireTime()
        {
            var state = EditModeTestsUtils.CreateStateWithPlayer(Vector3.zero);
            state.PlayerEntity.FacingDirection = Vector3.forward;
            state.ElapsedTime = 2.5f;
            var input = new FakeInputAdapter { AttackPressed = true };
            var context = CreateContext(input);

            ShootingManager.Tick(state, in context);

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

            ShootingManager.Tick(state, in context);

            Assert.AreEqual(1, eventBuffer.SpawnedProjectiles.Count);
            Assert.AreEqual(state.Projectiles[0].Id, eventBuffer.SpawnedProjectiles[0].Id);
        }
    }
}
