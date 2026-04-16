using NUnit.Framework;
using View;

namespace Tests.EditMode
{
    /// <summary>
    /// Pure-math tests for WeaponPullbackMath.ComputeRetract — boundary cases the
    /// sphere-cast caller cannot easily exercise in EditMode.
    /// </summary>
    [TestFixture]
    public class WeaponPullbackMathTests
    {
        [Test]
        public void NoHit_ReturnsZero()
        {
            float r = WeaponPullbackMath.ComputeRetract(float.PositiveInfinity, 0.3f, 1.2f);
            Assert.AreEqual(0f, r);
        }

        [Test]
        public void WallBehindPivot_FullRetract()
        {
            // cast origin is 0.3 behind pivot; hit at distance 0.2 from origin
            // means wall is 0.1 BEHIND the pivot (distFromPivot < 0) → full retract.
            float r = WeaponPullbackMath.ComputeRetract(0.2f, 0.3f, 1.2f);
            Assert.AreEqual(1f, r);
        }

        [Test]
        public void WallAtPivot_FullRetract()
        {
            // hit distance exactly equals origin backoff → distFromPivot == 0 → full retract.
            float r = WeaponPullbackMath.ComputeRetract(0.3f, 0.3f, 1.2f);
            Assert.AreEqual(1f, r);
        }

        [Test]
        public void WallAtWeaponLength_ZeroRetract()
        {
            // distFromPivot == weaponLength → boundary, no retract.
            float r = WeaponPullbackMath.ComputeRetract(0.3f + 1.2f, 0.3f, 1.2f);
            Assert.AreEqual(0f, r);
        }

        [Test]
        public void WallAtHalfLength_HalfRetract()
        {
            // distFromPivot = 0.6, weaponLength = 1.2 → retract = 1 - 0.5 = 0.5
            float r = WeaponPullbackMath.ComputeRetract(0.3f + 0.6f, 0.3f, 1.2f);
            Assert.AreEqual(0.5f, r, 0.0001f);
        }

        [Test]
        public void WallBeyondWeaponLength_ZeroRetract()
        {
            float r = WeaponPullbackMath.ComputeRetract(0.3f + 2.0f, 0.3f, 1.2f);
            Assert.AreEqual(0f, r);
        }

        [Test]
        public void ZeroWeaponLength_ReturnsZero()
        {
            // Defensive: avoid division by zero for invalid config.
            float r = WeaponPullbackMath.ComputeRetract(0.5f, 0.3f, 0f);
            Assert.AreEqual(0f, r);
        }

        [Test]
        public void ZeroOriginBackoff_WallAtPivot_FullRetract()
        {
            // No backoff → distFromPivot == castDistance. Wall at 0 = full retract.
            float r = WeaponPullbackMath.ComputeRetract(0f, 0f, 1.2f);
            Assert.AreEqual(1f, r);
        }

        [Test]
        public void ZeroOriginBackoff_PartialRetract()
        {
            // distFromPivot = 0.3, weaponLength = 1.0 → retract = 0.7
            float r = WeaponPullbackMath.ComputeRetract(0.3f, 0f, 1.0f);
            Assert.AreEqual(0.7f, r, 0.0001f);
        }
    }
}
