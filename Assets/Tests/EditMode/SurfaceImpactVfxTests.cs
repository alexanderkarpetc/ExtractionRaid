using Dev;
using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode
{
    public class SurfaceImpactVfxTests
    {
        ViewCheatsImpactVfxSection _config;
        GameObject _fallback;
        GameObject _wood;
        GameObject _concrete;
        GameObject _metal;

        [SetUp]
        public void SetUp()
        {
            _config = ScriptableObject.CreateInstance<ViewCheatsImpactVfxSection>();
            _fallback = new GameObject("Fallback");
            _config.WoodImpactPrefab = _wood = new GameObject("Wood");
            _config.ConcreteImpactPrefab = _concrete = new GameObject("Concrete");
            _config.MetalImpactPrefab = _metal = new GameObject("Metal");
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_config);
            Object.DestroyImmediate(_fallback);
            Object.DestroyImmediate(_wood);
            Object.DestroyImmediate(_concrete);
            Object.DestroyImmediate(_metal);
        }

        [Test]
        public void KnownSurfaces_SelectTheirAssignedPrefabs()
        {
            Assert.AreSame(_wood, _config.ResolveSurfaceImpact("wood", _fallback));
            Assert.AreSame(_concrete, _config.ResolveSurfaceImpact("concrete", _fallback));
            Assert.AreSame(_metal, _config.ResolveSurfaceImpact("metal", _fallback));
        }

        [TestCase("surface")]
        [TestCase("unknown")]
        [TestCase(null)]
        public void UnmappedSurface_UsesFallback(string surface)
        {
            Assert.AreSame(_fallback, _config.ResolveSurfaceImpact(surface, _fallback));
        }

        [TestCase("wood")]
        [TestCase("concrete")]
        [TestCase("metal")]
        public void MissingPrefab_UsesFallback(string surface)
        {
            _config.WoodImpactPrefab = null;
            _config.ConcreteImpactPrefab = null;
            _config.MetalImpactPrefab = null;
            Assert.AreSame(_fallback, _config.ResolveSurfaceImpact(surface, _fallback));
        }

        [Test]
        public void DisabledConfig_UsesFallback()
        {
            _config.Enabled = false;
            Assert.AreSame(_fallback, _config.ResolveSurfaceImpact("wood", _fallback));
        }
    }
}
