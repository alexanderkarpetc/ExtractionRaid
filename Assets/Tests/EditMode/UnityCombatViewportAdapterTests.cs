using Adapters;
using Dev;
using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode
{
    [TestFixture]
    public class UnityCombatViewportAdapterTests
    {
        GameObject _cameraObject;
        Camera _camera;
        UnityCombatViewportAdapter _adapter;

        [SetUp]
        public void SetUp()
        {
            _cameraObject = new GameObject("CombatViewportTestCamera");
            _camera = _cameraObject.AddComponent<Camera>();
            _camera.orthographic = true;
            _camera.orthographicSize = 5f;
            _camera.aspect = 1f;
            _adapter = new UnityCombatViewportAdapter();
            _adapter.SetCamera(_camera);
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_cameraObject);

        [Test]
        public void IsInside_CenterPoint_IsTrue()
        {
            Assert.IsTrue(_adapter.IsInside(new Vector3(0f, 0f, 5f), 0.12f));
        }

        [Test]
        public void IsInside_PointInMargin_IsFalse()
        {
            Assert.IsFalse(_adapter.IsInside(new Vector3(4.5f, 0f, 5f), 0.12f));
        }

        [Test]
        public void IsInside_PointBehindCamera_IsFalse()
        {
            Assert.IsFalse(_adapter.IsInside(new Vector3(0f, 0f, -5f), 0f));
        }

        [Test]
        public void BotEngagementAsset_HasUsableViewportMargins()
        {
            var config = Resources.Load<DevCheatsBotEngagementSection>(
                "Configs/DevCheats/BotEngagement");

            Assert.IsNotNull(config);
            Assert.Greater(config.ViewportEnterMargin, 0f);
            Assert.GreaterOrEqual(config.ViewportExitMargin, 0f);
            Assert.Less(config.ViewportExitMargin, config.ViewportEnterMargin);
        }
    }
}
