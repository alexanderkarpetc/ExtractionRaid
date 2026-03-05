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
        public void Tick_WithAttackPressedAndValidAim_SpawnsProjectile()
        {
            var state = EditModeTestsUtils.CreateStateWithPlayer(Vector3.zero);
            var input = new FakeInputAdapter
            {
                AttackPressed = true,
                AimWorldPoint = new Vector3(0f, 0f, 10f),
            };
            var context = CreateContext(input);

            ShootingManager.Tick(state, in context);

            Assert.AreEqual(1, state.Projectiles.Count);
        }

        [Test]
        public void Tick_WithAttackNotPressed_DoesNotSpawn()
        {
            var state = EditModeTestsUtils.CreateStateWithPlayer(Vector3.zero);
            var input = new FakeInputAdapter
            {
                AttackPressed = false,
                AimWorldPoint = new Vector3(0f, 0f, 10f),
            };
            var context = CreateContext(input);

            ShootingManager.Tick(state, in context);

            Assert.AreEqual(0, state.Projectiles.Count);
        }

        [Test]
        public void Tick_WithinCooldown_DoesNotSpawn()
        {
            var state = EditModeTestsUtils.CreateStateWithPlayer(Vector3.zero);
            state.ElapsedTime = 1f;
            state.PlayerEntity.Combat.LastFireTime = 0.9f;
            var input = new FakeInputAdapter
            {
                AttackPressed = true,
                AimWorldPoint = new Vector3(0f, 0f, 10f),
            };
            var context = CreateContext(input);

            ShootingManager.Tick(state, in context);

            Assert.AreEqual(0, state.Projectiles.Count);
        }

        [Test]
        public void Tick_AfterCooldownExpires_SpawnsAgain()
        {
            var state = EditModeTestsUtils.CreateStateWithPlayer(Vector3.zero);
            state.ElapsedTime = 1f;
            state.PlayerEntity.Combat.LastFireTime = 0.5f;
            var input = new FakeInputAdapter
            {
                AttackPressed = true,
                AimWorldPoint = new Vector3(0f, 0f, 10f),
            };
            var context = CreateContext(input);

            ShootingManager.Tick(state, in context);

            Assert.AreEqual(1, state.Projectiles.Count);
        }

        [Test]
        public void Tick_AimTooCloseToPlayer_DoesNotSpawn()
        {
            var state = EditModeTestsUtils.CreateStateWithPlayer(Vector3.zero);
            var input = new FakeInputAdapter
            {
                AttackPressed = true,
                AimWorldPoint = Vector3.zero,
            };
            var context = CreateContext(input);

            ShootingManager.Tick(state, in context);

            Assert.AreEqual(0, state.Projectiles.Count);
        }

        [Test]
        public void Tick_ProjectileDirectionIsNormalized()
        {
            var state = EditModeTestsUtils.CreateStateWithPlayer(Vector3.zero);
            var input = new FakeInputAdapter
            {
                AttackPressed = true,
                AimWorldPoint = new Vector3(5f, 0f, 5f),
            };
            var context = CreateContext(input);

            ShootingManager.Tick(state, in context);

            Assert.AreEqual(1, state.Projectiles.Count);
            Assert.AreEqual(1f, state.Projectiles[0].Direction.magnitude, 0.001f);
        }

        [Test]
        public void Tick_ProjectileSpawnPositionIsAtMuzzleOffset()
        {
            var playerPos = new Vector3(2f, 0f, 3f);
            var state = EditModeTestsUtils.CreateStateWithPlayer(playerPos);
            var input = new FakeInputAdapter
            {
                AttackPressed = true,
                AimWorldPoint = new Vector3(2f, 0f, 13f),
            };
            var context = CreateContext(input);

            ShootingManager.Tick(state, in context);

            var proj = state.Projectiles[0];
            var expectedPos = playerPos + Vector3.forward * ShootingManager.MuzzleOffset;
            Assert.AreEqual(expectedPos.x, proj.Position.x, 0.001f);
            Assert.AreEqual(expectedPos.z, proj.Position.z, 0.001f);
            Assert.AreEqual(playerPos.y, proj.Position.y, 0.001f);
        }

        [Test]
        public void Tick_NullPlayer_DoesNotThrow()
        {
            var state = RaidState.Create();
            var input = new FakeInputAdapter
            {
                AttackPressed = true,
                AimWorldPoint = new Vector3(0f, 0f, 10f),
            };
            var context = CreateContext(input);

            Assert.DoesNotThrow(() => ShootingManager.Tick(state, in context));
        }

        [Test]
        public void Tick_SetsLastFireTime()
        {
            var state = EditModeTestsUtils.CreateStateWithPlayer(Vector3.zero);
            state.ElapsedTime = 2.5f;
            var input = new FakeInputAdapter
            {
                AttackPressed = true,
                AimWorldPoint = new Vector3(0f, 0f, 10f),
            };
            var context = CreateContext(input);

            ShootingManager.Tick(state, in context);

            Assert.AreEqual(2.5f, state.PlayerEntity.Combat.LastFireTime, 0.001f);
        }

        [Test]
        public void Tick_EmitsProjectileSpawnedEvent()
        {
            var state = EditModeTestsUtils.CreateStateWithPlayer(Vector3.zero);
            var eventBuffer = new RaidEventBuffer();
            var input = new FakeInputAdapter
            {
                AttackPressed = true,
                AimWorldPoint = new Vector3(0f, 0f, 10f),
            };
            var context = CreateContext(input, events: eventBuffer);

            ShootingManager.Tick(state, in context);

            Assert.AreEqual(1, eventBuffer.SpawnedProjectiles.Count);
            Assert.AreEqual(state.Projectiles[0].Id, eventBuffer.SpawnedProjectiles[0].Id);
        }
    }
}
