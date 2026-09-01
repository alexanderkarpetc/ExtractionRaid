using NUnit.Framework;
using Progression;
using Session;
using Systems;
using UnityEngine;

namespace Tests.EditMode
{
    [TestFixture]
    public class ProgressionCostSystemTests
    {
        ProgressionTreeConfig _cfg;
        Player _player;

        [SetUp]
        public void SetUp()
        {
            _cfg = ProgressionTreeDefaults.BuildRuntime();
            _player = new Player();
        }

        [TearDown]
        public void TearDown()
        {
            if (_cfg != null) Object.DestroyImmediate(_cfg);
        }

        [Test]
        public void CanUnlock_DevPointBypassesMissingMaterials()
        {
            _player.Progression.DevUnlockPoints = 1;

            Assert.IsTrue(ProgressionCostSystem.CanUnlock(_cfg, _player, "predator.0.0"));
        }

        [Test]
        public void TryUnlock_DevPointAllocatesNodeAndConsumesOnePoint()
        {
            _player.Progression.DevUnlockPoints = 2;

            bool unlocked = ProgressionCostSystem.TryUnlock(_cfg, _player, "predator.0.0");

            Assert.IsTrue(unlocked);
            CollectionAssert.Contains(_player.Progression.AllocatedNodeIds, "predator.0.0");
            Assert.AreEqual(1, _player.Progression.DevUnlockPoints);
        }

        [Test]
        public void CanUnlock_DevPointDoesNotBypassConnectivity()
        {
            _player.Progression.DevUnlockPoints = 1;

            Assert.IsFalse(ProgressionCostSystem.CanUnlock(_cfg, _player, "predator.0.1"));
            Assert.AreEqual(1, _player.Progression.DevUnlockPoints);
        }

        [Test]
        public void TryUnlock_FailureDoesNotConsumeDevPoint()
        {
            _player.Progression.DevUnlockPoints = 1;

            Assert.IsFalse(ProgressionCostSystem.TryUnlock(_cfg, _player, "predator.0.1"));
            Assert.AreEqual(1, _player.Progression.DevUnlockPoints);
        }

        [Test]
        public void CanUnlock_WithoutDevPointStillRequiresMaterials()
        {
            _cfg.TryFind("predator.0.0", out _, out _, out var node);
            Assert.Greater(node.Cost.Count, 0);

            Assert.IsFalse(ProgressionCostSystem.CanUnlock(_cfg, _player, "predator.0.0"));
        }

        [Test]
        public void DevUnlockPoints_DoNotRoundTripThroughSaveData()
        {
            _player.Progression.DevUnlockPoints = 10;
            var restored = new Player();

            restored.LoadFrom(_player.ToSaveData());

            Assert.AreEqual(0, restored.Progression.DevUnlockPoints);
        }
    }
}
