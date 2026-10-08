using NUnit.Framework;
using UnityEngine;
using View;
using View.Audio;

namespace Tests.EditMode
{
    public class ImpactSurfaceTests
    {
        GameObject _root;
        Collider _collider;
        PhysicsMaterial _material;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("Surface root");
            var child = new GameObject("Collider child");
            child.transform.SetParent(_root.transform);
            _collider = child.AddComponent<BoxCollider>();
            _material = new PhysicsMaterial("steel");
            _collider.sharedMaterial = _material;
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_root);
            Object.DestroyImmediate(_material);
        }

        [TestCase(ImpactSurfaceType.Default, "surface")]
        [TestCase(ImpactSurfaceType.Wood, "wood")]
        [TestCase(ImpactSurfaceType.Concrete, "concrete")]
        [TestCase(ImpactSurfaceType.Metal, "metal")]
        public void ParentSurface_OverridesMaterialDetection(ImpactSurfaceType type, string expected)
        {
            _root.AddComponent<ImpactSurface>().Surface = type;
            Assert.AreEqual(expected, SurfaceAudioClassifier.Resolve(_collider));
        }

        [Test]
        public void ColliderSurface_OverridesParentSurface()
        {
            _root.AddComponent<ImpactSurface>().Surface = ImpactSurfaceType.Wood;
            _collider.gameObject.AddComponent<ImpactSurface>().Surface = ImpactSurfaceType.Concrete;
            Assert.AreEqual("concrete", SurfaceAudioClassifier.Resolve(_collider));
        }

        [TestCase("steel", "metal")]
        [TestCase("unclassified", "surface")]
        public void NoComponent_PreservesMaterialDetection(string materialName, string expected)
        {
            _material.name = materialName;
            Assert.AreEqual(expected, SurfaceAudioClassifier.Resolve(_collider));
        }

        [Test]
        public void MissingCollider_UsesGenericSurface()
        {
            Assert.AreEqual("surface", SurfaceAudioClassifier.Resolve(null));
        }
    }
}
