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

        [Test]
        public void Tick_AimRight_SetsAimDirectionRight()
        {
            var state = EditModeTestsUtils.CreateStateWithPlayer(Vector3.zero);
            var input = new FakeInputAdapter { AimWorldPoint = new Vector3(10f, 0f, 0f) };
            var context = CreateContext(input);

            AimingSystem.Tick(state, in context);

            Assert.AreEqual(1f, state.PlayerEntity.AimDirection.x, 0.001f);
            Assert.AreEqual(0f, state.PlayerEntity.AimDirection.z, 0.001f);
        }

        [Test]
        public void Tick_AimInsideCone_BodyDoesNotSnapInstantly()
        {
            var state = EditModeTestsUtils.CreateStateWithPlayer(Vector3.zero);
            state.PlayerEntity.FacingDirection = Vector3.forward;
            var aimDir = Quaternion.Euler(0f, 30f, 0f) * Vector3.forward;
            var input = new FakeInputAdapter { AimWorldPoint = aimDir * 10f };
            var context = CreateContext(input, deltaTime: 1f / 60f);

            AimingSystem.Tick(state, in context);

            var angle = Vector3.Angle(state.PlayerEntity.FacingDirection, aimDir);
            Assert.Greater(angle, 0.1f, "Body should not snap instantly when inside cone");
            Assert.Less(angle, 30f, "Body should have moved toward aim direction");
        }

        [Test]
        public void Tick_AimInsideCone_AimDirectionIsInstant()
        {
            var state = EditModeTestsUtils.CreateStateWithPlayer(Vector3.zero);
            state.PlayerEntity.FacingDirection = Vector3.forward;
            var aimDir = Quaternion.Euler(0f, 30f, 0f) * Vector3.forward;
            var input = new FakeInputAdapter { AimWorldPoint = aimDir * 10f };
            var context = CreateContext(input, deltaTime: 1f / 60f);

            AimingSystem.Tick(state, in context);

            Assert.AreEqual(aimDir.x, state.PlayerEntity.AimDirection.x, 0.001f);
            Assert.AreEqual(aimDir.z, state.PlayerEntity.AimDirection.z, 0.001f);
        }

        [Test]
        public void Tick_AimOutsideCone_BodySnapsInstantly()
        {
            var state = EditModeTestsUtils.CreateStateWithPlayer(Vector3.zero);
            state.PlayerEntity.FacingDirection = Vector3.forward;
            var input = new FakeInputAdapter { AimWorldPoint = new Vector3(10f, 0f, 0f) };
            var context = CreateContext(input);

            AimingSystem.Tick(state, in context);

            Assert.AreEqual(1f, state.PlayerEntity.FacingDirection.x, 0.001f);
            Assert.AreEqual(0f, state.PlayerEntity.FacingDirection.z, 0.001f);
        }

        [Test]
        public void Tick_AimAtExactConeEdge_BodyLerpsNotSnaps()
        {
            var state = EditModeTestsUtils.CreateStateWithPlayer(Vector3.zero);
            state.PlayerEntity.FacingDirection = Vector3.forward;
            var aimDir = Quaternion.Euler(0f, 45f, 0f) * Vector3.forward;
            var input = new FakeInputAdapter { AimWorldPoint = aimDir * 10f };
            var context = CreateContext(input, deltaTime: 1f / 60f);

            AimingSystem.Tick(state, in context);

            var angle = Vector3.Angle(state.PlayerEntity.FacingDirection, aimDir);
            Assert.Greater(angle, 0.1f, "Body should lerp, not snap at exact cone boundary");
        }

        [Test]
        public void Tick_MultipleTicks_InsideCone_BodyConverges()
        {
            var state = EditModeTestsUtils.CreateStateWithPlayer(Vector3.zero);
            state.PlayerEntity.FacingDirection = Vector3.forward;
            var aimDir = Quaternion.Euler(0f, 20f, 0f) * Vector3.forward;
            var input = new FakeInputAdapter { AimWorldPoint = aimDir * 10f };
            var context = CreateContext(input, deltaTime: 1f / 60f);

            for (int i = 0; i < 120; i++)
                AimingSystem.Tick(state, in context);

            var angle = Vector3.Angle(state.PlayerEntity.FacingDirection, aimDir);
            Assert.Less(angle, 2f, "Body should converge on aim direction after many ticks");
        }
    }
}
