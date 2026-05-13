using Dev;
using NUnit.Framework;
using State;
using UnityEngine;

namespace Tests.EditMode
{
    /// <summary>
    /// A1 per-archetype camera shake — pure-logic composition tests for
    /// <see cref="ArchetypeShakeResolver"/>. View-layer event routing is verified
    /// behaviorally у play mode, not unit-tested.
    /// </summary>
    [TestFixture]
    public class ArchetypeShakeResolverTests
    {
        ViewCheatsCameraShakeSection _cfg;

        [SetUp]
        public void Setup()
        {
            // SO ScriptableObject.CreateInstance applies field initializers, so defaults
            // already match section's authored values.
            _cfg = ScriptableObject.CreateInstance<ViewCheatsCameraShakeSection>();
        }

        [TearDown]
        public void Teardown() => Object.DestroyImmediate(_cfg);

        [Test]
        public void Resolve_AutoBallistic_UsesAutoShape_BallisticModifier()
        {
            var r = ArchetypeShakeResolver.Resolve(_cfg, "Ballistic", FiringPattern.Auto);
            // Auto shape × Ballistic mod (scale=1, freq=30)
            Assert.AreEqual(_cfg.AutoShape.KickMagnitude * _cfg.BallisticModifier.KickMagnitudeScale,
                r.KickMagnitude, 1e-4f);
            Assert.AreEqual(_cfg.AutoShape.KickDuration, r.KickDuration, 1e-4f);
            Assert.AreEqual(_cfg.AutoShape.KickDirOffset, r.KickDirOffset);
            Assert.AreEqual(_cfg.BallisticModifier.TremorFrequency, r.TremorFrequency, 1e-4f);
        }

        [Test]
        public void Resolve_ScatterLaser_AppliesLaserModifierToScatterShape()
        {
            var r = ArchetypeShakeResolver.Resolve(_cfg, "Laser", FiringPattern.Scatter);
            // Laser mod: kick 0.7×, tremor 1.3×, freq=18
            Assert.AreEqual(_cfg.ScatterShape.KickMagnitude * _cfg.LaserModifier.KickMagnitudeScale,
                r.KickMagnitude, 1e-4f);
            Assert.AreEqual(_cfg.ScatterShape.TremorMagnitude * _cfg.LaserModifier.TremorMagnitudeScale,
                r.TremorMagnitude, 1e-4f);
            Assert.AreEqual(18f, r.TremorFrequency, 1e-4f);
            Assert.AreEqual(_cfg.ScatterShape.KickDirOffset, r.KickDirOffset,
                "Direction offset comes from shape, not modifier");
        }

        [Test]
        public void Resolve_UnknownPayload_FallsBackToBallistic()
        {
            var rUnknown = ArchetypeShakeResolver.Resolve(_cfg, "Plasma", FiringPattern.Single);
            var rBal     = ArchetypeShakeResolver.Resolve(_cfg, "Ballistic", FiringPattern.Single);
            Assert.AreEqual(rBal.KickMagnitude, rUnknown.KickMagnitude, 1e-4f);
            Assert.AreEqual(rBal.TremorFrequency, rUnknown.TremorFrequency, 1e-4f);
        }

        [Test]
        public void Resolve_NullPayload_FallsBackToBallistic()
        {
            var rNull = ArchetypeShakeResolver.Resolve(_cfg, null, FiringPattern.Single);
            var rBal  = ArchetypeShakeResolver.Resolve(_cfg, "Ballistic", FiringPattern.Single);
            Assert.AreEqual(rBal.KickMagnitude, rNull.KickMagnitude, 1e-4f);
        }

        [Test]
        public void Resolve_ScatterShape_HasBigSideOffset()
        {
            // Spec: shotgun has lateral shove (X≠0). Pistol/rifle should be purely vertical.
            Assert.AreNotEqual(0f, _cfg.ScatterShape.KickDirOffset.x,
                "Spec: Scatter has lateral kick component");
            Assert.AreEqual(0f, _cfg.SingleActionShape.KickDirOffset.x, 1e-4f,
                "Spec: Single (pistol) is vertical only");
            Assert.AreEqual(0f, _cfg.AutoShape.KickDirOffset.x, 1e-4f,
                "Spec: Auto (rifle) is vertical only — climb pattern");
        }

        [Test]
        public void Resolve_BallisticVsLaser_DiffersOnKickAndFrequency()
        {
            // Spec: laser quieter kick (0.7×) but more tremor (1.3×) at lower freq.
            var bal = ArchetypeShakeResolver.Resolve(_cfg, "Ballistic", FiringPattern.Auto);
            var las = ArchetypeShakeResolver.Resolve(_cfg, "Laser",     FiringPattern.Auto);
            Assert.Less(las.KickMagnitude, bal.KickMagnitude, "Laser kick softer");
            Assert.Greater(las.TremorMagnitude, bal.TremorMagnitude, "Laser tremor stronger");
            Assert.Less(las.TremorFrequency, bal.TremorFrequency, "Laser hum is slower buzz");
        }
    }
}
