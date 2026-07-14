using NUnit.Framework;
using Session;
using State;
using Systems;
using Tests.EditMode.Fakes;
using UnityEngine;

namespace Tests.EditMode
{
    /// <summary>
    /// PlayerVisionSystem — resolves the sniper-scope reveal from the equipped weapon's
    /// SightRangeBonus, ADS state and aim distance. ScopeReveal = AdsBlend × distance-blend
    /// (0 near the player → 1 past FarDistance); ScopeRadius = the bonus; ScopeCenter = raw aim.
    /// </summary>
    [TestFixture]
    public class PlayerVisionSystemTests
    {
        static RaidContext Ctx() => TestContextFactory.Create(new FakeInputAdapter());

        static RaidState Scoped(float sightBonus, bool ads, float adsBlend, Vector3 aim)
        {
            var state = EditModeTestsUtils.CreateStateWithPlayer(Vector3.zero);
            state.PlayerEntity.EquippedWeapon.Stats.SightRangeBonus = sightBonus;
            state.PlayerEntity.IsADS = ads;
            state.PlayerEntity.AdsBlend = adsBlend;
            state.PlayerEntity.RawAimPoint = aim;
            return state;
        }

        [Test]
        public void NoScope_RevealAndRadiusZero()
        {
            var state = Scoped(0f, ads: true, adsBlend: 1f, aim: new Vector3(0f, 0f, 20f));
            var ctx = Ctx();
            PlayerVisionSystem.Tick(state, in ctx);
            Assert.AreEqual(0f, state.PlayerEntity.ScopeReveal, 1e-4f);
            Assert.AreEqual(0f, state.PlayerEntity.ScopeRadius, 1e-4f);
        }

        [Test]
        public void Scope_NotAiming_RevealZero()
        {
            var state = Scoped(15f, ads: false, adsBlend: 1f, aim: new Vector3(0f, 0f, 20f));
            var ctx = Ctx();
            PlayerVisionSystem.Tick(state, in ctx);
            Assert.AreEqual(0f, state.PlayerEntity.ScopeReveal, 1e-4f);
            Assert.AreEqual(15f, state.PlayerEntity.ScopeRadius, 1e-4f); // radius still reflects the equipped scope
        }

        [Test]
        public void Scope_Ads_FarAim_RevealFull()
        {
            // Aim 20m out — past FarDistance (13) → distance blend = 1, AdsBlend = 1 → reveal = 1.
            var state = Scoped(15f, ads: true, adsBlend: 1f, aim: new Vector3(0f, 0f, 20f));
            var ctx = Ctx();
            PlayerVisionSystem.Tick(state, in ctx);
            Assert.AreEqual(1f, state.PlayerEntity.ScopeReveal, 1e-3f);
            Assert.AreEqual(15f, state.PlayerEntity.ScopeRadius, 1e-4f);
            Assert.AreEqual(20f, state.PlayerEntity.ScopeCenter.z, 1e-4f);
        }

        [Test]
        public void Scope_Ads_NearAim_RevealZero()
        {
            // Aim 2m out — inside NearDistance (4) → distance blend = 0 → reveal = 0 (plain dot).
            var state = Scoped(15f, ads: true, adsBlend: 1f, aim: new Vector3(0f, 0f, 2f));
            var ctx = Ctx();
            PlayerVisionSystem.Tick(state, in ctx);
            Assert.AreEqual(0f, state.PlayerEntity.ScopeReveal, 1e-3f);
        }

        [Test]
        public void Scope_HalfAds_FarAim_RevealHalf()
        {
            var state = Scoped(15f, ads: true, adsBlend: 0.5f, aim: new Vector3(0f, 0f, 20f));
            var ctx = Ctx();
            PlayerVisionSystem.Tick(state, in ctx);
            Assert.AreEqual(0.5f, state.PlayerEntity.ScopeReveal, 1e-3f); // AdsBlend 0.5 × distBlend 1
        }
    }
}
