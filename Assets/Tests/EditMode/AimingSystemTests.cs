using Systems;
using NUnit.Framework;
using Session;
using State;
using Tests.EditMode.Fakes;
using UnityEngine;

namespace Tests.EditMode
{
    [TestFixture]
    public class AimingSystemTests
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
        public void Tick_AimForward_SetsFacingDirectionForward()
        {
            var state = EditModeTestsUtils.CreateStateWithPlayer(Vector3.zero);
            var input = new FakeInputAdapter { AimWorldPoint = new Vector3(0f, 0f, 10f) };
            var context = CreateContext(input);

            AimingSystem.Tick(state, in context);

            Assert.AreEqual(0f, state.PlayerEntity.FacingDirection.x, 0.001f);
            Assert.AreEqual(0f, state.PlayerEntity.FacingDirection.y, 0.001f);
            Assert.AreEqual(1f, state.PlayerEntity.FacingDirection.z, 0.001f);
        }

        [Test]
        public void Tick_AimRight_SetsFacingDirectionRight()
        {
            var state = EditModeTestsUtils.CreateStateWithPlayer(Vector3.zero);
            var input = new FakeInputAdapter { AimWorldPoint = new Vector3(10f, 0f, 0f) };
            var context = CreateContext(input);

            AimingSystem.Tick(state, in context);

            Assert.AreEqual(1f, state.PlayerEntity.FacingDirection.x, 0.001f);
            Assert.AreEqual(0f, state.PlayerEntity.FacingDirection.z, 0.001f);
        }

        [Test]
        public void Tick_DiagonalAim_FacingDirectionIsNormalized()
        {
            var state = EditModeTestsUtils.CreateStateWithPlayer(Vector3.zero);
            var input = new FakeInputAdapter { AimWorldPoint = new Vector3(5f, 0f, 5f) };
            var context = CreateContext(input);

            AimingSystem.Tick(state, in context);

            Assert.AreEqual(1f, state.PlayerEntity.FacingDirection.magnitude, 0.001f);
        }

        [Test]
        public void Tick_AimOnPlayer_KeepsPreviousFacingDirection()
        {
            var state = EditModeTestsUtils.CreateStateWithPlayer(Vector3.zero);
            state.PlayerEntity.FacingDirection = Vector3.right;
            var input = new FakeInputAdapter { AimWorldPoint = Vector3.zero };
            var context = CreateContext(input);

            AimingSystem.Tick(state, in context);

            Assert.AreEqual(Vector3.right, state.PlayerEntity.FacingDirection);
        }

        [Test]
        public void Tick_AimWithYOffset_IgnoresYComponent()
        {
            var state = EditModeTestsUtils.CreateStateWithPlayer(Vector3.zero);
            var input = new FakeInputAdapter { AimWorldPoint = new Vector3(0f, 5f, 10f) };
            var context = CreateContext(input);

            AimingSystem.Tick(state, in context);

            Assert.AreEqual(0f, state.PlayerEntity.FacingDirection.y, 0.001f);
            Assert.AreEqual(1f, state.PlayerEntity.FacingDirection.z, 0.001f);
        }

        [Test]
        public void Tick_NullPlayerEntity_DoesNotThrow()
        {
            var state = RaidState.Create();
            var input = new FakeInputAdapter { AimWorldPoint = new Vector3(0f, 0f, 10f) };
            var context = CreateContext(input);

            Assert.DoesNotThrow(() => AimingSystem.Tick(state, in context));
        }

        [Test]
        public void Tick_PlayerNotAtOrigin_FacingIsRelativeToPlayerPosition()
        {
            var state = EditModeTestsUtils.CreateStateWithPlayer(new Vector3(5f, 0f, 5f));
            var input = new FakeInputAdapter { AimWorldPoint = new Vector3(5f, 0f, 15f) };
            var context = CreateContext(input);

            AimingSystem.Tick(state, in context);

            Assert.AreEqual(0f, state.PlayerEntity.FacingDirection.x, 0.001f);
            Assert.AreEqual(1f, state.PlayerEntity.FacingDirection.z, 0.001f);
        }
    }
}
