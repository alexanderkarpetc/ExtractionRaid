using NUnit.Framework;
using Systems;
using UnityEngine;
using View;

namespace Tests.EditMode
{
    [TestFixture]
    public class InteractableOutlineTargetTests
    {
        GameObject _gameObject;
        InteractableOutlineTarget _target;

        [SetUp]
        public void SetUp()
        {
            _gameObject = new GameObject("InteractableOutlineTargetTests");
            _gameObject.SetActive(false);
            _target = _gameObject.AddComponent<InteractableOutlineTarget>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_gameObject);
        }

        [Test]
        public void EffectiveActivationRadius_DefaultsToSharedLootRange()
        {
            _target.ActivationRadius = 1.25f;

            Assert.That(_target.EffectiveActivationRadius, Is.EqualTo(LootSystem.LootRange));
        }

        [Test]
        public void EffectiveActivationRadius_WhenOverridden_UsesLocalRadius()
        {
            _target.ActivationRadius = 1.25f;
            _target.OverrideActivationRadius = true;

            Assert.That(_target.EffectiveActivationRadius, Is.EqualTo(1.25f));
        }
    }
}
