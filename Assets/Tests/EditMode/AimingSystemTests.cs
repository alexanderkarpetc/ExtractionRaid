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

        // ── FacingDirection tests (follows raw aim, unchanged behavior) ──

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
            var context = CreateContext(input, deltaTime: 1f);

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
        public void Tick_AimOutsideCone_BodySnapsInstantly()
        {
            var state = EditModeTestsUtils.CreateStateWithPlayer(Vector3.zero);
            state.PlayerEntity.FacingDirection = Vector3.forward;
            var input = new FakeInputAdapter { AimWorldPoint = new Vector3(10f, 0f, 0f) };
            var context = CreateContext(input, deltaTime: 1);

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
        public void Tick_NoEquippedWeapon_BodyRotatesWithUnarmedSettings()
        {
            var state = EditModeTestsUtils.CreateStateWithPlayer(Vector3.zero);
            state.PlayerEntity.EquippedWeapon = null;
            state.PlayerEntity.FacingDirection = Vector3.forward;
            var aimDir = Quaternion.Euler(0f, 30f, 0f) * Vector3.forward;
            var input = new FakeInputAdapter { AimWorldPoint = aimDir * 10f };
            var context = CreateContext(input, deltaTime: 1f / 60f);

            AimingSystem.Tick(state, in context);

            var angle = Vector3.Angle(state.PlayerEntity.FacingDirection, aimDir);
            Assert.Greater(angle, 0.1f, "Body should not snap instantly");
            Assert.Less(angle, 30f, "Body should have moved toward aim direction");
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

        // ── Raw aim tests (instant) ──

        [Test]
        public void Tick_RawAimPoint_MatchesInputWorldPoint()
        {
            var state = EditModeTestsUtils.CreateStateWithPlayer(Vector3.zero);
            var aimTarget = new Vector3(7f, 2f, 12f);
            var input = new FakeInputAdapter { AimWorldPoint = aimTarget };
            var context = CreateContext(input);

            AimingSystem.Tick(state, in context);

            Assert.AreEqual(aimTarget.x, state.PlayerEntity.RawAimPoint.x, 0.001f);
            Assert.AreEqual(aimTarget.y, state.PlayerEntity.RawAimPoint.y, 0.001f);
            Assert.AreEqual(aimTarget.z, state.PlayerEntity.RawAimPoint.z, 0.001f);
        }

        [Test]
        public void Tick_RawAimPoint_UpdatesInstantlyEachFrame()
        {
            var state = EditModeTestsUtils.CreateStateWithPlayer(Vector3.zero);
            var input = new FakeInputAdapter { AimWorldPoint = new Vector3(5f, 0f, 5f) };
            var context = CreateContext(input);

            AimingSystem.Tick(state, in context);
            Assert.AreEqual(5f, state.PlayerEntity.RawAimPoint.x, 0.001f);

            input.AimWorldPoint = new Vector3(-3f, 0f, 8f);
            AimingSystem.Tick(state, in context);
            Assert.AreEqual(-3f, state.PlayerEntity.RawAimPoint.x, 0.001f);
            Assert.AreEqual(8f, state.PlayerEntity.RawAimPoint.z, 0.001f);
        }

        // ── Weapon aim tests (smoothed) ──

        [Test]
        public void Tick_WeaponAimPoint_MovesTowardTarget_WhenStartingAtOrigin()
        {
            var state = EditModeTestsUtils.CreateStateWithPlayer(Vector3.zero);
            state.PlayerEntity.WeaponAimPoint = Vector3.zero;
            var input = new FakeInputAdapter { AimWorldPoint = new Vector3(10f, 0f, 0f) };
            var context = CreateContext(input);

            AimingSystem.Tick(state, in context);

            // Position lerp: moves toward target but doesn't snap
            Assert.Greater(state.PlayerEntity.WeaponAimPoint.x, 0.5f,
                "Weapon aim should move toward target");
            Assert.Less(state.PlayerEntity.WeaponAimPoint.x, 10f,
                "Weapon aim should not reach target in one tick");
        }

        [Test]
        public void Tick_WeaponAimPoint_FollowsRawWithDelay()
        {
            var state = EditModeTestsUtils.CreateStateWithPlayer(Vector3.zero);
            state.PlayerEntity.WeaponAimPoint = new Vector3(0f, 0f, 10f);
            var input = new FakeInputAdapter { AimWorldPoint = new Vector3(10f, 0f, 0f) };
            var context = CreateContext(input, deltaTime: 1f / 60f);

            AimingSystem.Tick(state, in context);

            var toWeapon = state.PlayerEntity.WeaponAimPoint - state.PlayerEntity.Position;
            var toRaw = new Vector3(10f, 0f, 0f);
            var angle = Vector3.Angle(toWeapon, toRaw);
            Assert.Greater(angle, 5f, "Weapon aim should lag behind raw aim");
            Assert.Less(angle, 90f, "Weapon aim should have moved toward raw aim");
        }

        [Test]
        public void Tick_WeaponAimPoint_HighSharpness_ConvergesFast()
        {
            var state = EditModeTestsUtils.CreateStateWithPlayer(Vector3.zero);
            state.PlayerEntity.EquippedWeapon.AimFollowSharpness = 50f;
            state.PlayerEntity.WeaponAimPoint = new Vector3(0f, 0f, 10f);
            var input = new FakeInputAdapter { AimWorldPoint = new Vector3(10f, 0f, 0f) };
            var context = CreateContext(input, deltaTime: 1f / 60f);

            for (int i = 0; i < 10; i++)
                AimingSystem.Tick(state, in context);

            var toWeapon = (state.PlayerEntity.WeaponAimPoint - state.PlayerEntity.Position).normalized;
            var toRaw = new Vector3(1f, 0f, 0f);
            var angle = Vector3.Angle(toWeapon, toRaw);
            Assert.Less(angle, 5f, "High sharpness should converge in a few ticks");
        }

        [Test]
        public void Tick_WeaponAimPoint_LowSharpness_ConvergesSlow()
        {
            var state = EditModeTestsUtils.CreateStateWithPlayer(Vector3.zero);
            state.PlayerEntity.EquippedWeapon.AimFollowSharpness = 3f;
            state.PlayerEntity.WeaponAimPoint = new Vector3(0f, 0f, 10f);
            var input = new FakeInputAdapter { AimWorldPoint = new Vector3(10f, 0f, 0f) };
            var context = CreateContext(input, deltaTime: 1f / 60f);

            for (int i = 0; i < 10; i++)
                AimingSystem.Tick(state, in context);

            var toWeapon = (state.PlayerEntity.WeaponAimPoint - state.PlayerEntity.Position).normalized;
            var toRaw = new Vector3(1f, 0f, 0f);
            var angle = Vector3.Angle(toWeapon, toRaw);
            Assert.Greater(angle, 20f, "Low sharpness should converge slowly");
        }

        [Test]
        public void Tick_WeaponAimPoint_NoWeapon_UsesUnarmedSharpness()
        {
            var state = EditModeTestsUtils.CreateStateWithPlayer(Vector3.zero);
            state.PlayerEntity.EquippedWeapon = null;
            state.PlayerEntity.WeaponAimPoint = new Vector3(0f, 0f, 10f);
            var input = new FakeInputAdapter { AimWorldPoint = new Vector3(10f, 0f, 0f) };
            var context = CreateContext(input, deltaTime: 1f / 60f);

            AimingSystem.Tick(state, in context);

            var toWeapon = (state.PlayerEntity.WeaponAimPoint - state.PlayerEntity.Position).normalized;
            var toRaw = new Vector3(1f, 0f, 0f);
            var angle = Vector3.Angle(toWeapon, toRaw);
            Assert.Less(angle, 70f, "Unarmed sharpness should move weapon aim noticeably in one tick");
        }

        [Test]
        public void Tick_WeaponAimPoint_ConvergesOverMultipleTicks()
        {
            var state = EditModeTestsUtils.CreateStateWithPlayer(Vector3.zero);
            state.PlayerEntity.WeaponAimPoint = new Vector3(0f, 0f, 10f);
            var input = new FakeInputAdapter { AimWorldPoint = new Vector3(10f, 0f, 0f) };
            var context = CreateContext(input, deltaTime: 1f / 60f);

            for (int i = 0; i < 60; i++)
                AimingSystem.Tick(state, in context);

            var toWeapon = (state.PlayerEntity.WeaponAimPoint - state.PlayerEntity.Position).normalized;
            var toRaw = new Vector3(1f, 0f, 0f);
            var angle = Vector3.Angle(toWeapon, toRaw);
            Assert.Less(angle, 1f, "Weapon aim should converge after many ticks");
        }

        // ── AimDirection tests (derived from weapon aim) ──

        [Test]
        public void Tick_AimDirection_DerivedFromWeaponAimPoint()
        {
            var state = EditModeTestsUtils.CreateStateWithPlayer(Vector3.zero);
            // Pre-align weapon aim to target so AimDirection matches instantly
            state.PlayerEntity.WeaponAimPoint = new Vector3(10f, 0f, 0f);
            var input = new FakeInputAdapter { AimWorldPoint = new Vector3(10f, 0f, 0f) };
            var context = CreateContext(input);

            AimingSystem.Tick(state, in context);

            Assert.AreEqual(1f, state.PlayerEntity.AimDirection.x, 0.001f);
            Assert.AreEqual(0f, state.PlayerEntity.AimDirection.z, 0.001f);
        }

        [Test]
        public void Tick_AimDirection_NotInstant_WhenWeaponLags()
        {
            var state = EditModeTestsUtils.CreateStateWithPlayer(Vector3.zero);
            state.PlayerEntity.EquippedWeapon.AimFollowSharpness = 5f;
            state.PlayerEntity.WeaponAimPoint = new Vector3(0f, 0f, 10f);
            var input = new FakeInputAdapter { AimWorldPoint = new Vector3(10f, 0f, 0f) };
            var context = CreateContext(input, deltaTime: 1f / 60f);

            AimingSystem.Tick(state, in context);

            var angle = Vector3.Angle(state.PlayerEntity.AimDirection, Vector3.right);
            Assert.Greater(angle, 10f, "AimDirection should lag when weapon aim is smoothed");
        }

        [Test]
        public void Tick_AimDirection_ConvergesWithWeaponAim()
        {
            var state = EditModeTestsUtils.CreateStateWithPlayer(Vector3.zero);
            state.PlayerEntity.WeaponAimPoint = new Vector3(0f, 0f, 10f);
            var input = new FakeInputAdapter { AimWorldPoint = new Vector3(10f, 0f, 0f) };
            var context = CreateContext(input, deltaTime: 1f / 60f);

            for (int i = 0; i < 60; i++)
                AimingSystem.Tick(state, in context);

            Assert.AreEqual(1f, state.PlayerEntity.AimDirection.x, 0.05f);
            Assert.AreEqual(0f, state.PlayerEntity.AimDirection.z, 0.05f);
        }

        // ── FacingDirection follows raw aim (not weapon aim) ──

        [Test]
        public void Tick_FacingDirection_FollowsRawAim_NotWeaponAim()
        {
            var state = EditModeTestsUtils.CreateStateWithPlayer(Vector3.zero);
            state.PlayerEntity.EquippedWeapon.AimFollowSharpness = 1f;
            state.PlayerEntity.FacingDirection = Vector3.forward;
            state.PlayerEntity.WeaponAimPoint = new Vector3(0f, 0f, 10f);
            var input = new FakeInputAdapter { AimWorldPoint = new Vector3(10f, 0f, 0f) };
            var context = CreateContext(input, deltaTime: 1f);

            AimingSystem.Tick(state, in context);

            // Body should face right (raw aim), despite weapon aim still being mostly forward
            Assert.AreEqual(1f, state.PlayerEntity.FacingDirection.x, 0.001f);
            Assert.AreEqual(0f, state.PlayerEntity.FacingDirection.z, 0.001f);
        }

        [Test]
        public void Tick_WeaponAimPoint_FollowsStraightLine()
        {
            // Position lerp follows straight line (no angular arc deviation)
            var state = EditModeTestsUtils.CreateStateWithPlayer(Vector3.zero);
            // Weapon aim starts at (0, 0, 10), raw aim at (0, 0, -10) — same X
            state.PlayerEntity.WeaponAimPoint = new Vector3(5f, 0f, 10f);
            var input = new FakeInputAdapter { AimWorldPoint = new Vector3(5f, 0f, -10f) };
            var context = CreateContext(input, deltaTime: 1f / 60f);

            AimingSystem.Tick(state, in context);

            // X should stay at 5 (straight line between two points with same X)
            Assert.AreEqual(5f, state.PlayerEntity.WeaponAimPoint.x, 0.01f,
                "Position lerp should follow straight line, no lateral deviation");
        }
    }
}
