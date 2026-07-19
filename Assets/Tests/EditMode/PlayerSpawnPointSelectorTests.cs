using NUnit.Framework;
using UnityEngine;
using View.SpawnPoints;

namespace Tests.EditMode
{
    [TestFixture]
    public class PlayerSpawnPointSelectorTests
    {
        PlayerSpawnPoint[] _points;

        [TearDown]
        public void TearDown()
        {
            if (_points == null) return;
            foreach (var point in _points)
                if (point != null) Object.DestroyImmediate(point.gameObject);
        }

        [Test]
        public void Pick_NoPoints_ReturnsNull()
        {
            Assert.IsNull(PlayerSpawnPointSelector.Pick(System.Array.Empty<PlayerSpawnPoint>(), 0.5f));
        }

        [Test]
        public void Pick_WeightedRoll_SelectsExpectedPoint()
        {
            _points = CreatePoints(6f, 3f, 1f);

            Assert.AreSame(_points[0], PlayerSpawnPointSelector.Pick(_points, 0.59f));
            Assert.AreSame(_points[1], PlayerSpawnPointSelector.Pick(_points, 0.60f));
            Assert.AreSame(_points[2], PlayerSpawnPointSelector.Pick(_points, 0.95f));
        }

        [Test]
        public void Pick_ZeroWeightPoint_IsSkipped()
        {
            _points = CreatePoints(0f, 1f);

            Assert.AreSame(_points[1], PlayerSpawnPointSelector.Pick(_points, 0f));
            Assert.AreSame(_points[1], PlayerSpawnPointSelector.Pick(_points, 1f));
        }

        [Test]
        public void Pick_AllWeightsZero_FallsBackToEqualChance()
        {
            _points = CreatePoints(0f, 0f, 0f);

            Assert.AreSame(_points[0], PlayerSpawnPointSelector.Pick(_points, 0f));
            Assert.AreSame(_points[1], PlayerSpawnPointSelector.Pick(_points, 0.5f));
            Assert.AreSame(_points[2], PlayerSpawnPointSelector.Pick(_points, 1f));
        }

        static PlayerSpawnPoint[] CreatePoints(params float[] weights)
        {
            var points = new PlayerSpawnPoint[weights.Length];
            for (int i = 0; i < weights.Length; i++)
            {
                points[i] = new GameObject($"PlayerSpawnPoint_{i}").AddComponent<PlayerSpawnPoint>();
                points[i].weight = weights[i];
            }
            return points;
        }
    }
}
