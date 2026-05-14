using NUnit.Framework;
using Session;
using State;
using Systems;
using Tests.EditMode.Fakes;
using UnityEngine;

namespace Tests.EditMode
{
    /// <summary>
    /// A2 — verify archetype propagates through fire → impact → event pipeline so view-layer
    /// consumers (ProjectilePresenter, BloodDecalPresenter, CharacterHitFx tint) can dispatch.
    /// Visual choices themselves are play-mode feel; here we only verify the data path.
    /// </summary>
    [TestFixture]
    public class ImpactVfxDispatchTests
    {
        [Test]
        public void Shoot_LaserPayload_ProjectileCarriesLaserArchetype()
        {
            var state = EditModeTestsUtils.CreateStateWithPlayer(Vector3.zero);
            state.PlayerEntity.FacingDirection = Vector3.forward;
            var weapon = state.PlayerEntity.EquippedWeapon;
            var laserSO = WeaponBuilderTestFactory.MakeLaser(chargeTime: 1f);
            try
            {
                weapon.PayloadDefinition = laserSO;
                weapon.Phase = WeaponPhase.Charging;
                weapon.ChargeStartTime = 0f;
                state.ElapsedTime = 1.1f;
                var input = new FakeInputAdapter { AttackJustReleased = true };
                var ctx = TestContextFactory.Create(input);

                ShootingSystem.Tick(state, in ctx);

                Assert.AreEqual(1, state.Projectiles.Count);
                Assert.AreEqual(PayloadArchetypeKey.Laser, state.Projectiles[0].Archetype);
            }
            finally { Object.DestroyImmediate(laserSO); }
        }

        [Test]
        public void Shoot_BallisticPayload_ProjectileCarriesBallisticArchetype()
        {
            var state = EditModeTestsUtils.CreateStateWithPlayer(Vector3.zero);
            state.PlayerEntity.FacingDirection = Vector3.forward;
            var weapon = state.PlayerEntity.EquippedWeapon;
            var ballisticSO = WeaponBuilderTestFactory.MakeBallistic();
            try
            {
                weapon.PayloadDefinition = ballisticSO;
                var input = new FakeInputAdapter { AttackPressed = true };
                var ctx = TestContextFactory.Create(input);

                ShootingSystem.Tick(state, in ctx);

                Assert.AreEqual(1, state.Projectiles.Count);
                Assert.AreEqual(PayloadArchetypeKey.Ballistic, state.Projectiles[0].Archetype);
            }
            finally { Object.DestroyImmediate(ballisticSO); }
        }

        [Test]
        public void Shoot_NoPayload_DefaultsToBallistic()
        {
            // No PayloadDefinition (default starting weapon) → defaults to Ballistic.
            var state = EditModeTestsUtils.CreateStateWithPlayer(Vector3.zero);
            state.PlayerEntity.FacingDirection = Vector3.forward;
            Assert.IsNull(state.PlayerEntity.EquippedWeapon.PayloadDefinition, "precondition");

            var input = new FakeInputAdapter { AttackPressed = true };
            var ctx = TestContextFactory.Create(input);

            ShootingSystem.Tick(state, in ctx);

            Assert.AreEqual(1, state.Projectiles.Count);
            Assert.AreEqual(PayloadArchetypeKey.Ballistic, state.Projectiles[0].Archetype);
        }
    }
}
