using NUnit.Framework;
using Systems;
using UnityEngine;
using View;
using View.SpawnPoints;
using State;

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

        [Test]
        public void InteractionZone_AutoFindsPointOnSameObject()
        {
            var point = _gameObject.AddComponent<WorkbenchSpawnPoint>();
            _target.RefreshRenderers();
            Assert.AreSame(point, _target.InteractionZone);
        }

        [Test]
        public void InteractionZone_AutoFindsParentPoint()
        {
            var child = new GameObject("Outline child");
            child.SetActive(false);
            child.transform.SetParent(_gameObject.transform);
            var point = _gameObject.AddComponent<WorkbenchSpawnPoint>();
            var outline = child.AddComponent<InteractableOutlineTarget>();
            outline.RefreshRenderers();
            Assert.AreSame(point, outline.InteractionZone);
        }

        [Test]
        public void InteractionZone_UsesBoundEligibilityAndIgnoresRadius()
        {
            var point = _gameObject.AddComponent<WorkbenchSpawnPoint>();
            _target.InteractionZone = point;
            _target.OverrideActivationRadius = true;
            _target.ActivationRadius = 100f;
            Assert.IsFalse(_target.IsInteractionZoneActive, "Unbound zones must not use radius fallback.");

            var state = WorkbenchState.Create(EId.None, Vector3.zero);
            point.Bind(state);
            Assert.IsFalse(_target.IsInteractionZoneActive);
            state.IsPlayerInInteractionZone = true;
            _target.ActivationRadius = 0f;
            Assert.IsTrue(_target.IsInteractionZoneActive);
            state.IsPlayerInInteractionZone = false;
            Assert.IsFalse(_target.IsInteractionZoneActive);
        }
    }
}
