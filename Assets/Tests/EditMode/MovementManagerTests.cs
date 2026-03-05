using Managers;
using NUnit.Framework;
using Session;
using State;
using Tests.EditMode.Fakes;
using UnityEngine;

namespace Tests.EditMode
{
    [TestFixture]
    public class MovementManagerTests
    {
        static RaidContext CreateContext(FakeInputAdapter input, float deltaTime = 1f / 60f)
        {
            return new RaidContext(
                deltaTime: deltaTime,
                events: new FakeRaidEvents(),
                time: new FakeTimeAdapter { DeltaTime = deltaTime },
                input: input,
                navMesh: new FakeNavMeshAdapter()
            );
        }

        [Test]
        public void Tick_WithForwardInput_MovesPlayerAlongPositiveZ()
        {
            var state = EditModeTestsUtils.CreateStateWithPlayer(Vector3.zero);
            var input = new FakeInputAdapter { MoveInput = Vector2.up };
            var context = CreateContext(input, deltaTime: 1f);

            MovementManager.Tick(state, in context);

            Assert.AreEqual(0f, state.PlayerEntity.Position.x, 0.001f);
            Assert.AreEqual(MovementManager.MoveSpeed, state.PlayerEntity.Position.z, 0.001f);
            Assert.AreEqual(0f, state.PlayerEntity.Position.y, 0.001f);
        }

        [Test]
        public void Tick_WithRightInput_MovesPlayerAlongPositiveX()
        {
            var state = EditModeTestsUtils.CreateStateWithPlayer(Vector3.zero);
            var input = new FakeInputAdapter { MoveInput = Vector2.right };
            var context = CreateContext(input, deltaTime: 1f);

            MovementManager.Tick(state, in context);

            Assert.AreEqual(MovementManager.MoveSpeed, state.PlayerEntity.Position.x, 0.001f);
            Assert.AreEqual(0f, state.PlayerEntity.Position.z, 0.001f);
        }

        [Test]
        public void Tick_WithZeroInput_DoesNotMove()
        {
            var startPos = new Vector3(5f, 0f, 3f);
            var state = EditModeTestsUtils.CreateStateWithPlayer(startPos);
            var input = new FakeInputAdapter { MoveInput = Vector2.zero };
            var context = CreateContext(input, deltaTime: 1f);

            MovementManager.Tick(state, in context);

            Assert.AreEqual(5f, state.PlayerEntity.Position.x, 0.001f);
            Assert.AreEqual(3f, state.PlayerEntity.Position.z, 0.001f);
        }

        [Test]
        public void Tick_DiagonalInput_IsNormalized()
        {
            var state = EditModeTestsUtils.CreateStateWithPlayer(Vector3.zero);
            var input = new FakeInputAdapter { MoveInput = new Vector2(1f, 1f) };
            var context = CreateContext(input, deltaTime: 1f);

            MovementManager.Tick(state, in context);

            var distance = state.PlayerEntity.Position.magnitude;
            Assert.AreEqual(MovementManager.MoveSpeed, distance, 0.01f);
        }

        [Test]
        public void Tick_RespectsDeltatime()
        {
            var state = EditModeTestsUtils.CreateStateWithPlayer(Vector3.zero);
            var input = new FakeInputAdapter { MoveInput = Vector2.up };
            var context = CreateContext(input, deltaTime: 0.5f);

            MovementManager.Tick(state, in context);

            Assert.AreEqual(MovementManager.MoveSpeed * 0.5f, state.PlayerEntity.Position.z, 0.001f);
        }

        [Test]
        public void Tick_SetsVelocityFromInput()
        {
            var state = EditModeTestsUtils.CreateStateWithPlayer(Vector3.zero);
            var input = new FakeInputAdapter { MoveInput = Vector2.right };
            var context = CreateContext(input, deltaTime: 1f / 60f);

            MovementManager.Tick(state, in context);

            Assert.AreEqual(MovementManager.MoveSpeed, state.PlayerEntity.Velocity.x, 0.001f);
            Assert.AreEqual(0f, state.PlayerEntity.Velocity.z, 0.001f);
        }

        [Test]
        public void Tick_NullPlayerEntity_DoesNotThrow()
        {
            var state = RaidState.Create();
            var input = new FakeInputAdapter { MoveInput = Vector2.up };
            var context = CreateContext(input, deltaTime: 1f);

            Assert.DoesNotThrow(() => MovementManager.Tick(state, in context));
        }

        [Test]
        public void Tick_AccumulatesPositionOverMultipleTicks()
        {
            var state = EditModeTestsUtils.CreateStateWithPlayer(Vector3.zero);
            var input = new FakeInputAdapter { MoveInput = Vector2.up };
            var context = CreateContext(input, deltaTime: 1f);

            MovementManager.Tick(state, in context);
            MovementManager.Tick(state, in context);
            MovementManager.Tick(state, in context);

            Assert.AreEqual(MovementManager.MoveSpeed * 3f, state.PlayerEntity.Position.z, 0.01f);
        }
    }
}
